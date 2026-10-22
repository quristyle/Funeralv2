using System.Text;
using System.Text.Json;
using HtmlAgilityPack;
using UglyToad.PdfPig;

namespace AIAgentServer.Services.Rag;

/// <summary>올려 둔 파일에서 글자를 뽑는다. 못 읽는 형식을 조용히 넘기지 않는다.</summary>
public static class DocumentText
{
    /// <summary>글자를 바로 읽는 형식.</summary>
    private static readonly HashSet<string> Plain = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".text", ".csv", ".tsv", ".json", ".yaml", ".yml",
        ".log", ".sql", ".cs", ".ts", ".js", ".py", ".sh", ".xml", ".ini", ".conf",
    };

    public static bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return Plain.Contains(ext)
               || ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
               || ext is ".html" or ".htm";
    }

    public static string Describe() =>
        "글자 파일(.md .txt .csv .json .yaml .log .sql 등) · .pdf · .html";

    /// <summary>글자를 뽑는다. 못 뽑으면 <paramref name="reason"/> 에 까닭이 담기고 빈 글을 돌려준다.</summary>
    public static string Extract(string path, string fileName, out string? reason)
    {
        reason = null;
        var ext = Path.GetExtension(fileName);

        try
        {
            if (Plain.Contains(ext)) return File.ReadAllText(path);

            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new StringBuilder();
                using var doc = PdfDocument.Open(path);
                foreach (var page in doc.GetPages())
                {
                    sb.AppendLine(page.Text);
                    sb.AppendLine();
                }

                var text = sb.ToString().Trim();
                if (text.Length == 0)
                {
                    // 스캔한 PDF 는 그림이라 글자가 없다. OCR 이 있어야 읽는다.
                    reason = "PDF 에서 글자를 찾지 못했습니다. 스캔본이면 OCR 이 필요합니다.";
                }

                return text;
            }

            if (ext is ".html" or ".htm")
            {
                var d = new HtmlDocument();
                d.Load(path);

                // 표 본문을 JSON 으로 싣고 화면에서 그리는 문서가 있다. 그것까지 버리면 머리만 남는다.
                var data = new StringBuilder();
                foreach (var tag in new[] { "script", "style" })
                {
                    var ns = d.DocumentNode.SelectNodes($"//{tag}");
                    if (ns is null) continue;
                    foreach (var n in ns.ToList())
                    {
                        if (tag == "script"
                            && n.GetAttributeValue("type", string.Empty).Contains("json", StringComparison.OrdinalIgnoreCase))
                        {
                            AppendJson(data, n.InnerText);
                        }

                        n.Remove();
                    }
                }

                var body = System.Net.WebUtility.HtmlDecode(d.DocumentNode.InnerText ?? string.Empty).Trim();
                return data.Length == 0 ? body : $"{body}\n\n{data.ToString().Trim()}";
            }

            reason = $"읽을 수 없는 형식입니다({ext}). 쓸 수 있는 것: {Describe()}";
            return string.Empty;
        }
        catch (Exception ex)
        {
            reason = $"파일을 읽지 못했습니다: {ex.Message}";
            return string.Empty;
        }
    }

    /// <summary>JSON 을 「열쇠: 값」 줄로 푼다. 배열의 한 항목이 한 문단이라 토막이 항목 경계에서 갈린다.</summary>
    private static void AppendJson(StringBuilder sb, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            Walk(sb, doc.RootElement, string.Empty);
        }
        catch (JsonException)
        {
        }
    }

    private static void Walk(StringBuilder sb, JsonElement e, string key)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject()) Walk(sb, p.Value, p.Name);
                break;
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray())
                {
                    Walk(sb, item, key);
                    if (item.ValueKind == JsonValueKind.Object) sb.AppendLine();
                }

                break;
            case JsonValueKind.String:
                var v = e.GetString();
                if (!string.IsNullOrWhiteSpace(v)) sb.AppendLine(key.Length > 0 ? $"{key}: {v}" : v);
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.AppendLine(key.Length > 0 ? $"{key}: {e.GetRawText()}" : e.GetRawText());
                break;
        }
    }
}
