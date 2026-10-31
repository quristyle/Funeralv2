using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
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

    [Fact]
    public void ScheduleEditPopup_복제_기능은_현재_정보를_저장하고_메모와_원문을_포함하여_복제한다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Shared/ScheduleEditPopup.razor.cs");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);

        // 원문을 null 로 초기화하던 이전 버그가 없어야 함
        Assert.DoesNotContain("clone.BodyText = null;", content);

        // 복제 전 현재 정보 저장이 호출되어야 함
        Assert.Contains("Api.UpdateAsync(_form)", content);

        // 복제 생성 시 Api.CreateAsync(clone) 호출 및 팝업 갱신
        Assert.Contains("Api.CreateAsync(clone)", content);
        Assert.Contains("StateHasChanged()", content);
    }

    [Fact]
    public void ScheduleEditPopup_Copy_메모와_원문_요약_상태를_그대로_복사한다()
    {
        var source = new WbsItemDto
        {
            WbsId = 123,
            PrjRid = 1,
            ProcNm = "일정 테스트",
            Comm = "테스트 메모 내용",
            BodyText = "테스트 원문 내용\n두 번째 줄",
            Summary = "테스트 요약",
            ScheduleType = "Public",
            WbsState = "RUNNING",
            Discarded = false,
        };

        var copied = ScheduleEditPopup.Copy(source);

        Assert.Equal(source.WbsId, copied.WbsId);
        Assert.Equal(source.PrjRid, copied.PrjRid);
        Assert.Equal(source.ProcNm, copied.ProcNm);
        Assert.Equal(source.Comm, copied.Comm);
        Assert.Equal(source.BodyText, copied.BodyText);
        Assert.Equal(source.Summary, copied.Summary);
        Assert.Equal(source.ScheduleType, copied.ScheduleType);
        Assert.Equal(source.WbsState, copied.WbsState);
        Assert.Equal(source.Discarded, copied.Discarded);
    }
}
