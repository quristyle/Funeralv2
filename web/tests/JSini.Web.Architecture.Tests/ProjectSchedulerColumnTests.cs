using System.Reflection;
using JSini.Web.ProjMng.Components.Pages;
using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class ProjectSchedulerColumnTests
{
    [Fact]
    public void ProjectScheduler_그리드에_시작_기간_알림_컬럼이_노출되고_나머지는_숨김처리된다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);

        // 노출 컬럼 (시작, 기간, 알림)
        Assert.Contains("Caption=\"시작\"", content);
        Assert.Contains("Caption=\"기간\"", content);
        Assert.Contains("Caption=\"알림\"", content);

        // 숨김 컬럼 (종료, 구분, 종류, 요청자, 담당자, 진행, 메모, 원문)
        Assert.Contains("Caption=\"종료\" Width=\"150\"\n                                              FixedPosition=\"@PinnedColumn\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"구분\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"종류\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"요청자\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"담당자\" Width=\"130\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"진행\" Width=\"115\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"메모\" MinWidth=\"140\"\n                                              TextAlignment=\"GridTextAlignment.Left\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"원문\" MinWidth=\"140\"\n                                              TextAlignment=\"GridTextAlignment.Left\"\n                                              Visible=\"false\"", content);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(0.0, "00 00")]
    [InlineData(4.0, "00 04")]
    [InlineData(24.0, "01 00")]
    [InlineData(28.0, "01 04")]
    [InlineData(48.0, "02 00")]
    [InlineData(72.5, "03 00")]
    public void HoursText_DD_HH24_포맷_표현을_검증한다(double? hours, string expected)
    {
        var method = typeof(ProjectScheduler).GetMethod("HoursText", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string?)method.Invoke(null, [hours]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ProjectScheduler_다크모드_인라인_편집창_배경색과_글자색_대비_스타일이_정의되어_있다()
    {
        var cssPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css");
        Assert.True(File.Exists(cssPath), $"파일이 존재해야 합니다: {cssPath}");

        var css = File.ReadAllText(cssPath);

        // 고정 흰색 배경(#fff)으로 하드코딩되어 다크 모드 글자색과 충돌하던 문제가 없어야 함
        Assert.DoesNotContain("var(--jsini-bg-surface, #fff)", css);

        // 다크모드 선택자에서 입력박스 배경과 글자색 스타일 정의 여부
        Assert.Contains(":root[data-theme='dark'] .pm-inline-edit", css);
        Assert.Contains(":root[data-bs-theme='dark'] .pm-inline-edit", css);
        Assert.Contains("--dxbl-text-edit-color: var(--tb-text", css);
        Assert.Contains("color: var(--tb-text, var(--jsini-text, #dee2e6));", css);
    }
}
