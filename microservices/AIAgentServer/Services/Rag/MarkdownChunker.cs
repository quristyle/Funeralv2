using System.Text;

namespace AIAgentServer.Services.Rag;

/// <summary>마크다운을 <b>제목 단위</b>로 자른다. 글자 수로 기계적으로 끊으면 표가 반으로 갈리고 코드 블록이 열린 채로 끝난다.</summary>
public static class MarkdownChunker
{
    /// <summary>이보다 길면 문단 경계에서 한 번 더 자른다.</summary>
    private const int MaxChars = 1600;

    /// <summary>이보다 짧은 토막은 앞 토막에 붙인다. 제목만 있는 줄이 혼자 남지 않게.</summary>
    private const int MinChars = 120;

    public static List<RagChunk> Split(string source, string markdown)
    {
        var chunks = new List<RagChunk>();
        var path = new List<string>();
        var body = new StringBuilder();
        var headingAtStart = string.Empty;

        void Flush()
        {
            var text = body.ToString().Trim();
            body.Clear();
            if (text.Length == 0) return;

            foreach (var piece in SplitLong(text))
            {
                chunks.Add(new RagChunk
                {
                    Id = $"{source}#{chunks.Count}",
                    Source = source,
                    Heading = headingAtStart,
                    Text = piece,
                });
            }
        }

        var inFence = false;
        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            // 코드 울타리 안의 `#` 는 제목이 아니라 주석이다.
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                body.AppendLine(line);
                continue;
            }

            var level = inFence ? 0 : HeadingLevel(line);
            if (level == 0)
            {
                body.AppendLine(line);
                continue;
            }

            Flush();

            while (path.Count >= level) path.RemoveAt(path.Count - 1);
            while (path.Count < level - 1) path.Add(string.Empty);
            path.Add(line[level..].Trim());

            headingAtStart = string.Join(" > ", path.Where(x => x.Length > 0));
        }

        Flush();

        return Merge(chunks);
    }

    private static int HeadingLevel(string line)
    {
        var n = 0;
        while (n < line.Length && line[n] == '#') n++;
        return n is >= 1 and <= 6 && n < line.Length && line[n] == ' ' ? n : 0;
    }

    /// <summary>너무 긴 토막을 빈 줄(문단) 경계에서 자른다.</summary>
    private static IEnumerable<string> SplitLong(string text)
    {
        if (text.Length <= MaxChars)
        {
            yield return text;
            yield break;
        }

        var buf = new StringBuilder();
        foreach (var para in text.Split("\n\n"))
        {
            if (buf.Length > 0 && buf.Length + para.Length > MaxChars)
            {
                yield return buf.ToString().Trim();
                buf.Clear();
            }

            // 문단 하나가 상한보다 길면(긴 표·코드 블록) 글자 수로라도 끊는다.
            // 안 끊으면 임베딩 서버가 문맥 길이 초과로 거절한다.
            if (para.Length > MaxChars)
            {
                for (var off = 0; off < para.Length; off += MaxChars)
                {
                    yield return para.Substring(off, Math.Min(MaxChars, para.Length - off)).Trim();
                }

                continue;
            }

            buf.Append(para).Append("\n\n");
        }

        if (buf.ToString().Trim() is { Length: > 0 } tail) yield return tail;
    }

    /// <summary>짧은 토막을 앞에 붙인다. 제목 줄만 남은 토막을 없앤다.</summary>
    private static List<RagChunk> Merge(List<RagChunk> src)
    {
        var outp = new List<RagChunk>();
        foreach (var c in src)
        {
            if (outp.Count > 0
                && c.Text.Length < MinChars
                && outp[^1].Source == c.Source
                && outp[^1].Text.Length + c.Text.Length < MaxChars)
            {
                var prev = outp[^1];
                outp[^1] = new RagChunk
                {
                    Id = prev.Id,
                    Source = prev.Source,
                    Heading = prev.Heading,
                    Text = prev.Text + "\n\n" + (c.Heading.Length > 0 ? c.Heading + "\n" : "") + c.Text,
                };
                continue;
            }

            outp.Add(c);
        }

        return outp;
    }

    /// <summary>임베딩에 넣을 글. 제목 경로를 앞에 붙인 것이다.</summary>
    public static string ForEmbedding(RagChunk c)
        => c.Heading.Length > 0 ? $"{c.Source}\n{c.Heading}\n\n{c.Text}" : $"{c.Source}\n\n{c.Text}";
}
