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
        using (UnityWebRequest request = UnityWebRequest.Get(VersionUrl))
        {
            request.timeout = 8;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }

            remoteVersion = JsonUtility.FromJson<VersionInfo>(request.downloadHandler.text);
            if (remoteVersion == null || string.IsNullOrWhiteSpace(remoteVersion.latestVersion))
            {
                yield break;
            }

            showUpdate = CompareVersions(remoteVersion.latestVersion, Application.version) > 0;
            mandatoryUpdate = !string.IsNullOrWhiteSpace(remoteVersion.minimumVersion)
                && CompareVersions(remoteVersion.minimumVersion, Application.version) > 0;
        }
    }

    private void OnGUI()
    {
        if (!showUpdate || remoteVersion == null)
        {
            return;
        }

        int width = Mathf.Min(520, Screen.width - 32);
        int height = 240;
        Rect panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        GUI.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

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
            fontSize = 16,
            wordWrap = true
        };
        body.normal.textColor = Color.white;

        GUI.Label(new Rect(panel.x + 20, panel.y + 20, panel.width - 40, 45), "NUEVA VERSION", title);
        GUI.Label(new Rect(panel.x + 35, panel.y + 72, panel.width - 70, 60), remoteVersion.message, body);
        GUI.Label(new Rect(panel.x + 35, panel.y + 125, panel.width - 70, 28), Application.version + "  >  " + remoteVersion.latestVersion, body);

        if (GUI.Button(new Rect(panel.x + panel.width / 2f - 155, panel.y + 175, 145, 42), "ACTUALIZAR"))
        {
            Application.OpenURL(remoteVersion.downloadUrl);
        }

        if (!mandatoryUpdate && GUI.Button(new Rect(panel.x + panel.width / 2f + 10, panel.y + 175, 145, 42), "MAS TARDE"))
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
