using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ReleasePublisher : EditorWindow
{
    private const string RepositoryOwner = "VisionGameStudios";
    private const string RepositoryName = "SnakeGame";
    private const string KeychainService = "SnakeGame Unity Publisher";

    private string version = "1.0.1";
    private string releaseNotes = "Nueva versión de Snake.";
    private string githubToken = "";
    private bool rememberToken = true;
    private bool mandatory;
    private bool publishing;
    private string status = "";

    private void OnEnable()
    {
        githubToken = LoadTokenFromKeychain();
        version = NextPatchVersion(PlayerSettings.bundleVersion);
    }

    [MenuItem("Tools/Snake/Publicar versión")]
    private static void OpenWindow()
    {
        GetWindow<ReleasePublisher>("Publicar Snake");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Publicar nueva versión", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Genera el build de la plataforma activa, actualiza version.json, publica el código y crea un GitHub Release.",
            MessageType.Info
        );

        using (new EditorGUI.DisabledScope(publishing))
        {
            version = EditorGUILayout.TextField("Versión", version);
            releaseNotes = EditorGUILayout.TextArea(releaseNotes, GUILayout.Height(70));
            mandatory = EditorGUILayout.Toggle("Actualización obligatoria", mandatory);
            githubToken = EditorGUILayout.PasswordField("GitHub token", githubToken);
            rememberToken = EditorGUILayout.Toggle("Guardar en Llavero", rememberToken);

            GUILayout.Space(8);
            if (GUILayout.Button("GENERAR Y PUBLICAR", GUILayout.Height(38)))
            {
                Publish();
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(status, publishing ? MessageType.Info : MessageType.None);
        }
    }

    private void Publish()
    {
        Version parsedVersion;
        if (!Version.TryParse(NormalizeVersion(version), out parsedVersion))
        {
            EditorUtility.DisplayDialog("Versión inválida", "Usa un formato como 1.1.0.", "Aceptar");
            return;
        }

        Version currentVersion;
        if (Version.TryParse(NormalizeVersion(PlayerSettings.bundleVersion), out currentVersion)
            && parsedVersion <= currentVersion)
        {
            EditorUtility.DisplayDialog(
                "Versión no incrementada",
                "La nueva versión debe ser mayor que " + currentVersion.ToString(3) + ". Prueba con " + NextPatchVersion(currentVersion.ToString(3)) + ".",
                "Aceptar"
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(githubToken))
        {
            EditorUtility.DisplayDialog("Falta el token", "Introduce un GitHub Personal Access Token con acceso al repositorio.", "Aceptar");
            return;
        }

        publishing = true;
        try
        {
            if (rememberToken)
                SaveTokenToKeychain(githubToken);
            else
                DeleteTokenFromKeychain();

            string normalizedVersion = parsedVersion.ToString(3);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            PlayerSettings.bundleVersion = normalizedVersion;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string archivePath = BuildAndArchive(projectRoot, normalizedVersion);
            WriteVersionManifest(projectRoot, normalizedVersion, archivePath);
            AssetDatabase.Refresh();
            CommitAndPush(projectRoot, normalizedVersion);
            CreateRelease(normalizedVersion, archivePath);

            status = "Versión " + normalizedVersion + " publicada correctamente.";
            EditorUtility.DisplayDialog("Publicación completada", status, "Aceptar");
        }
        catch (Exception exception)
        {
            status = "No se pudo publicar: " + exception.Message;
            UnityEngine.Debug.LogException(exception);
            EditorUtility.DisplayDialog("Error al publicar", status, "Aceptar");
        }
        finally
        {
            publishing = false;
            if (!rememberToken)
                githubToken = "";
            Repaint();
        }
    }

    private void WriteVersionManifest(string projectRoot, string normalizedVersion, string archivePath)
    {
        string minimumVersion = mandatory ? normalizedVersion : "1.0.0";
        string archiveName = Path.GetFileName(archivePath);
        string archiveUrl = "https://github.com/" + RepositoryOwner + "/" + RepositoryName
            + "/releases/download/v" + normalizedVersion + "/" + Uri.EscapeDataString(archiveName);
        string sha256 = ComputeSha256(archivePath);
        long size = new FileInfo(archivePath).Length;
        string escapedNotes = releaseNotes.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        string json = "{\n"
            + "  \"latestVersion\": \"" + normalizedVersion + "\",\n"
            + "  \"minimumVersion\": \"" + minimumVersion + "\",\n"
            + "  \"downloadUrl\": \"" + archiveUrl + "\",\n"
            + "  \"sha256\": \"" + sha256 + "\",\n"
            + "  \"size\": " + size + ",\n"
            + "  \"message\": \"" + escapedNotes + "\"\n"
            + "}\n";
        File.WriteAllText(Path.Combine(projectRoot, "version.json"), json);
    }

    private static string NextPatchVersion(string current)
    {
        Version parsed;
        if (!Version.TryParse(NormalizeVersion(current), out parsed))
            return "1.0.1";

        return parsed.Major + "." + parsed.Minor + "." + (parsed.Build + 1);
    }

    private static string BuildAndArchive(string projectRoot, string normalizedVersion)
    {
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string buildFolder = Path.Combine(projectRoot, "Builds", "Snake-" + normalizedVersion);
        Directory.CreateDirectory(buildFolder);

        string executableName;
        if (target == BuildTarget.StandaloneOSX)
            executableName = "Snake.app";
        else if (target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64)
            executableName = "Snake.exe";
        else
            throw new InvalidOperationException("Selecciona macOS o Windows en Build Profiles antes de publicar.");

        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No hay escenas activas en Build Profiles.");

        BuildReport report = BuildPipeline.BuildPlayer(scenes, Path.Combine(buildFolder, executableName), target, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("El build falló. Revisa la consola de Unity.");

        if (target == BuildTarget.StandaloneOSX)
            BuildUpdaterApp(projectRoot, Path.Combine(buildFolder, executableName));

        string archivePath = Path.Combine(projectRoot, "Builds", "Snake-" + normalizedVersion + "-" + target + ".zip");
        if (File.Exists(archivePath)) File.Delete(archivePath);
        ZipFile.CreateFromDirectory(
            buildFolder,
            archivePath,
            System.IO.Compression.CompressionLevel.Optimal,
            false
        );
        return archivePath;
    }

    private static void BuildUpdaterApp(string projectRoot, string gameAppPath)
    {
        string sourcePath = Path.Combine(projectRoot, "Assets", "Editor", "Updater", "Updater.swift");
        if (!File.Exists(sourcePath))
            throw new InvalidOperationException("No se encontró Assets/Editor/Updater/Updater.swift.");

        string updaterApp = Path.Combine(gameAppPath, "Contents", "Resources", "Updater.app");
        string executablePath = Path.Combine(updaterApp, "Contents", "MacOS", "SnakeUpdater");
        string infoPath = Path.Combine(updaterApp, "Contents", "Info.plist");
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath));
        File.WriteAllText(infoPath,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
            + "<plist version=\"1.0\"><dict>"
            + "<key>CFBundleExecutable</key><string>SnakeUpdater</string>"
            + "<key>CFBundleIdentifier</key><string>com.visiongamestudios.snake.updater</string>"
            + "<key>CFBundleName</key><string>SnakeUpdater</string>"
            + "<key>CFBundlePackageType</key><string>APPL</string>"
            + "</dict></plist>\n");
        RunProcess("/usr/bin/xcrun", "swiftc \"" + sourcePath + "\" -o \"" + executablePath + "\"", projectRoot, null);
        RunProcess("/bin/chmod", "+x \"" + executablePath + "\"", projectRoot, null);

        if (!File.Exists(executablePath) || new FileInfo(executablePath).Length == 0)
            throw new InvalidOperationException("No se pudo incluir el instalador automático en Snake.app.");
    }

    private static string ComputeSha256(string path)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private void CommitAndPush(string projectRoot, string normalizedVersion)
    {
        RunProcess("git", "add Assets Packages ProjectSettings version.json .gitignore", projectRoot, null);
        if (!ProcessSucceeds("git", "diff --cached --quiet", projectRoot))
        {
            RunProcess("git", "commit -m \"Release v" + normalizedVersion + "\"", projectRoot, null);
        }

        // En un reintento conserva el tag existente para no modificar uno ya publicado.
        if (!ProcessSucceeds("git", "rev-parse -q --verify refs/tags/v" + normalizedVersion, projectRoot))
        {
            RunProcess("git", "tag -a v" + normalizedVersion + " -m \"Release v" + normalizedVersion + "\"", projectRoot, null);
        }

        string askPassPath = Path.Combine(Path.GetTempPath(), "snake-git-askpass.sh");
        File.WriteAllText(askPassPath, "#!/bin/sh\ncase \"$1\" in *Username*) echo x-access-token;; *) echo \"$GITHUB_TOKEN\";; esac\n");
        RunProcess("chmod", "+x \"" + askPassPath + "\"", projectRoot, null);

        var environment = new System.Collections.Generic.Dictionary<string, string>
        {
            { "GIT_ASKPASS", askPassPath },
            { "GIT_TERMINAL_PROMPT", "0" },
            { "GITHUB_TOKEN", githubToken }
        };

        try
        {
            RunProcess(
                "git",
                "push origin main refs/tags/v" + normalizedVersion,
                projectRoot,
                environment
            );
        }
        finally
        {
            if (File.Exists(askPassPath)) File.Delete(askPassPath);
        }
    }

    private void CreateRelease(string normalizedVersion, string archivePath)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SnakeGame-Unity-Publisher");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            string body = "{\"tag_name\":\"v" + normalizedVersion + "\",\"name\":\"Snake v" + normalizedVersion
                + "\",\"body\":\"" + releaseNotes.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"}";
            HttpResponseMessage response = client.PostAsync(
                "https://api.github.com/repos/" + RepositoryOwner + "/" + RepositoryName + "/releases",
                new StringContent(body, Encoding.UTF8, "application/json")
            ).GetAwaiter().GetResult();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("GitHub rechazó el release: " + responseBody);

            ReleaseResponse release = JsonUtility.FromJson<ReleaseResponse>(responseBody);
            byte[] archive = File.ReadAllBytes(archivePath);
            using (ByteArrayContent content = new ByteArrayContent(archive))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                string uploadUrl = "https://uploads.github.com/repos/" + RepositoryOwner + "/" + RepositoryName
                    + "/releases/" + release.id + "/assets?name=" + Uri.EscapeDataString(Path.GetFileName(archivePath));
                HttpResponseMessage upload = client.PostAsync(uploadUrl, content).GetAwaiter().GetResult();
                if (!upload.IsSuccessStatusCode)
                    throw new InvalidOperationException("No se pudo adjuntar el ZIP: " + upload.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        }
    }

    [Serializable]
    private class ReleaseResponse
    {
        public long id;
    }

    private static void RunProcess(string executable, string arguments, string workingDirectory,
        System.Collections.Generic.Dictionary<string, string> environment)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (environment != null)
        {
            foreach (var pair in environment)
                startInfo.EnvironmentVariables[pair.Key] = pair.Value;
        }

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(executable + " falló: " + error + output);
        }
    }

    private static bool ProcessSucceeds(string executable, string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
    }

    private static string LoadTokenFromKeychain()
    {
        if (Application.platform != RuntimePlatform.OSXEditor)
            return "";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/security",
            Arguments = "find-generic-password -a \"" + RepositoryOwner + "\" -s \"" + KeychainService + "\" -w",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string token = process.StandardOutput.ReadToEnd().Trim();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? token : "";
        }
    }

    private static void SaveTokenToKeychain(string token)
    {
        if (Application.platform != RuntimePlatform.OSXEditor)
            return;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "/bin/zsh",
            Arguments = "-c \"IFS= read -r token; /usr/bin/security add-generic-password -U -a '"
                + RepositoryOwner + "' -s '" + KeychainService + "' -w \\\"$token\\\"\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            process.StandardInput.WriteLine(token);
            process.StandardInput.Close();
            process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("No se pudo guardar el token en el Llavero: " + error);
        }
    }

    private static void DeleteTokenFromKeychain()
    {
        if (Application.platform != RuntimePlatform.OSXEditor)
            return;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/security",
            Arguments = "delete-generic-password -a \"" + RepositoryOwner + "\" -s \"" + KeychainService + "\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
        }
    }

    private static string NormalizeVersion(string rawVersion)
    {
        string cleaned = rawVersion.Trim().TrimStart('v', 'V');
        int components = cleaned.Split('.').Length;
        if (components == 1) return cleaned + ".0.0";
        if (components == 2) return cleaned + ".0";
        return cleaned;
    }
}
