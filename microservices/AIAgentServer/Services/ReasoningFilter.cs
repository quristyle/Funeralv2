namespace AIAgentServer.Services;

/// <summary>답에서 <c>&lt;think&gt;…&lt;/think&gt;</c> 블록을 걷어낸다. <b>조각으로 들어와도 된다.</b> 추론(reasoning) 모델은 답 앞에 생각을 먼저 쏟아낸다.</summary>
public sealed class ReasoningFilter
{
    private const string Open = "<think>";
    private const string Close = "</think>";

    /// <summary>아직 판단이 끝나지 않아 들고 있는 글자.</summary>
    private string _pending = string.Empty;

    /// <summary>지금 생각 블록 안인지.</summary>
    private bool _inside;

    /// <summary>한 글자라도 내보낸 적이 있는지. 전부 생각이었는지 판단하는 데 쓴다.</summary>
    public bool EmittedAnything { get; private set; }

    /// <summary>조각을 넣고, <b>지금 확실히 내보낼 수 있는 부분</b>만 받는다. 내보낼 것이 없으면 빈 문자열.</summary>
    public string Feed(string chunk)
    {
        _pending += chunk;
        var output = new System.Text.StringBuilder();

        while (true)
        {
            if (_inside)
            {
                var close = IndexOfIgnoreCase(_pending, Close);
                if (close < 0)
                {
                    // 아직 안 닫혔다. 닫는 태그의 앞부분일 수 있는 꼬리만 남기고 버린다.
                    _pending = TailThatCouldStart(_pending, Close);
                    break;
                }

                _pending = _pending[(close + Close.Length)..];
                _inside = false;
                continue;
            }

            var open = IndexOfIgnoreCase(_pending, Open);
            if (open < 0)
            {
                // 여는 태그의 앞부분일 수 있는 꼬리는 들고 있는다. 나머지는 답이다.
                var keep = TailThatCouldStart(_pending, Open);
                var emit = _pending[..(_pending.Length - keep.Length)];
                if (emit.Length > 0) output.Append(emit);
                _pending = keep;
                break;
            }

            if (open > 0) output.Append(_pending[..open]);
            _pending = _pending[(open + Open.Length)..];
            _inside = true;
        }

        var result = output.ToString();
        if (result.Length > 0) EmittedAnything = true;
        return result;
    }

    /// <summary>스트림이 끝났다. 들고 있던 것 중 <b>답인 것만</b> 내보낸다. 생각 블록 안에서 끝났으면 아무것도 내보내지 않는다(잘린 독백).</summary>
    public string Flush()
    {
        if (_inside)
        {
            _pending = string.Empty;
            return string.Empty;
        }

        var rest = _pending;
        _pending = string.Empty;
        if (rest.Length > 0) EmittedAnything = true;
        return rest;
    }

    private static int IndexOfIgnoreCase(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="text"/> 의 끝부분이 <paramref name="tag"/> 의 시작과 겹치면
    /// 그 겹치는 꼬리를 돌려준다. 없으면 빈 문자열.
    /// 예: 태그가 &lt;think&gt; 이고 글이 답변 &lt;thi 로 끝나면 &lt;thi 를 돌려준다.
    /// </summary>
    private static string TailThatCouldStart(string text, string tag)
    {
        var max = Math.Min(tag.Length - 1, text.Length);

        for (var len = max; len > 0; len--)
        {
            if (string.Equals(text[^len..], tag[..len], StringComparison.OrdinalIgnoreCase))
            {
                return text[^len..];
            }
        }

        return string.Empty;
    }
}
