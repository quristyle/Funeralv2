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

    [Fact]
    public void 계정관리화면은_소셜결합계정을_식별할수있고_소셜연동컬럼과_결합뱃지를_제공한다()
    {
        var razorSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor");
        var csSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor.cs");

        // 아이디 컬럼의 소셜결합 뱃지 및 소셜 연동 컬럼
        Assert.Contains("IsSocialMerged", razorSource);
        Assert.Contains("소셜결합", razorSource);
        Assert.Contains("Caption=\"소셜 연동\"", razorSource);
        Assert.Contains("SocialBadgeClass", razorSource);

        // 검색 시 소셜 정보 및 전화번호/이메일 검색 지원
        Assert.Contains("a.SocialProviderText?.Contains(k", csSource);
        Assert.Contains("a.Emails?.Any(e => e.Contains(k", csSource);
        Assert.Contains("a.Phones?.Any(p => p.Contains(k", csSource);
    }

    [Fact]
    public void 계정관리화면은_결합된_이메일과_전화번호를_모두_표현한다()
    {
        var razorSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor");

        // 이메일 컬럼에서 복수 이메일 모두 출력
        Assert.Contains("var emails = a.Emails", razorSource);
        Assert.Contains("foreach (var em in emails)", razorSource);

        // 연락처 컬럼에서 복수 연락처 모두 출력
        Assert.Contains("var phones = a.Phones", razorSource);
        Assert.Contains("foreach (var ph in phones)", razorSource);
    }

    [Fact]
    public void 소셜가입_결합시에_대상계정_사용승인과_기본역할부여_및_정보병합이_반영된다()
    {
        var source = ReadRepoFile("microservices/AuthServer/Services/SignupService.cs");

        // 1. 소셜 계정 사용승인 반영 (targetStatus.Content = StatusActive)
        Assert.Contains("targetStatus.Content = StatusActive;", source);
        Assert.Contains("SignupDefaultRoles.AddAsync(db, configuration, logger, targetAccount.Id, approver, ct);", source);

        // 2. 전화번호 및 이메일 병합
        Assert.Contains("pendingPhone", source);
        Assert.Contains("DetailType = \"Phone\"", source);

        // 3. 결합 이력 기록
        Assert.Contains("DetailType = \"SocialMerge\"", source);
        Assert.Contains("Content = login.Provider", source);
    }

    [Fact]
    public void 결합된_소셜계정_로그인시에_기존계정과_결합계정_모두에게_앱알림과_이메일을_발송한다()
    {
        var endpointSource = ReadRepoFile("microservices/AuthServer/Endpoints/SocialLoginEndpoints.cs");
        var serviceSource = ReadRepoFile("microservices/AuthServer/Services/SocialLoginService.cs");

        // 엔드포인트에서 결합 로그인 알림 호출
        Assert.Contains("social.NotifyMergedLoginAsync(outcome.Account!, request.Provider", endpointSource);

        // 서비스에서 결합 이력(SocialMerge) 확인
        Assert.Contains("d.DetailType == \"SocialMerge\"", serviceSource);

        // 앱 알림 푸시 발송 (기존계정 및 결합계정 대상)
        Assert.Contains("push.NotifyOwnersAsync", serviceSource);

        // 이메일 알림 발송 (기존계정 및 결합계정 이메일)
        Assert.Contains("mail.SendAsync", serviceSource);
        Assert.Contains("emailTargets", serviceSource);
    }
}
