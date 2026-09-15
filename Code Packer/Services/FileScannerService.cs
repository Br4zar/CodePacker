using System.Collections.Generic;
using System;
using System.IO;
using System.Threading.Tasks;
using CodePacker.Models;

namespace CodePacker.Services;

public class FileScannerService
{
    private static readonly HashSet<string> DefaultCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".csproj", ".sln", ".js", ".jsx", ".ts", ".tsx", ".py", ".java",
        ".c", ".cpp", ".h", ".hpp", ".rs", ".go", ".rb", ".php", ".swift", ".kt",
        ".html", ".css", ".scss", ".json", ".xml", ".yaml", ".yml", ".md", ".txt",
        ".sh", ".bash", ".ps1", ".bat", ".cmd", ".sql", ".graphql", ".proto", ".vue", ".svelte"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico"
    };

    private static readonly HashSet<string> DefaultIgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".git", ".github", "node_modules", ".next", "dist", "build",
        "__pycache__", ".venv", "venv", ".idea", ".vscode", "coverage", "target"
    };

    private static readonly HashSet<string> DefaultIgnoredFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "Cargo.lock",
        ".DS_Store", "Thumbs.db", "desktop.ini"
    };

    public async Task<List<FileItem>> ScanDirectoryAsync(
        string rootPath,
        string customExtsRaw,
        string customIgnoreRaw,
        IProgress<string>? progress = null)
    {
        var result = new List<FileItem>();
        if (!Directory.Exists(rootPath)) return result;

        var customExts = ParseExtensions(customExtsRaw);
        var customIgnores = ParseIgnores(customIgnoreRaw);

        await Task.Run(() =>
        {
            var dirInfo = new DirectoryInfo(rootPath);
            ScanRecursive(dirInfo, rootPath, customExts, customIgnores, result, progress);
        });

        return result;
    }

    public async Task<FileItem?> ProcessSingleFileAsync(string filePath, string rootPath, string customExtsRaw, string customIgnoreRaw)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists) return null;

        var ext = fileInfo.Extension;
        var customExts = ParseExtensions(customExtsRaw);
        var isCode = DefaultCodeExtensions.Contains(ext) || customExts.Contains(ext);
        var isImg = ImageExtensions.Contains(ext);

        if (!isCode && !isImg) return null;

        var relPath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
        var item = new FileItem
        {
            FullPath = filePath,
            RelativePath = relPath,
            SizeBytes = fileInfo.Length,
            IsImage = isImg
        };

        if (isImg)
        {
            try
            {
                item.ImageData = await File.ReadAllBytesAsync(filePath);
                using var ms = new MemoryStream(item.ImageData);
                using var bmp = new System.Drawing.Bitmap(ms);
                item.ImageWidth = bmp.Width;
                item.ImageHeight = bmp.Height;
                item.RawContent = $"[IMAGE: {bmp.Width}x{bmp.Height}]";
            }
            catch { return null; }
        }
        else
        {
            try
            {
                item.RawContent = await File.ReadAllTextAsync(filePath);
            }
            catch { return null; }
        }

        return item;
    }

    private void ScanRecursive(
        DirectoryInfo currentDir,
        string rootPath,
        HashSet<string> customExts,
        List<string> customIgnores,
        List<FileItem> results,
        IProgress<string>? progress)
    {
        if (DefaultIgnoredFolders.Contains(currentDir.Name) || MatchesAnyIgnore(currentDir.Name, customIgnores))
            return;

        progress?.Report($"Сканирование: {currentDir.Name}");

        FileInfo[] files;
        try { files = currentDir.GetFiles(); }
        catch { return; }

        foreach (var file in files)
        {
            if (DefaultIgnoredFiles.Contains(file.Name) || MatchesAnyIgnore(file.Name, customIgnores))
                continue;

            var ext = file.Extension;
            bool isCode = DefaultCodeExtensions.Contains(ext) || customExts.Contains(ext);
            bool isImg = ImageExtensions.Contains(ext);

            if (!isCode && !isImg) continue;

            var relPath = Path.GetRelativePath(rootPath, file.FullName).Replace('\\', '/');
            if (MatchesAnyIgnore(relPath, customIgnores)) continue;

            var item = new FileItem
            {
                FullPath = file.FullName,
                RelativePath = relPath,
                SizeBytes = file.Length,
                IsImage = isImg
            };

            if (isImg)
            {
                try
                {
                    item.ImageData = File.ReadAllBytes(file.FullName);
                    using var ms = new MemoryStream(item.ImageData);
                    using var bmp = new System.Drawing.Bitmap(ms);
                    item.ImageWidth = bmp.Width;
                    item.ImageHeight = bmp.Height;
                    item.RawContent = $"[IMAGE: {bmp.Width}x{bmp.Height}]";
                }
                catch { continue; }
            }
            else
            {
                try
                {
                    item.RawContent = File.ReadAllText(file.FullName);
                }
                catch { continue; }
            }

            results.Add(item);
        }

        DirectoryInfo[] subDirs;
        try { subDirs = currentDir.GetDirectories(); }
        catch { return; }

        foreach (var sub in subDirs)
        {
            ScanRecursive(sub, rootPath, customExts, customIgnores, results, progress);
        }
    }

    private HashSet<string> ParseExtensions(string raw)
    {
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(x => x.Trim())
                  .Select(x => x.StartsWith('.') ? x : "." + x)
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private List<string> ParseIgnores(string raw)
    {
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(x => x.Trim())
                  .Where(x => !string.IsNullOrEmpty(x))
                  .ToList();
    }

    private bool MatchesAnyIgnore(string nameOrPath, List<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.StartsWith("*."))
            {
                var ext = pattern[1..];
                if (nameOrPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (nameOrPath.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                     nameOrPath.Contains("/" + pattern + "/", StringComparison.OrdinalIgnoreCase) ||
                     nameOrPath.StartsWith(pattern + "/", StringComparison.OrdinalIgnoreCase) ||
                     nameOrPath.EndsWith("/" + pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}