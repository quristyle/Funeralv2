using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 기존 계정을 대상 계정으로 결합(Merge)하는 기능의 구조 및 구현 검증.
/// </summary>
public sealed class AccountMergeTests
{
    private static string ReadRepoFile(string relativePath)
    {
        var testDir = AppContext.BaseDirectory;
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
    public void 계정관리화면은_줄동작에_계정결합단추를_제공하고_결합팝업이_존재한다()
    {
        var razorSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor");

        // 행 작업 단추 (계정 결합)
        Assert.Contains("IconCssClass=\"jsini-icon-link\"", razorSource);
        Assert.Contains("Title=\"다른 계정으로 결합\"", razorSource);
        Assert.Contains("AskMergeAsync(a)", razorSource);

        // 마스크 규칙이 없으면 아이콘 묶음의 바탕색만 남아 꽉 찬 네모로 그려진다.
        var css = ReadRepoFile("web/src/Shared/JSini.Web.Components/wwwroot/app.css");
        Assert.Matches(@"\.jsini-icon-link\s*\{[^}]*mask-image", css);

        // 계정 결합 팝업
        Assert.Contains("<CommPopup @bind-Visible=\"_mergingVisible\" HeaderText=\"계정 결합\"", razorSource);
        Assert.Contains("<DxComboBox Data=\"@MergeTargetCandidates\"", razorSource);
        Assert.Contains("@bind-Value=\"_mergeTargetAccountId\"", razorSource);
        Assert.Contains("<DxTextBox @bind-Text=\"_mergeTargetAccountId\"", razorSource);
        Assert.Contains("Click=\"@MergeAsync\"", razorSource);
    }

    [Fact]
    public void 계정관리_코드비하인드는_대상계정추천과_결합API호출_및_목록새로고침을_제공한다()
    {
        var csSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor.cs");

        // 후보 목록 (자신 제외)
        Assert.Contains("MergeTargetCandidates", csSource);
        Assert.Contains("a.Id != _mergeSource?.Id", csSource);

        // 자동 추천 및 팝업 오픈
        Assert.Contains("void AskMergeAsync(AccountDto a)", csSource);
        Assert.Contains("_mergeTargetAccountId = matched.LoginId", csSource);

        // 결합 실행 및 새로고침
        Assert.Contains("Task MergeAsync()", csSource);
        Assert.Contains("Api.MergeAccountAsync(source.Id, targetId)", csSource);
        Assert.Contains("await ReloadAsync()", csSource);
    }

    [Fact]
    public void 관리자_API클라이언트는_계정결합요청시_인코딩된_URL을_전송한다()
    {
        var clientSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Api/AdminClient.cs");

        Assert.Contains("MergeAccountAsync(string sourceId, string targetId", clientSource);
        Assert.Contains("auth/system/account/{Uri.EscapeDataString(sourceId)}/merge?targetId={Uri.EscapeDataString(targetId)}", clientSource);
    }

    [Fact]
    public void 백엔드_시스템엔드포인트는_관리자권한검증과_계정결합_엔드포인트를_노출한다()
    {
        var endpointSource = ReadRepoFile("microservices/AuthServer/Endpoints/SystemEndpoints.cs");

        Assert.Contains("group.MapPost(\"/account/{id}/merge\"", endpointSource);
        Assert.Contains("IsAdmin(user, http)", endpointSource);
        Assert.Contains("string.IsNullOrWhiteSpace(targetId)", endpointSource);
        Assert.Contains("userService.MergeAccountAsync(id, targetId, user.UserId, ct)", endpointSource);
    }

    [Fact]
    public void 백엔드_유저서비스는_계정결합시_소셜연동_이메일_전화번호_즐겨찾기_병합과_2단계삭제를_수행한다()
    {
        var interfaceSource = ReadRepoFile("microservices/AuthServer/Services/IUserService.cs");
        var serviceSource = ReadRepoFile("microservices/AuthServer/Services/UserService.cs");

        // 인터페이스 정의 확인
        Assert.Contains("Task<(bool Ok, string? Error)> MergeAccountAsync", interfaceSource);

        // 서비스 구현: Id 또는 UserId 조회
        Assert.Contains("a.Id == sourceAccountId || a.UserId == sourceAccountId", serviceSource);
        Assert.Contains("a.Id == targetAccountId || a.UserId == targetAccountId", serviceSource);

        // 소셜 연동 이전 및 중복 처리
        Assert.Contains("_db.AccountSocialLogins", serviceSource);
        Assert.Contains("login.AccountId = targetAccount.Id;", serviceSource);

        // WebAuthn 자격증명 및 즐겨찾기(MenuFavorites), 역할(RoleAccounts) 병합
        Assert.Contains("_db.AccountWebAuthnCredentials", serviceSource);
        Assert.Contains("_db.MenuFavorites", serviceSource);
        Assert.Contains("_db.RoleAccounts", serviceSource);

        // 이메일 및 전화번호 병합, 결합 이력 기록
        Assert.Contains("DetailType = \"Email\"", serviceSource);
        Assert.Contains("DetailType = \"Phone\"", serviceSource);
        Assert.Contains("DetailType = \"SocialMerge\"", serviceSource);

        // 2단계 저장 및 원본 계정 삭제
        Assert.Contains("_db.Accounts.Remove(sourceAccount);", serviceSource);
        Assert.Contains("await _db.SaveChangesAsync(ct);", serviceSource);

        // 알림 발송
        Assert.Contains("_notify.NotifyOwnersAsync", serviceSource);
        Assert.Contains("_mail.SendAsync", serviceSource);
    }
}
