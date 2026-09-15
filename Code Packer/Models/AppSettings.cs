using System.Collections.Generic;

namespace CodePacker.Models;

public class AppSettings
{
    public string CustomExtensions { get; set; } = ".xaml, .csproj, .sln, .props, .targets, .env, .config";
    public string CustomIgnorePatterns { get; set; } = "bin, obj, .vs, .git, node_modules, dist, build, logs, *.log";
    public bool RemoveWhitespace { get; set; } = true;
    public bool RemoveComments { get; set; } = false;
    public bool MinimalHeader { get; set; } = true;
    public bool SmartJsonMinify { get; set; } = true;
    public bool IncludeImagesInPdf { get; set; } = true;
    public int PdfFontSize { get; set; } = 8;
    public string SelectedPreset { get; set; } = "Claude (200k)";
    public List<QuickSlot> QuickSlots { get; set; } = new();
    public List<string> RecentFolders { get; set; } = new();
}