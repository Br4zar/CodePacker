using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodePacker.Services;

public class CodeProcessorService
{
    public string ProcessCode(string content, string filePath, bool removeWhitespace, bool removeComments, bool smartJson)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (smartJson && ext == ".json")
        {
            try
            {
                if (filePath.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))
                {
                    var node = JsonNode.Parse(content);
                    if (node is JsonObject obj)
                    {
                        var slim = new JsonObject();
                        string[] keepKeys = ["name", "version", "scripts", "dependencies", "devDependencies"];
                        foreach (var key in keepKeys)
                        {
                            if (obj.ContainsKey(key))
                                slim[key] = obj[key]?.DeepClone();
                        }
                        return slim.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                    }
                }

                var doc = JsonDocument.Parse(content);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
            }
            catch { /* Невалидный json — продолжаем стандартную обработку */ }
        }

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();
        bool prevEmpty = false;

        foreach (var rawLine in lines)
        {
            var line = removeWhitespace ? rawLine.TrimEnd() : rawLine;
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (removeWhitespace && prevEmpty) continue;
                sb.AppendLine();
                prevEmpty = true;
                continue;
            }

            prevEmpty = false;

            if (removeComments && IsCommentLine(trimmed, ext))
                continue;

            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }

    private bool IsCommentLine(string trimmed, string ext)
    {
        // Не удаляем директивы препроцессора и важные метаданные
        if (trimmed.StartsWith("#include") || trimmed.StartsWith("#define") ||
            trimmed.StartsWith("#pragma") || trimmed.StartsWith("#region") ||
            trimmed.StartsWith("#!") || trimmed.StartsWith("# [") || ext == ".md")
        {
            return false;
        }

        if (trimmed.StartsWith("//")) return true;
        if (trimmed.StartsWith("--")) return true;
        if (trimmed.StartsWith("/*") && trimmed.EndsWith("*/")) return true;
        if (trimmed.StartsWith("<!--") && trimmed.EndsWith("-->")) return true;
        if (trimmed.StartsWith("#") && (ext == ".py" || ext == ".sh" || ext == ".yaml" || ext == ".yml" || ext == ".rb")) return true;

        return false;
    }
}