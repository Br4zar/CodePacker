using System.Collections.Generic;
using System;
using CodePacker.Models;

namespace CodePacker.Services;

public class TokenEstimatorService
{
    public static readonly Dictionary<string, int> Presets = new()
    {
        ["Claude (200k)"] = 180_000,
        ["GPT-4o (128k)"] = 115_000,
        ["Gemini 1.5 Pro (1M)"] = 900_000,
        ["DeepSeek V3 (64k)"] = 60_000,
        ["Local LLM (32k)"] = 28_000
    };

    public int EstimateTextTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 3.5);
    }

    public int EstimateImageTokens(int width, int height)
    {
        if (width <= 0 || height <= 0) return 0;
        int tilesX = (int)Math.Ceiling(width / 512.0);
        int tilesY = (int)Math.Ceiling(height / 512.0);
        return 85 + (tilesX * tilesY * 170);
    }

    public List<OptimizationHint> GenerateHints(IEnumerable<FileItem> files)
    {
        var hints = new List<OptimizationHint>();
        var selected = files.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return hints;

        long totalSize = selected.Sum(f => f.SizeBytes);

        var locks = selected.Where(f => f.FileName.EndsWith(".lock") || f.FileName.Contains("-lock.")).ToList();
        if (locks.Count > 0)
        {
            hints.Add(new OptimizationHint
            {
                Type = HintType.Warning,
                Message = $"Lock-файлы тратят много токенов: {string.Join(", ", locks.Select(l => l.FileName))}",
                AffectedFileIds = locks.Select(l => l.Id).ToList()
            });
        }

        foreach (var file in selected.OrderByDescending(f => f.SizeBytes).Take(2))
        {
            double pct = totalSize > 0 ? (file.SizeBytes * 100.0 / totalSize) : 0;
            if (pct > 20)
            {
                hints.Add(new OptimizationHint
                {
                    Type = HintType.Info,
                    Message = $"Файл «{file.FileName}» занимает {pct:F0}% всего пакета ({FormatSize(file.SizeBytes)})",
                    AffectedFileIds = new List<string> { file.Id }
                });
            }
        }

        return hints;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    public static string FormatTokens(int tokens)
    {
        if (tokens < 1000) return tokens.ToString();
        return $"{tokens / 1000.0:F1}k";
    }
}