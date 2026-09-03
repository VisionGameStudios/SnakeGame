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
    private string downloadError;
    private float downloadProgress;

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

        int width = Mathf.Min(600, Screen.width - 32);
        int height = Mathf.Min(340, Screen.height - 32);
        Rect panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        GUI.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 32,
            fontStyle = FontStyle.Bold
        };
        title.normal.textColor = new Color(1f, 0.9f, 0.3f);

        GUIStyle body = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            wordWrap = true
        };
        body.normal.textColor = Color.white;

        GUI.Label(new Rect(panel.x + 20, panel.y + 24, panel.width - 40, 50), "NUEVA VERSION", title);
        GUI.Label(new Rect(panel.x + 40, panel.y + 82, panel.width - 80, 66), remoteVersion.message, body);
        GUI.Label(new Rect(panel.x + 40, panel.y + 145, panel.width - 80, 30), Application.version + "  >  " + remoteVersion.latestVersion, body);

        if (remoteVersion.size > 0)
        {
            GUI.Label(new Rect(panel.x + 40, panel.y + 174, panel.width - 80, 25), FormatBytes(remoteVersion.size), body);
        }

        if (downloading)
        {
            GUI.Box(new Rect(panel.x + 40, panel.y + 208, panel.width - 80, 24), GUIContent.none);
            GUI.Box(new Rect(panel.x + 40, panel.y + 208, (panel.width - 80) * downloadProgress, 24), GUIContent.none);
            GUI.Label(new Rect(panel.x + 40, panel.y + 235, panel.width - 80, 25), "Descargando " + Mathf.RoundToInt(downloadProgress * 100f) + "%", body);
        }

        if (!string.IsNullOrWhiteSpace(downloadError))
        {
            GUI.Label(new Rect(panel.x + 30, panel.y + 205, panel.width - 60, 55), downloadError, body);
        }

        bool guiWasEnabled = GUI.enabled;
        GUI.enabled = !downloading;
        if (GUI.Button(new Rect(panel.x + panel.width / 2f - 175, panel.y + panel.height - 65, 165, 45), "ACTUALIZAR"))
        {
            StartCoroutine(DownloadAndInstall());
        }
        GUI.enabled = guiWasEnabled;

        if (!mandatoryUpdate && !downloading && GUI.Button(new Rect(panel.x + panel.width / 2f + 10, panel.y + panel.height - 65, 165, 45), "MAS TARDE"))
        {
            showUpdate = false;
        }
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
        downloadError = "";
        downloadProgress = 0f;
        string directory = Path.Combine(Application.temporaryCachePath, "updates");
        string archivePath = Path.Combine(directory, "Snake-" + remoteVersion.latestVersion + ".zip");
        Directory.CreateDirectory(directory);

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
        if (!string.Equals(ComputeSha256(archivePath), remoteVersion.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(archivePath);
            downloadError = "La actualización no pasó la verificación de integridad.";
            downloading = false;
            yield break;
        }

        string updaterSource = Path.Combine(Application.dataPath, "Resources", "Updater.app");
        if (!Directory.Exists(updaterSource))
        {
            downloadError = "Este build no contiene el instalador automático.";
            downloading = false;
            yield break;
        }

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
                yield break;
            }
        }

        string installedApp = Directory.GetParent(Application.dataPath).FullName;
        string updaterExecutable = Path.Combine(updaterPath, "Contents", "MacOS", UpdaterName);
        Process.Start(new ProcessStartInfo
        {
            FileName = updaterExecutable,
            Arguments = Quote(archivePath) + " " + Quote(installedApp) + " " + Process.GetCurrentProcess().Id,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Application.Quit();
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
