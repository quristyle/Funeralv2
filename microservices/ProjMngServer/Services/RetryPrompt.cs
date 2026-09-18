using System.Text;

namespace ProjMngServer.Services;

/// <summary>
/// 다시 시도할 때 <b>지난번에 왜 실패했는지</b>를 지시문 앞에 붙인다.
/// </summary>
/// <remarks>
/// <para>
/// 붙이지 않으면 재시도는 <b>같은 지시문을 같은 자리에서 다시 읽는 것</b>뿐이라,
/// 대개 같은 자리에서 같은 이유로 또 실패한다. 재시도의 값은 「한 번 더」가
/// 아니라 <b>「이번엔 무엇을 피해야 하는지 알고」</b> 도는 데 있다.
/// </para>
///
/// <para>
/// <b>본래 지시문은 손대지 않는다.</b> 실패 이야기를 위에 얹고 본문은 그대로
/// 둔다 — 본문을 고쳐 넣으면 사람이 화면에서 읽는 글과 실제로 돈 글이 달라져,
/// 「내가 시킨 것과 다르게 했다」를 확인할 길이 없어진다.
/// </para>
///
/// <para>
/// <b>길이를 자른다.</b> 결과문은 CLI 가 쏟아 낸 글이라 수십 KB 가 되기도
/// 한다. 그대로 실으면 프롬프트가 본문보다 실패 이야기로 가득 차고,
/// antigravity 처럼 인자로 넘기는 어댑터에서는 길이 제한에 걸린다.
/// 끝부분을 남기는 이유는 <b>오류는 대개 마지막에 찍히기</b> 때문이다.
/// </para>
/// </remarks>
public static class RetryPrompt
{
    /// <summary>결과문에서 남길 꼬리 길이.</summary>
    private const int TailChars = 4000;

    /// <summary>오류 요약에서 남길 길이. 이쪽은 이미 요약이라 짧다.</summary>
    private const int ErrorChars = 2000;

    /// <summary>
    /// 지난 시도를 얹은 지시문을 만든다. 얹을 것이 없으면 <paramref name="contents"/>
    /// 를 그대로 돌려준다 — 첫 시도의 프롬프트는 예전과 한 글자도 다르지 않다.
    /// </summary>
    /// <param name="contents">사람이 쓴 본래 지시문.</param>
    /// <param name="attempt">이번이 몇 번째 시도인가(1부터).</param>
    /// <param name="attemptMax">최대 몇 번까지.</param>
    /// <param name="last">바로 앞 실행. 없으면 얹지 않는다.</param>
    public static string Compose(string? contents, int attempt, int attemptMax, Previous? last)
    {
        var body = contents ?? string.Empty;

        if (attempt <= 1 || last is null)
        {
            return body;
        }

        var sb = new StringBuilder();

        sb.AppendLine($"# 앞선 시도가 실패했다 ({attempt}/{attemptMax}번째 시도)");
        sb.AppendLine();
        sb.AppendLine($"직전 실행(#{last.Seq})이 `{last.Status}` 로 끝났다"
                      + (last.ExitCode is { } code ? $" (종료 코드 {code})." : "."));
        sb.AppendLine();

        if (Trim(last.Error, ErrorChars) is { Length: > 0 } error)
        {
            sb.AppendLine("## 그때 남긴 오류");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(error);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (Tail(last.Result, TailChars) is { Length: > 0 } tail)
        {
            sb.AppendLine("## 그때 남긴 결과 (끝부분)");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(tail);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("## 이번에 할 일");
        sb.AppendLine();
        sb.AppendLine("**같은 방법을 그대로 되풀이하지 마라.** 위 실패를 먼저 읽고");
        sb.AppendLine("무엇이 원인이었는지 판단한 뒤, 그 원인을 고치고 나서 아래 본래");
        sb.AppendLine("지시를 수행하라. 원인이 분명하지 않으면 **먼저 원인을 찾는 일부터**");
        sb.AppendLine("하라 — 확인 없이 다시 같은 순서로 진행하면 같은 자리에서 또 멈춘다.");
        sb.AppendLine();
        sb.AppendLine("작업 자리는 새로 준비된 곳이다. 지난 시도가 남긴 파일은 없다고 보고");
        sb.AppendLine("처음부터 수행하되, 위에서 읽은 실패만 피하면 된다.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# 본래 지시");
        sb.AppendLine();
        sb.Append(body);

        return sb.ToString();
    }

    /// <summary>앞에서부터 자른다. 오류 요약처럼 <b>첫 줄이 중요한</b> 글에 쓴다.</summary>
    private static string Trim(string? text, int max)
    {
        var t = (text ?? string.Empty).Trim();

        return t.Length <= max ? t : t[..max] + "\n… (줄임)";
    }

    /// <summary>뒤에서부터 남긴다. 로그처럼 <b>마지막이 중요한</b> 글에 쓴다.</summary>
    private static string Tail(string? text, int max)
    {
        var t = (text ?? string.Empty).Trim();

        return t.Length <= max ? t : "(앞부분 줄임) …\n" + t[^max..];
    }

    /// <summary>지시문에 얹을 지난 실행의 자취.</summary>
    public sealed class Previous
    {
        public int Seq { get; set; }

        public string Status { get; set; } = string.Empty;

        public int? ExitCode { get; set; }

        public string? Error { get; set; }

        public string? Result { get; set; }
    }
}
