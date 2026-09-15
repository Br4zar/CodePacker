using System.Collections.Generic;

namespace CodePacker.Models;

public enum HintType { Info, Warning }

public class OptimizationHint
{
    public HintType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> AffectedFileIds { get; set; } = new();
}