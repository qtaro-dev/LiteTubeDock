using System.IO;
using LiteTubeDock.Constants;

namespace LiteTubeDock.Services;

public static class AppPathService
{
    public static string GetProjectRootPath()
    {
        return FindProjectRoot(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
    }

    public static string GetDataDirectoryPath()
    {
        return Path.Combine(GetProjectRootPath(), AppConstants.DataDirectoryName);
    }

    public static string GetIconsDirectoryPath()
    {
        return Path.Combine(GetDataDirectoryPath(), AppConstants.IconsDirectoryName);
    }

    public static string GetDefaultSettingsExportDirectoryPath()
    {
        return Path.Combine(GetProjectRootPath(), AppConstants.SettingsExportDirectoryName);
    }

    public static string ResolveSettingsExportFolder(string? path)
    {
        var candidate = string.IsNullOrWhiteSpace(path)
            ? AppConstants.DefaultSettingsExportFolder
            : path.Trim();

        return Path.GetFullPath(Path.IsPathRooted(candidate)
            ? candidate
            : Path.Combine(GetProjectRootPath(), candidate));
    }

    public static bool TryResolveProjectPath(string? path, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var root = GetProjectRootPath();
        var candidate = Path.IsPathRooted(path)
            ? path
            : Path.Combine(root, path);

        var fullRoot = Path.GetFullPath(root);
        var fullCandidate = Path.GetFullPath(candidate);
        var relativePath = Path.GetRelativePath(fullRoot, fullCandidate);
        var isOutsideProject = relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath);

        if (isOutsideProject)
        {
            return false;
        }

        fullPath = fullCandidate;
        return true;
    }

    public static string ResolveProjectPath(string path)
    {
        var root = GetProjectRootPath();
        var candidate = Path.IsPathRooted(path)
            ? path
            : Path.Combine(root, path);

        var fullRoot = Path.GetFullPath(root);
        var fullCandidate = Path.GetFullPath(candidate);
        var relativePath = Path.GetRelativePath(fullRoot, fullCandidate);
        var isOutsideProject = relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath);

        return isOutsideProject
            ? Path.Combine(fullRoot, AppConstants.DefaultWebView2UserDataFolder)
            : fullCandidate;
    }

    private static string? FindProjectRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory is not null)
        {
            var projectPath = Path.Combine(directory.FullName, AppConstants.ProjectFileName);
            if (File.Exists(projectPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
