using System;

namespace CodePacker.Models;

public class FileItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(RelativePath);
    public long SizeBytes { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsImage { get; set; }
    public string RawContent { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public byte[]? ImageData { get; set; }
}