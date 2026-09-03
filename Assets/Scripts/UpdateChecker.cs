using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class UpdateChecker : MonoBehaviour
{
    private const string VersionUrl = "https://raw.githubusercontent.com/VisionGameStudios/SnakeGame/main/version.json";

    [Serializable]
    private class VersionInfo
    {
        public string latestVersion;
        public string minimumVersion;
        public string downloadUrl;
        public string message;
    }

    private VersionInfo remoteVersion;
    private bool showUpdate;
    private bool mandatoryUpdate;

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
        int height = Mathf.Min(280, Screen.height - 32);
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

        if (GUI.Button(new Rect(panel.x + panel.width / 2f - 175, panel.y + panel.height - 65, 165, 45), "ACTUALIZAR"))
        {
            Application.OpenURL(remoteVersion.downloadUrl);
        }

        if (!mandatoryUpdate && GUI.Button(new Rect(panel.x + panel.width / 2f + 10, panel.y + panel.height - 65, 165, 45), "MAS TARDE"))
        {
            showUpdate = false;
        }
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
