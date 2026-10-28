using System.Reflection;
using JSini.Web.ProjMng.Components.Pages;
using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class ProjectSchedulerColumnTests
{
    [Fact]
    public void ProjectScheduler_그리드에_시작_종료_기간_알림_컬럼이_노출되고_나머지는_숨김처리된다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);

        // 노출 컬럼 (시작, 종료, 기간, 알림)
        Assert.Contains("Caption=\"시작\"", content);
        Assert.Contains("Caption=\"종료\"", content);
        Assert.Contains("Caption=\"기간\"", content);
        Assert.Contains("Caption=\"알림\"", content);

        // 종료 컬럼은 숨김(Visible="false")되지 않고 노출되어야 함
        Assert.DoesNotContain("Caption=\"종료\" Width=\"150\"\n                                              FixedPosition=\"@PinnedColumn\"\n                                              Visible=\"false\"", content);

        // 숨김 컬럼 (구분, 종류, 요청자, 담당자, 진행, 메모, 원문)
        Assert.Contains("Caption=\"구분\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"종류\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"요청자\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"담당자\" Width=\"130\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"진행\" Width=\"115\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"메모\" MinWidth=\"140\"\n                                              TextAlignment=\"GridTextAlignment.Left\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"원문\" MinWidth=\"140\"\n                                              TextAlignment=\"GridTextAlignment.Left\"\n                                              Visible=\"false\"", content);
    }

    [Fact]
    public void ProjectScheduler_알림컬럼은_가변너비로_관리컬럼이_오른쪽에_고정된다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);

        // 시작, 기간은 왼쪽에 고정되고 알림 컬럼은 고정 Width 없이 가변 너비로 공간을 채워 관리 컬럼이 오른쪽에 고정되도록 함
        Assert.Contains("FieldName=\"@nameof(WbsItemDto.ProcNm)\" Caption=\"알림\" MinWidth=\"180\"", content);
        Assert.DoesNotContain("FieldName=\"@nameof(WbsItemDto.ProcNm)\" Caption=\"알림\" Width=\"240\"", content);

        var commGrdPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Shared/JSini.Web.Components/Data/CommGrd.razor");
        Assert.True(File.Exists(commGrdPath), $"CommGrd 파일이 존재해야 합니다: {commGrdPath}");

        var commGrdContent = File.ReadAllText(commGrdPath);
        Assert.Contains("Name=\"@ActionsColumnName\"", commGrdContent);
        Assert.Contains("FixedPosition=\"@(Carded ? GridColumnFixedPosition.None : GridColumnFixedPosition.Right)\"", commGrdContent);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(0.0, "0시간")]
    [InlineData(4.0, "4시간")]
    [InlineData(24.0, "24시간")]
    [InlineData(28.0, "28시간")]
    [InlineData(48.0, "48시간")]
    [InlineData(72.5, "72시간 30분")]
    public void HoursText_총기간_시간_포맷_표현을_검증한다(double? hours, string expected)
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
