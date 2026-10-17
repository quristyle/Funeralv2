using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 좁은 자리의 시각 표기 — <b>오늘에서 먼 만큼만 적는다</b>(2026-10-15).
/// </summary>
/// <remarks>
/// <para>
/// 휴대폰에서 이 판을 열어 눈으로 보는 길은 <b>그날 하루치만 확인할 수 있다.</b>
/// 「지난달 것」과 「작년 것」을 화면으로 보려면 그만큼 묵은 건을 찾아 열어야
/// 하고, 해가 바뀌는 경계는 한 해에 하루뿐이다.
/// </para>
/// <para>
/// <b>틀려도 화면은 멀쩡하다.</b> 「14일 09:30」은 어느 해 어느 달의 것이든
/// 같은 얼굴로 서므로, 1년 어긋난 시각을 오늘 것으로 읽어도 읽는 사람이
/// 알아챌 단서가 없다 — 그것이 여기 박아 두는 까닭이다.
/// </para>
/// </remarks>
public sealed class AiTaskWhenNearTests
{
    /// <summary>오늘 것은 <b>시·분만</b>. 날짜는 읽는 사람이 되채울 수 있다.</summary>
    [Fact]
    public void 오늘_것은_시분만_적는다()
    {
        var today = AppTime.Today;

        Assert.Equal("09:30", AiTaskWhen.Near(Kst(today, 9, 30)));
    }

    /// <summary>이달 것은 <b>날짜까지</b>. 맨 숫자로 두면 시각과 안 갈려 「일」을 붙인다.</summary>
    [Fact]
    public void 이달_것은_날짜에_일을_붙인다()
    {
        var today = AppTime.Today;

        // 오늘이 1일이면 어제가 지난달이다 — 이달 안의 **다른** 날을 고른다.
        var day = today.Day == 1 ? 2 : 1;
        var other = new DateOnly(today.Year, today.Month, day);

        Assert.Equal($"{day:00}일 09:30", AiTaskWhen.Near(Kst(other, 9, 30)));
    }

    /// <summary>올해의 다른 달이면 <b>달까지</b> 적고 해는 뗀다.</summary>
    [Fact]
    public void 올해의_다른_달이면_월일을_적는다()
    {
        var today = AppTime.Today;
        var month = today.Month == 1 ? 2 : 1;
        var other = new DateOnly(today.Year, month, 14);

        Assert.Equal($"{month:00}-14 09:30", AiTaskWhen.Near(Kst(other, 9, 30)));
    }

    /// <summary>
    /// <b>해가 다르면 해를 붙인다.</b> 떼어도 되는 것은 읽는 사람이 되채울 수
    /// 있는 것뿐이고, 해는 그렇지 않다.
    /// </summary>
    [Fact]
    public void 지난해_것에는_해가_붙는다()
    {
        var today = AppTime.Today;
        var other = new DateOnly(today.Year - 1, 9, 14);

        Assert.Equal($"{today.Year - 1}-09-14 09:30", AiTaskWhen.Near(Kst(other, 9, 30)));
    }

    /// <summary>
    /// <b>달만 견주면 안 된다.</b> 작년 같은 달이 이달로 읽혀 「14일 09:30」이
    /// 되면, 1년 어긋난 시각이 오늘 것과 같은 꼴로 선다.
    /// </summary>
    [Fact]
    public void 작년_같은_달을_이달로_읽지_않는다()
    {
        var today = AppTime.Today;
        var sameMonthLastYear = new DateOnly(today.Year - 1, today.Month, 14);

        Assert.Equal(
            $"{today.Year - 1}-{today.Month:00}-14 09:30",
            AiTaskWhen.Near(Kst(sameMonthLastYear, 9, 30)));
    }

    /// <summary>시각이 없으면 비운다 — 적을 것이 없는 자리에 오늘을 적지 않는다.</summary>
    [Fact]
    public void 시각이_없으면_빈_표시다()
    {
        Assert.Equal("-", AiTaskWhen.Near(null));
    }

    /// <summary>
    /// 카드와 휴대폰 머리띠가 <b>같은 규칙</b>으로 적는다 —
    /// <c>RunAt(compact: true)</c> 가 이 표기를 그대로 쓴다.
    /// </summary>
    [Fact]
    public void 좁은_자리의_실행일시도_같은_규칙이다()
    {
        var today = AppTime.Today;
        var task = new AiTaskDto { StartedAt = Kst(today, 9, 30) };

        Assert.Equal("09:30", AiTaskWhen.RunAt(task, compact: true));
        Assert.Equal(AppTime.ToKorea(task.StartedAt!.Value).ToString("yyyy-MM-dd HH:mm"),
            AiTaskWhen.RunAt(task));
    }

    /// <summary>
    /// <b>머리띠의 시각 둘도 같은 규칙이다</b>(2026-10-17). 한동안 이 표기는
    /// 휴대폰에서만 섰고 책상에서는 <c>yyyy-MM-dd HH:mm</c> 열여섯 글자가
    /// 그대로 붙었다 — 거기 서는 건도 거의 오늘·이번 달 것이라 <b>앞의 열
    /// 글자가 거의 늘 같은 글자</b>였다.
    /// </summary>
    /// <remarks>
    /// <b>눈으로는 못 지킨다.</b> 되돌아가도 화면은 멀쩡하고, 오늘 것을 보는
    /// 동안에는 「날짜가 함께 보이는구나」로만 읽힌다.
    /// </remarks>
    [Fact]
    public void 머리띠의_작성과_동작은_짧은_표기를_쓴다()
    {
        var razor = TaskViewRazor();

        // 작성 — 보낸 시각. 짧게 적고 연월일은 `title` 에 남긴다.
        Assert.Contains("title=\"@t.CreDt.Kst(\"yyyy-MM-dd HH:mm\")\">@AiTaskWhen.Near(t.CreDt)", razor);

        // 동작 — 실제로 돈 시각. 같은 짝이다.
        Assert.Contains("title=\"@AiTaskWhen.RunAt(t)\">@AiTaskWhen.RunAt(t, compact: true)", razor);
    }

    /// <summary>
    /// <b>폭으로 갈리지 않는다.</b> 갈라 두면 같은 건의 같은 시각이 기기마다
    /// 다른 꼴로 서고, 그 갈래를 되살리는 쪽이 사람 눈에는 「휴대폰만 짧다」로
    /// 보여 틀림으로 읽히지 않는다.
    /// </summary>
    [Fact]
    public void 머리띠가_폭을_재지_않는다()
    {
        var razor = TaskViewRazor();

        Assert.DoesNotContain("_isPhone", razor);
        Assert.DoesNotContain("DxLayoutBreakpoint", razor);
    }

    private static string TaskViewRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "AiTaskView.razor"));

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>한국 벽시계로 적은 날·시각을, 서버가 주는 꼴(UTC)로 되돌린다.</summary>
    private static DateTime Kst(DateOnly day, int hour, int minute) =>
        AppTime.FromKorea(day.ToDateTime(new TimeOnly(hour, minute)));
}
