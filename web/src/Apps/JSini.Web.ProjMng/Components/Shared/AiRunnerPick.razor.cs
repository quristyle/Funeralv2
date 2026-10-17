using System.Linq.Expressions;

using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// AI 고르개. 이름과 남은 한도를 함께 보여 준다 — 그리는 까닭은 마크업 머리말에.
/// </summary>
/// <remarks>
/// <para>
/// <b>한도는 부르는 쪽이 읽어 온다</b>(<see cref="Usage"/>). 부품이 직접 읽게
/// 두면 같은 화면에 고르개가 둘 설 때 같은 것을 두 번 묻고, 서랍처럼 오래
/// 살아 있는 자리에서 <b>언제 다시 읽을지</b>를 부품이 알 길이 없다.
/// </para>
/// <para>
/// 한도를 안 주면(<see cref="Usage"/> 가 <c>null</c>) 배지 없이 이름만
/// 그린다 — 못 읽었다고 고르는 일 자체를 막지 않는다.
/// </para>
/// </remarks>
public partial class AiRunnerPick
{
    /// <summary>
    /// 고를 수 있는 AI. <b>이미 걸러서 넘긴다</b> — 고른 대상이 무엇을
    /// 허용하는지는 부르는 쪽만 안다.
    /// </summary>
    [Parameter] public IReadOnlyList<AiModelOption> Items { get; set; } = [];

    /// <summary>고른 값. <c>@bind-Value</c> 로 묶는다.</summary>
    [Parameter] public string? Value { get; set; }

    /// <inheritdoc cref="Value"/>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <inheritdoc cref="Value"/>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>고를 수 있나. 고를 것이 하나뿐일 때도 꺼 둔다(부르는 쪽 판단).</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>아무것도 안 골랐을 때 적을 글.</summary>
    [Parameter] public string? NullText { get; set; }

    /// <summary>자리마다 다른 폭·여백을 주는 고리.</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <summary>
    /// 그 AI 의 한도 줄들을 집어 주는 길. 한 AI 가 여럿을 가질 수 있다.
    /// <b>안 주면 배지를 안 그린다.</b>
    /// </summary>
    [Parameter] public Func<string?, IReadOnlyCollection<AiUsageSnapshot>>? Usage { get; set; }

    /// <summary>
    /// 한도를 <b>한 번이라도 받아 봤나.</b> 거짓이면 「확인 중」으로 적는다.
    /// </summary>
    /// <remarks>
    /// 받아 보기 전과 받아 봤는데 빈 것은 다른 상태다. 가르지 않으면 아직
    /// 묻는 중인 CLI 가 <b>「보고 없음」</b>으로 보이는데, 그 글자는
    /// 「실행기가 그 CLI 를 못 묻고 있다」는 뜻이라 멀쩡한 장비를
    /// 들여다보게 만든다.
    /// </remarks>
    [Parameter] public bool UsageSeen { get; set; } = true;

    private static readonly AiUsageText.Badge None = new(string.Empty, string.Empty);

    private static readonly AiUsageText.Badge Checking = new("한도 확인 중", "jsini-badge--off");

    private static readonly AiUsageText.Badge CheckingMini = new("확인중", "jsini-badge--off");

    private string PickCss => string.IsNullOrWhiteSpace(CssClass)
        ? "pm-aipick"
        : $"pm-aipick {CssClass}";

    /// <summary>펼친 목록 한 줄에 다는 배지. 세션·주간을 풀어 적는다.</summary>
    private AiUsageText.Badge Full(string? kind)
        => Usage is null ? None
            : UsageSeen ? AiUsageText.Of([.. Usage(kind)])
            : Checking;

    /// <summary>닫힌 칸에 다는 짧은 배지. 숫자 하나와 색뿐이다.</summary>
    private AiUsageText.Badge Mini(string? kind)
        => Usage is null ? None
            : UsageSeen ? AiUsageText.Mini([.. Usage(kind)])
            : CheckingMini;

    /// <summary>닫힌 칸에 못 담은 한도 전문. 그 칸의 <c>title</c> 이 된다.</summary>
    private string Title(AiModelOption pick)
    {
        if (Usage is null)
        {
            return pick.Text;
        }

        var full = UsageSeen ? AiUsageText.Summary(Usage(pick.Value)) : "한도 확인 중";

        return string.IsNullOrWhiteSpace(full) ? pick.Text : $"{pick.Text} · {full}";
    }
}
