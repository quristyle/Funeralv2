using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 소셜 가입신청 대상자를 기존 계정에 결합(Merge)하는 기능의 안전성 및 구현 검증.
/// </summary>
public sealed class SignupMergeTests
{
    private static string ReadRepoFile(string relativePath)
    {
        var testDir = AppContext.BaseDirectory;
        // Search up until repo root
        var dir = new DirectoryInfo(testDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "jsini.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("저장소 루트(jsini.sln)를 찾을 수 없습니다.");
        }

        var fullPath = Path.Combine(dir.FullName, relativePath);
        return File.ReadAllText(fullPath);
    }

    [Fact]
    public void 계정관리화면은_가입현황_단추로_가입신청목록으로_이동한다()
    {
        var source = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor");

        Assert.Contains("Text=\"가입현황\"", source);
        Assert.Contains("Navigation.NavigateTo(\"/admin/system/signup\")", source);
    }

    [Fact]
    public void 가입신청화면의_결합팝업은_대상계정선택_콤보박스와_직접입력을_제공한다()
    {
        var razorSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/SignupList.razor");
        var csSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/SignupList.razor.cs");

        // 팝업 내 콤보박스 및 텍스트박스
        Assert.Contains("<DxComboBox Data=\"@_accounts\"", razorSource);
        Assert.Contains("@bind-Value=\"_targetAccountId\"", razorSource);
        Assert.Contains("<DxTextBox @bind-Text=\"_targetAccountId\"", razorSource);

        // 코드비하인드에서 대상 계정 목록 로드 및 이메일/이름 매칭
        Assert.Contains("AdminApi.GetAccountsAsync()", csSource);
        Assert.Contains("_targetAccountId = matched.LoginId", csSource);
    }

    [Fact]
    public void 가입신청_API클라이언트는_대상계정파라미터를_URL인코딩한다()
    {
        var source = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Api/SignupClient.cs");

        Assert.Contains("Uri.EscapeDataString(targetId)", source);
    }

    [Fact]
    public void 백엔드_가입엔드포인트는_대상계정검증과_구체적에러를_반환한다()
    {
        var source = ReadRepoFile("microservices/AuthServer/Endpoints/SignupEndpoints.cs");

        Assert.Contains("string.IsNullOrWhiteSpace(targetId)", source);
        Assert.Contains("signup.MergeAsync(id, targetId, user.UserId, ct)", source);
    }

    [Fact]
    public void 백엔드_가입서비스는_Id와UserId모두로_조회하고_FK와Cascade충돌을_방어한다()
    {
        var source = ReadRepoFile("microservices/AuthServer/Services/SignupService.cs");

        // 대상 계정을 Id 또는 UserId(로그인아이디)로 조회
        Assert.Contains("a.Id == targetAccountId || a.UserId == targetAccountId", source);

        // 외래키 FK에 대상 계정의 GUID(targetAccount.Id) 설정
        Assert.Contains("login.AccountId = targetAccount.Id;", source);
        Assert.Contains("login.Account = targetAccount;", source);

        // 신청 계정 삭제 전 소셜 및 프로필 변경사항 먼저 저장
        Assert.Contains("db.Accounts.Remove(pendingAccount);", source);

        // 명시적 결과 (bool Ok, string? Error) 반환
        Assert.Contains("Task<(bool Ok, string? Error)> MergeAsync", source);
    }
}
