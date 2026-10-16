using System.Text;
using HtmlAgilityPack;
using UglyToad.PdfPig;

namespace AIAgentServer.Services.Rag;

/// <summary>올려 둔 파일에서 글자를 뽑는다.</summary>
/// <remarks>
/// <para>
/// <b>못 읽는 형식을 조용히 넘기지 않는다.</b> 색인에 안 들어간 문서가 있는데
/// 화면에 아무 표시가 없으면, 사람은 올렸으니 찾아질 것으로 믿고 나중에
/// "분명히 올렸는데 못 찾는다" 를 겪는다. 그래서 까닭을 글로 돌려주고
/// 문서 상태를 <c>skipped</c> 로 남긴다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 글자를 뽑는다. 못 뽑으면 <paramref name="reason"/> 에 까닭이 담기고
    /// 빈 글을 돌려준다.
    /// </summary>
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
                foreach (var tag in new[] { "script", "style" })
                {
                    var ns = d.DocumentNode.SelectNodes($"//{tag}");
                    if (ns is null) continue;
                    foreach (var n in ns.ToList()) n.Remove();
                }

                return System.Net.WebUtility.HtmlDecode(d.DocumentNode.InnerText ?? string.Empty).Trim();
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
}
