#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Adds a Tools > Docs menu to the Unity Editor:
///   Open Documentation          opens the portal in the browser
///   Reveal Docs Folder          shows the portal file in the file explorer
///   ----------------------
///   Create Docs Folder          creates Docs~ (with Resources/Markdown/new_file.md) and copies the portal from the package
///   Create Docs File Template   asks for a name, creates an empty .md template in Docs~/Resources/Markdown and opens it in your IDE
/// </summary>
public static class DocsPortalMenu
{
    const string DocsFolderName = "Docs~";
    const string PackageHiddenFolder = "Portal~";
    const string PortalFileName = "crawling_documentation_portal.html";
    const string DefaultTemplateName = "new_file.md";

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string DocsFolder => Path.Combine(ProjectRoot, DocsFolderName);
    static string TemplatesFolder => Path.Combine(DocsFolder, "Resources", "Markdown");
    static string PortalPath => Path.GetFullPath(Path.Combine(DocsFolder, PortalFileName));

    // Automatically keeps Docs~/crawling_documentation_portal.html in sync whenever the UPM package updates
    [InitializeOnLoadMethod]
    static void AutoSyncPortalWithPackage()
    {
        if (!File.Exists(PortalPath)) return;

        string sourcePath = GetPackageHtmlPath();
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return;

        try
        {
            string packageHtml = File.ReadAllText(sourcePath);
            string localHtml = File.ReadAllText(PortalPath);
            if (!string.Equals(packageHtml, localHtml, StringComparison.Ordinal))
            {
                File.Copy(sourcePath, PortalPath, true);
                Debug.Log($"[Docs] Updated {PortalFileName} in {DocsFolderName} to match the installed UPM package.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Docs] Could not auto-sync portal HTML: {e.Message}");
        }
    }

    static string GetPackageHtmlPath()
    {
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DocsPortalMenu).Assembly);
        if (packageInfo == null) return null;

        // Prefer the hidden Portal~ folder so Unity never requires a .meta file for the HTML
        string hiddenPath = Path.Combine(packageInfo.resolvedPath, PackageHiddenFolder, PortalFileName);
        if (File.Exists(hiddenPath)) return hiddenPath;

        // Fallback to the package root if placed directly in UNITY/
        return Path.Combine(packageInfo.resolvedPath, PortalFileName);
    }

    [MenuItem("Tools/Docs/Open Documentation", false, 1)]
    static void Open()
    {
        var path = PortalPath;
        if (!File.Exists(path))
        {
            if (EditorUtility.DisplayDialog("Docs", $"The portal was not found at:\n{path}\n\nCreate the Docs folder now?", "Create", "Cancel"))
                CreateDocsFolder();
            return;
        }

        var url = new Uri(path).AbsoluteUri;

        if (Application.platform == RuntimePlatform.WindowsEditor && TryLaunchChromium(url))
            return;

        Application.OpenURL(url);
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
            !EditorUtility.DisplayDialog("Docs", $"The portal already exists:\n{PortalPath}\n\nReplace it with the version from the package?\n(Your pages and _manifest.json are not touched.)", "Replace", "Cancel"))
            return;

        string sourceHtmlPath = GetPackageHtmlPath();
        if (string.IsNullOrEmpty(sourceHtmlPath) || !File.Exists(sourceHtmlPath))
        {
            Debug.LogError($"[Docs] Source portal file not found inside the UPM package. Expected at: UNITY/{PackageHiddenFolder}/{PortalFileName}");
            return;
        }

        Directory.CreateDirectory(DocsFolder);
        EnsureTemplates();

        File.Copy(sourceHtmlPath, PortalPath, true);
        Debug.Log($"[Docs] Portal initialized at {PortalPath}");

        if (EditorUtility.DisplayDialog("Docs", $"Docs folder ready:\n{DocsFolder}\n\nOpen the documentation now?", "Open", "Later"))
            Open();
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

        var chosen = EditorUtility.SaveFilePanel("New docs file template", TemplatesFolder, "my_template", "md");
        if (string.IsNullOrEmpty(chosen)) return;

        var name = Path.GetFileNameWithoutExtension(chosen);
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        if (name.Length == 0) return;

        var path = Path.Combine(TemplatesFolder, name + ".md");
        if (!File.Exists(path)) File.WriteAllText(path, "");

        OpenInIde(path);
    }

    static void EnsureTemplates()
    {
        Directory.CreateDirectory(TemplatesFolder);
        var path = Path.Combine(TemplatesFolder, DefaultTemplateName);
        if (!File.Exists(path)) File.WriteAllText(path, "");
    }

    static void OpenInIde(string path)
    {
        if (!UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, 1))
            EditorUtility.OpenWithDefaultApp(path);
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