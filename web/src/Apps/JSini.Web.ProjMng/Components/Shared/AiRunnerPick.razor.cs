using System.Linq.Expressions;

using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>AI 고르개. 이름과 남은 한도를 보여 준다. 한도는 부르는 쪽이 Usage 로 넘긴다 — 부품이 읽으면 같은 것을 두 번 묻는다.</summary>
public partial class AiRunnerPick
{
    /// <summary>고를 수 있는 AI. 이미 걸러서 넘긴다 — 허용 여부는 부르는 쪽만 안다.</summary>
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

    /// <summary>그 AI 의 한도 줄들. 안 주면 배지를 그리지 않는다.</summary>
    [Parameter] public Func<string?, IReadOnlyCollection<AiUsageSnapshot>>? Usage { get; set; }

    /// <summary>한도를 한 번이라도 받아 봤나. 거짓이면 「확인 중」으로 적는다 — 받는 중인데 「보고 없음」으로 보이면 멀쩡한 장비를 의심하게 된다.</summary>
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
