#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

/// <summary>
/// Adds a Tools > Docs menu to the Unity Editor:
///   Open Documentation     opens the portal in the browser
///   Reveal Docs Folder     shows the portal file in the file explorer
///   ----------------------
///   Create Docs Folder     creates Docs~ (with Resources/Markdown/new_file.md) and downloads the latest portal into it
///   Create Docs File Template  asks for a name, creates an empty .md template in Docs~/Resources/Markdown and opens it in your IDE
///
/// Place this file in any "Editor" folder, e.g. Assets/Editor/DocsPortalMenu.cs.
///
/// Layout (relative to the project root, i.e. the folder that contains Assets/):
///   Docs~/crawling_documentation_portal.html
/// A folder ending in "~" is ignored by Unity, so no .meta files are generated for your docs,
/// but Git still tracks it, so commit the whole Docs~ folder (including _manifest.json).
/// </summary>
public static class DocsPortalMenu
{
    const string DocsFolderName = "Docs~";
    const string PortalFileName = "crawling_documentation_portal.html";
    // "raw" URL: the github.com/.../blob/... link is an HTML page, not the file itself.
    const string PortalDownloadUrl =
        "https://raw.githubusercontent.com/CrawlingWharf90/Crawling_Documentation_Portal/main/crawling_documentation_portal.html";

    static string ProjectRoot => Directory.GetParent(UnityEngine.Application.dataPath).FullName;
    const string DefaultTemplateName = "new_file.md";

    static string DocsFolder => Path.Combine(ProjectRoot, DocsFolderName);
    static string TemplatesFolder => Path.Combine(DocsFolder, "Resources", "Markdown");
    static string PortalPath => Path.GetFullPath(Path.Combine(DocsFolder, PortalFileName));

    [MenuItem("Tools/Docs/Open Documentation", false, 1)]
    static void Open()
    {
        var path = PortalPath;
        if (!File.Exists(path))
        {
            if (EditorUtility.DisplayDialog("Docs", $"The portal was not found at:\n{path}\n\nCreate the Docs folder and download it now?", "Create", "Cancel"))
                CreateDocsFolder();
            return;
        }

        var url = new Uri(path).AbsoluteUri;

        if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor && TryLaunchChromium(url))
            return;

        UnityEngine.Application.OpenURL(url);
    }

    [MenuItem("Tools/Docs/Reveal Docs Folder", false, 2)]
    static void Reveal()
    {
        var path = PortalPath;
        if (File.Exists(path)) EditorUtility.RevealInFinder(path);
        else Debug.LogError($"[Docs] Portal not found at: {path}\nUse Tools > Docs > Create Docs Folder.");
    }

    [MenuItem("Tools/Docs/Create Docs Folder", false, 100)]
    static void CreateDocsFolder()
    {
        if (File.Exists(PortalPath) &&
            !EditorUtility.DisplayDialog("Docs", $"The portal already exists:\n{PortalPath}\n\nDownload the latest version and replace it?\n(Your pages and _manifest.json are not touched.)", "Replace", "Cancel"))
            return;

        Directory.CreateDirectory(DocsFolder);
        EnsureTemplates();

        var request = UnityWebRequest.Get(PortalDownloadUrl);
        request.timeout = 30;
        var op = request.SendWebRequest();

        void Poll()
        {
            if (!op.isDone)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Docs", "Downloading the documentation portal…", op.progress))
                    request.Abort();
                return;
            }

            EditorApplication.update -= Poll;
            EditorUtility.ClearProgressBar();
            try { OnDownloaded(request); }
            finally { request.Dispose(); }
        }

        EditorApplication.update += Poll;
    }

    [MenuItem("Tools/Docs/Create Docs File Template", false, 101)]
    static void CreateTemplate()
    {
        if (!Directory.Exists(DocsFolder))
        {
            EditorUtility.DisplayDialog("Docs", "The Docs~ folder does not exist yet.\nUse Tools > Docs > Create Docs Folder first.", "OK");
            return;
        }

        EnsureTemplates();

        // The save dialog doubles as the "name your template" prompt.
        var chosen = EditorUtility.SaveFilePanel("New docs file template", TemplatesFolder, "my_template", "md");
        if (string.IsNullOrEmpty(chosen)) return;

        var name = Path.GetFileNameWithoutExtension(chosen);
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        if (name.Length == 0) return;

        // Always created inside Resources/Markdown, whichever folder the dialog ended up in.
        var path = Path.Combine(TemplatesFolder, name + ".md");
        if (!File.Exists(path)) File.WriteAllText(path, "");

        OpenInIde(path);
    }

    // Docs~/Resources/Markdown holds the templates the portal offers under Add docs > New File.
    static void EnsureTemplates()
    {
        Directory.CreateDirectory(TemplatesFolder);
        var path = Path.Combine(TemplatesFolder, DefaultTemplateName);
        if (!File.Exists(path)) File.WriteAllText(path, "");
    }

    static void OpenInIde(string path)
    {
        // Uses the script editor chosen in Preferences > External Tools; falls back to the default app for .md.
        if (!UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, 1))
            EditorUtility.OpenWithDefaultApp(path);
    }

    static void OnDownloaded(UnityWebRequest request)
    {
#if UNITY_2020_2_OR_NEWER
        bool failed = request.result != UnityWebRequest.Result.Success;
#else
        bool failed = request.isNetworkError || request.isHttpError;
#endif
        if (failed)
        {
            Debug.LogError($"[Docs] Download failed: {request.error}\n{PortalDownloadUrl}");
            return;
        }

        var text = request.downloadHandler.text;
        if (text.Length < 1000 || text.IndexOf("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Debug.LogError($"[Docs] The downloaded file does not look like the portal. Check the URL:\n{PortalDownloadUrl}");
            return;
        }

        var temp = PortalPath + ".tmp";
        File.WriteAllBytes(temp, request.downloadHandler.data);
        if (File.Exists(PortalPath)) File.Delete(PortalPath);
        File.Move(temp, PortalPath);

        Debug.Log($"[Docs] Portal saved to {PortalPath}");
        if (EditorUtility.DisplayDialog("Docs", $"Docs folder ready:\n{DocsFolder}\n\nOpen the documentation now?", "Open", "Later"))
            Open();
    }

    static bool TryLaunchChromium(string url)
    {
        string pf = Environment.GetEnvironmentVariable("ProgramFiles");
        string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        string local = Environment.GetEnvironmentVariable("LocalAppData");

        string[] candidates =
        {
            Path.Combine(pf ?? "", @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86 ?? "", @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(local ?? "", @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86 ?? "", @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf ?? "", @"Microsoft\Edge\Application\msedge.exe"),
        };

        foreach (var exe in candidates)
        {
            if (!File.Exists(exe)) continue;
            try
            {
                Process.Start(exe, $"\"{url}\"");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Docs] Could not launch {exe}: {e.Message}");
            }
        }
        return false;
    }
}
#endif
