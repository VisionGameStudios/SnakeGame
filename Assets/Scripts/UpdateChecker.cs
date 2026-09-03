using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using UnityEngine;
using UnityEngine.Networking;

public class UpdateChecker : MonoBehaviour
{
    private const string VersionUrl = "https://raw.githubusercontent.com/VisionGameStudios/SnakeGame/main/version.json";
    private const string UpdaterName = "SnakeUpdater";

    [Serializable]
    private class VersionInfo
    {
        public string latestVersion;
        public string minimumVersion;
        public string downloadUrl;
        public string sha256;
        public long size;
        public string message;
    }

    private VersionInfo remoteVersion;
    private bool showUpdate;
    private bool mandatoryUpdate;
    private bool downloading;
    private bool preparingInstall;
    private string downloadError;
    private float downloadProgress;
    private static Texture2D updateButtonTexture;
    private static Texture2D updateButtonHoverTexture;
    private static Texture2D updateButtonActiveTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (FindFirstObjectByType<UpdateChecker>() != null)
        {
            return;
        }

        GameObject checker = new GameObject("Update Checker");
        DontDestroyOnLoad(checker);
        checker.AddComponent<UpdateChecker>();
    }

    private void Start()
    {
        StartCoroutine(CheckForUpdates());
    }

    private IEnumerator CheckForUpdates()
    {
        // El parámetro evita que GitHub/CDN o un proxy entregue un version.json
        // guardado de una publicación anterior.
        string uncachedUrl = VersionUrl + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using (UnityWebRequest request = UnityWebRequest.Get(uncachedUrl))
        {
            request.timeout = 8;
            request.SetRequestHeader("Cache-Control", "no-cache");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("No se pudo comprobar si hay actualizaciones: " + request.error);
                yield break;
            }

            remoteVersion = JsonUtility.FromJson<VersionInfo>(request.downloadHandler.text);
            if (remoteVersion == null || string.IsNullOrWhiteSpace(remoteVersion.latestVersion))
            {
                Debug.LogWarning("El archivo remoto version.json no contiene una versión válida.");
                yield break;
            }

            showUpdate = CompareVersions(remoteVersion.latestVersion, Application.version) > 0;
            mandatoryUpdate = !string.IsNullOrWhiteSpace(remoteVersion.minimumVersion)
                && CompareVersions(remoteVersion.minimumVersion, Application.version) > 0;

            Debug.Log("Actualizaciones: instalada " + Application.version
                + ", disponible " + remoteVersion.latestVersion
                + (showUpdate ? " (se mostrará el aviso)." : " (está al día)."));
        }
    }

    private void OnGUI()
    {
        if (!showUpdate || remoteVersion == null)
        {
            return;
        }

        // Los valores menores se dibujan delante en IMGUI. Así el menú del juego
        // nunca puede ocultar el aviso de actualización.
        GUI.depth = -1000;

        int width = Mathf.Min(620, Screen.width - 32);
        int height = Mathf.Min(360, Screen.height - 32);
        Rect panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        DrawOverlay(new Color(0.01f, 0.03f, 0.06f, 0.78f));
        DrawPanel(new Rect(panel.x + 10, panel.y + 10, panel.width, panel.height), new Color(0f, 0f, 0f, 0.42f));
        DrawPanel(panel, new Color(0.05f, 0.1f, 0.17f, 0.99f));
        DrawPanel(new Rect(panel.x + 8, panel.y + 8, panel.width - 16, panel.height - 16), new Color(0.08f, 0.15f, 0.23f, 0.96f));
        DrawBorder(panel, 4, new Color(0.3f, 0.9f, 0.48f));
        DrawPanel(new Rect(panel.x + 4, panel.y + 4, panel.width - 8, 5), new Color(1f, 0.82f, 0.2f));

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };
        title.normal.textColor = new Color(1f, 0.9f, 0.3f);

        GUIStyle body = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            wordWrap = true
        };
        body.normal.textColor = Color.white;

        GUI.Label(new Rect(panel.x + 24, panel.y + 20, panel.width - 48, 44), "NUEVA VERSION", title);
        GUI.Label(new Rect(panel.x + 48, panel.y + 66, panel.width - 96, 42), remoteVersion.message, body);

        Rect versionRect = new Rect(panel.x + 72, panel.y + 119, panel.width - 144, 44);
        DrawPanel(versionRect, new Color(0.02f, 0.05f, 0.09f, 0.9f));
        DrawBorder(versionRect, 2, new Color(0.16f, 0.34f, 0.43f));
        GUI.Label(versionRect, Application.version + "  >  " + remoteVersion.latestVersion, title);

        if (remoteVersion.size > 0)
        {
            GUI.Label(new Rect(panel.x + 40, panel.y + 169, panel.width - 80, 24), FormatBytes(remoteVersion.size), body);
        }

        if (downloading)
        {
            Rect progressRect = new Rect(panel.x + 50, panel.y + 205, panel.width - 100, 18);
            DrawPanel(progressRect, new Color(0.02f, 0.04f, 0.07f));
            DrawPanel(new Rect(progressRect.x, progressRect.y, progressRect.width * downloadProgress, progressRect.height), new Color(0.3f, 0.9f, 0.48f));
            string progressLabel = preparingInstall
                ? "PREPARANDO INSTALACION..."
                : "DESCARGANDO " + Mathf.RoundToInt(downloadProgress * 100f) + "%";
            GUI.Label(new Rect(panel.x + 40, panel.y + 228, panel.width - 80, 24), progressLabel, body);
        }

        if (!string.IsNullOrWhiteSpace(downloadError))
        {
            GUIStyle error = new GUIStyle(body);
            error.normal.textColor = new Color(1f, 0.62f, 0.5f);
            GUI.Label(new Rect(panel.x + 38, panel.y + 201, panel.width - 76, 50), downloadError, error);
        }

        GUIStyle button = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        button.normal.textColor = new Color(0.02f, 0.04f, 0.07f);
        button.hover.textColor = Color.black;
        button.active.textColor = Color.black;
        button.normal.background = GetTexture(ref updateButtonTexture, new Color(1f, 0.84f, 0.25f));
        button.hover.background = GetTexture(ref updateButtonHoverTexture, new Color(1f, 0.94f, 0.5f));
        button.active.background = GetTexture(ref updateButtonActiveTexture, new Color(0.88f, 0.7f, 0.16f));

        bool guiWasEnabled = GUI.enabled;
        GUI.enabled = !downloading;
        if (GUI.Button(new Rect(panel.x + panel.width / 2f - 180, panel.y + panel.height - 64, 170, 44), "ACTUALIZAR", button))
        {
            StartCoroutine(DownloadAndInstall());
        }
        GUI.enabled = guiWasEnabled;

        if (!mandatoryUpdate && !downloading && GUI.Button(new Rect(panel.x + panel.width / 2f + 10, panel.y + panel.height - 64, 170, 44), "MAS TARDE", button))
        {
            showUpdate = false;
        }
    }

    private static void DrawOverlay(Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static void DrawPanel(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static void DrawBorder(Rect rect, int thickness, Color color)
    {
        DrawPanel(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawPanel(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawPanel(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawPanel(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static Texture2D GetTexture(ref Texture2D texture, Color color)
    {
        if (texture != null)
        {
            return texture;
        }

        texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private IEnumerator DownloadAndInstall()
    {
        if (Application.platform != RuntimePlatform.OSXPlayer)
        {
            downloadError = "La actualización automática está disponible en macOS.";
            yield break;
        }

        if (string.IsNullOrWhiteSpace(remoteVersion.downloadUrl) || string.IsNullOrWhiteSpace(remoteVersion.sha256))
        {
            downloadError = "Esta versión no tiene un paquete verificable.";
            yield break;
        }

        downloading = true;
        preparingInstall = false;
        downloadError = "";
        downloadProgress = 0f;
        string directory = Path.Combine(Application.temporaryCachePath, "updates");
        string archivePath = Path.Combine(directory, "Snake-" + remoteVersion.latestVersion + ".zip");
        Directory.CreateDirectory(directory);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using (UnityWebRequest request = UnityWebRequest.Get(remoteVersion.downloadUrl))
        {
            request.downloadHandler = new DownloadHandlerFile(archivePath);
            request.timeout = 300;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                downloadProgress = request.downloadProgress;
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                downloadError = "No se pudo descargar la actualización: " + request.error;
                downloading = false;
                yield break;
            }
        }

        downloadProgress = 1f;
        preparingInstall = true;
        if (!string.Equals(ComputeSha256(archivePath), remoteVersion.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(archivePath);
            downloadError = "La actualización no pasó la verificación de integridad.";
            downloading = false;
            yield break;
        }

        // Application.dataPath cambia entre versiones de Unity. Localizamos el
        // bundle .app recorriendo sus padres y partimos de una ruta conocida.
        DirectoryInfo appDirectory = FindContainingAppBundle(Application.dataPath);
        string updaterSource = appDirectory != null
            ? Path.Combine(appDirectory.FullName, "Contents", "Resources", "Updater.app")
            : "";

        if (appDirectory == null || !appDirectory.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            downloadError = "No se pudo localizar la aplicación instalada.";
            downloading = false;
            preparingInstall = false;
            yield break;
        }

        string installedApp = appDirectory.FullName;
        ProcessStartInfo installerInfo;

        if (Directory.Exists(updaterSource))
        {
            string updaterPath = Path.Combine(directory, UpdaterName + "-" + DateTime.UtcNow.Ticks + ".app");
            ProcessStartInfo copyInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/ditto",
                Arguments = Quote(updaterSource) + " " + Quote(updaterPath),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process copy = Process.Start(copyInfo))
            {
                copy.WaitForExit();
                if (copy.ExitCode != 0)
                {
                    downloadError = "No se pudo preparar el instalador.";
                    downloading = false;
                    preparingInstall = false;
                    yield break;
                }
            }

            installerInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(updaterPath, "Contents", "MacOS", UpdaterName),
                Arguments = Quote(archivePath) + " " + Quote(installedApp) + " " + Process.GetCurrentProcess().Id,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            // Respaldo para builds que por cualquier motivo perdieron Updater.app.
            // El script se ejecuta fuera del juego, espera a que cierre y lo reemplaza.
            string fallbackScript = CreateFallbackUpdater(directory);
            installerInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = Quote(fallbackScript) + " " + Quote(archivePath) + " " + Quote(installedApp) + " " + Process.GetCurrentProcess().Id,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        Process.Start(installerInfo);
        Application.Quit();
    }

    private static DirectoryInfo FindContainingAppBundle(string path)
    {
        DirectoryInfo current = new DirectoryInfo(path);
        while (current != null)
        {
            if (current.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string CreateFallbackUpdater(string directory)
    {
        string scriptPath = Path.Combine(directory, "SnakeUpdater-" + DateTime.UtcNow.Ticks + ".sh");
        string script = "#!/bin/bash\n"
            + "archive=\"$1\"\n"
            + "installed=\"$2\"\n"
            + "game_pid=\"$3\"\n"
            + "while kill -0 \"$game_pid\" 2>/dev/null; do sleep 1; done\n"
            + "work=$(/usr/bin/mktemp -d /tmp/SnakeUpdate.XXXXXX) || exit 1\n"
            + "/usr/bin/ditto -x -k \"$archive\" \"$work\" || exit 1\n"
            + "new_app=\"$work/Snake.app\"\n"
            + "[ -d \"$new_app\" ] || exit 1\n"
            + "backup=\"${installed}.backup\"\n"
            + "/bin/rm -rf \"$backup\"\n"
            + "/bin/mv \"$installed\" \"$backup\" || exit 1\n"
            + "/bin/mv \"$new_app\" \"$installed\" || { /bin/mv \"$backup\" \"$installed\"; exit 1; }\n"
            + "/usr/bin/open \"$installed\"\n"
            + "/bin/rm -rf \"$work\"\n"
            + "/bin/rm -f \"$archive\" \"$0\"\n";
        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    private static string ComputeSha256(string path)
    {
        using (SHA256 sha256 = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("0.0") + " KB";
        return (bytes / 1024f / 1024f).ToString("0.0") + " MB";
    }

    private static int CompareVersions(string left, string right)
    {
        Version leftVersion;
        Version rightVersion;

        if (!Version.TryParse(NormalizeVersion(left), out leftVersion)
            || !Version.TryParse(NormalizeVersion(right), out rightVersion))
        {
            return 0;
        }

        return leftVersion.CompareTo(rightVersion);
    }

    private static string NormalizeVersion(string version)
    {
        string cleaned = version.Trim().TrimStart('v', 'V');
        int components = cleaned.Split('.').Length;

        if (components == 1) return cleaned + ".0.0";
        if (components == 2) return cleaned + ".0";
        return cleaned;
    }
}
