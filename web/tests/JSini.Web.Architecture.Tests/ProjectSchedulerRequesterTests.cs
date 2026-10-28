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
}
