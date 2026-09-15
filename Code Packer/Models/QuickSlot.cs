using System;

namespace CodePacker.Models;

public class QuickSlot
{
    public int SlotIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public DateTime LastUsed { get; set; } = DateTime.Now;
}