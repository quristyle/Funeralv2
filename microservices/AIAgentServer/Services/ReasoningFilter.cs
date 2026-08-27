namespace AIAgentServer.Services;

/// <summary>
/// 답에서 <c>&lt;think&gt;…&lt;/think&gt;</c> 블록을 걷어낸다. <b>조각으로 들어와도 된다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 필요한가.</b> 추론(reasoning) 모델은 답 앞에 생각을 먼저 쏟아낸다. 그것을 그대로
/// 흘려보내면 두 가지가 나빠진다.
/// </para>
/// <list type="number">
///   <item>사용자가 "Okay, the user is asking…" 같은 영어 독백을 답으로 읽는다.</item>
///   <item>
///     <b>그 독백이 대화 기록에 저장되고 다음 턴에 다시 올라간다.</b> 화면이 받은 것을
///     그대로 기록에 쌓기 때문이다. 생각은 보통 답보다 길어서, 턴이 갈수록
///     <b>쓸모없는 글자가 문맥의 대부분</b>을 차지하고 무료 한도를 태운다.
///   </item>
/// </list>
///
/// <para>
/// <b>왜 서버에서 하는가.</b> 걸러 낸 것이 화면에 안 보이면서 <b>동시에</b> 기록에도
/// 안 남아야 한다. 화면은 받은 조각을 그대로 기록에 쌓으므로, 서버에서 빼면 두 곳이
/// 한 번에 해결된다. 브라우저에서 빼면 같은 상태 기계를 TS 로 한 번 더 써야 한다.
/// </para>
///
/// <para>
/// <b>어려운 점은 태그가 잘려서 온다는 것이다.</b> 스트리밍은 한 글자씩 오기도 해서
/// <c>&lt;thi</c> / <c>nk&gt;</c> 로 나뉜다. 그래서 <b>태그의 앞부분일 수 있는 꼬리는
/// 내보내지 않고 들고 있는다.</b> 뒤 글자가 와서 태그가 아니라고 밝혀지면 그때 내보낸다.
/// </para>
///
/// <para>
/// <b>닫히지 않은 생각은 뒤 전부를 버린다.</b> <c>max_tokens</c> 가 모자라 생각이 잘린
/// 경우인데, 그 뒤에 남은 것은 답이 아니라 잘린 독백의 조각이다.
/// (같은 판단을 한 줄 추천 쪽에서도 한다 — <c>LLMService.StripReasoning</c>.)
/// </para>
///
/// <para>
/// <b>이것으로 모든 추론 모델이 걸러지지는 않는다.</b> 태그를 쓰지 않고 응답의
/// <c>delta.reasoning</c> 같은 별도 항목으로 주는 공급자도 있다. 그쪽은 우리가
/// <c>delta.content</c> 만 읽으므로 애초에 들어오지 않는다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 조각을 넣고, <b>지금 확실히 내보낼 수 있는 부분</b>만 받는다.
    /// 내보낼 것이 없으면 빈 문자열.
    /// </summary>
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

    /// <summary>
    /// 스트림이 끝났다. 들고 있던 것 중 <b>답인 것만</b> 내보낸다.
    /// </summary>
    /// <remarks>
    /// 생각 블록 안에서 끝났으면 아무것도 내보내지 않는다(잘린 독백).
    /// 밖이었다면 들고 있던 꼬리는 태그가 되지 못한 <b>평범한 글자</b>이므로 내보낸다 —
    /// 답이 <c>&lt;</c> 로 끝나는 경우가 실제로 있다.
    /// </remarks>
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
    /// </summary>
    /// <remarks>
    /// 예: 태그가 <c>&lt;think&gt;</c> 이고 글이 <c>답변 &lt;thi</c> 로 끝나면 <c>&lt;thi</c> 를
    /// 돌려준다. 이 꼬리는 아직 내보내면 안 된다 — 다음 조각에서 태그가 완성될 수 있다.
    /// </remarks>
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
