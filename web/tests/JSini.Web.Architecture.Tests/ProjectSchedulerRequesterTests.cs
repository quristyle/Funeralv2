using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class ProjectSchedulerRequesterTests
{
    [Fact]
    public void WbsItemDto_요청자_담당자_필드가_존재한다()
    {
        var dto = new WbsItemDto
        {
            ReqUser = "홍길동",
            DevUser = "이순신",
        };

        Assert.Equal("홍길동", dto.ReqUser);
        Assert.Equal("이순신", dto.DevUser);
    }

    [Fact]
    public void ProjectScheduler_마크업에_요청자와_담당자_컬럼이_정의되어_있다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);
        Assert.Contains("FieldName=\"@nameof(WbsItemDto.ReqUser)\" Caption=\"요청자\"", content);
        Assert.Contains("FieldName=\"@nameof(WbsItemDto.DevUser)\" Caption=\"담당자\"", content);
        Assert.Contains("FieldName=\"@nameof(WbsItemDto.BodyText)\" Caption=\"원문\"", content);
    }

    [Fact]
    public void WbsEditForm_마크업에_요청자와_담당자_입력칸이_정의되어_있다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Shared/WbsEditForm.razor");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);
        Assert.Contains("Caption=\"요청자\"", content);
        Assert.Contains("@bind-Text=\"Item.ReqUser\"", content);
        Assert.Contains("Caption=\"담당자\"", content);
        Assert.Contains("@bind-Text=\"Item.DevUser\"", content);
        Assert.Contains("Caption=\"원문\"", content);
        Assert.Contains("@bind-Text=\"Item.BodyText\"", content);
    }

    [Fact]
    public void ScheduleEditPopup_마크업에_높이와_최소높이가_설정되어_최소크기를_유지한다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Shared/ScheduleEditPopup.razor");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);
        Assert.Contains("CssClass=\"pm-scheduler-edit-popup\"", content);
        Assert.Contains("Height=\"700px\"", content);
        Assert.Contains("MinHeight=\"640px\"", content);
    }

    [Fact]
    public void projmng_css_에_일정수정_팝업_확장시_메모와_원문이_늘어나는_규칙이_정의되어_있다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);
        Assert.Contains(".dxbl-popup.pm-scheduler-edit-popup .dxbl-modal-content", content);
        Assert.Contains(".pm-scheduler-edit .dxbl-fl .dxbl-fl-item:nth-last-child(-n + 2)", content);
        Assert.Contains("grid-template-rows: auto auto auto auto auto 1fr", content);
    }
}
