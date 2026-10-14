using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// /admin/profile 화면의 소셜 계정 결합 정보 노출 및 다중 연락처(이메일, 전화번호) 표현/관리 기능 검증.
/// </summary>
public sealed class ProfileSocialAndMultiContactTests
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
    public void 프로필화면은_사이드바와_기본설정에_소셜결합_식별뱃지와_연동정보를_표시한다()
    {
        var razorSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/Profile.razor");

        // 좌측 사이드바 패널의 소셜결합 뱃지
        Assert.Contains("_info?.IsSocialMerged == true", razorSource);
        Assert.Contains("소셜결합", razorSource);
        Assert.Contains("_info.SocialProviders", razorSource);

        // 기본 설정 탭의 소셜 연동 정보
        Assert.Contains("Caption=\"소셜 연동\"", razorSource);
    }

    [Fact]
    public void 프로필화면_기본설정은_다중이메일과_다중전화번호를_TagBox로_관리한다()
    {
        var razorSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/Profile.razor");
        var csSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/Profile.razor.cs");

        // TagBox 바인딩
        Assert.Contains("@bind-Values=\"_edit.EmailValues\"", razorSource);
        Assert.Contains("@bind-Values=\"_edit.PhoneValues\"", razorSource);
        Assert.Contains("AllowCustomTags=\"true\"", razorSource);

        // Revert 시 다중 이메일/전화번호 복원
        Assert.Contains("Emails = _info.Emails.ToList()", csSource);
        Assert.Contains("Phones = _info.Phones.ToList()", csSource);
    }

    [Fact]
    public void 프로필화면_계정정보탭은_다중이메일_다중연락처와_소셜결합그룹_및_상세목록을_표시한다()
    {
        var razorSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/Profile.razor");

        // 계정 그룹 내 다중 이메일/연락처
        Assert.Contains("var emails = _info.Emails", razorSource);
        Assert.Contains("var phones = _info.Phones", razorSource);
        Assert.Contains("대표", razorSource);
        Assert.Contains("보조", razorSource);

        // 소셜 계정 결합 카드 그룹
        Assert.Contains("Caption=\"소셜 계정 결합\"", razorSource);
        Assert.Contains("Caption=\"소셜 상태\"", razorSource);
        Assert.Contains("Caption=\"결합 목록\"", razorSource);
        Assert.Contains("socialAcc.MergedLoginId", razorSource);
        Assert.Contains("socialAcc.LinkedAt", razorSource);
    }

    [Fact]
    public void 모바일허브요약은_다중연락처와_소셜결합정보를_반영한다()
    {
        var csSource = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Components/Pages/Profile.razor.cs");

        Assert.Contains("_info!.Emails.Count > 1", csSource);
        Assert.Contains("_info.Phones.Count > 1", csSource);
        Assert.Contains("_info?.IsSocialMerged == true ? \"소셜결합\"", csSource);
        Assert.Contains("_info.IsSocialMerged && !string.IsNullOrWhiteSpace(_info.SocialProviderText)", csSource);
    }

    [Fact]
    public void 프론트엔드_모델은_소셜결합정보와_다중연락처_바인딩을_지원한다()
    {
        var source = ReadRepoFile("web/src/Apps/JSini.Web.Admin/Api/AdminModels.cs");

        // UserInfoDto
        Assert.Contains("public List<string> Emails { get; set; } = [];", source);
        Assert.Contains("public List<string> Phones { get; set; } = [];", source);
        Assert.Contains("public List<string> SocialProviders { get; set; } = [];", source);
        Assert.Contains("public bool IsSocialMerged { get; set; }", source);
        Assert.Contains("public List<SocialMergedAccountDto> MergedSocialAccounts { get; set; } = [];", source);

        // UpdateProfileDto
        Assert.Contains("public IEnumerable<string> EmailValues", source);
        Assert.Contains("public IEnumerable<string> PhoneValues", source);
    }

    [Fact]
    public void 백엔드_UserInfo와_UserService는_소셜결합과_다중연락처를_조회_및_저장한다()
    {
        var dtoSource = ReadRepoFile("microservices/AuthServer/DTOs/UserInfoDto.cs");
        var updateDtoSource = ReadRepoFile("microservices/AuthServer/DTOs/UpdateProfileDto.cs");
        var serviceSource = ReadRepoFile("microservices/AuthServer/Services/UserService.cs");

        // DTO 정의
        Assert.Contains("public List<string> Emails { get; set; }", dtoSource);
        Assert.Contains("public List<string> Phones { get; set; }", dtoSource);
        Assert.Contains("public bool IsSocialMerged { get; set; }", dtoSource);
        Assert.Contains("public class SocialMergedAccountDto", dtoSource);

        Assert.Contains("public List<string>? Emails { get; set; }", updateDtoSource);
        Assert.Contains("public List<string>? Phones { get; set; }", updateDtoSource);

        // UserService.GetUserInfoAsync 조회
        Assert.Contains("var accountSocials = await _db.AccountSocialLogins", serviceSource);
        Assert.Contains("p.DetailType == \"SocialMerge\"", serviceSource);
        Assert.Contains("MergedSocialAccounts = mergedSocialAccounts", serviceSource);

        // UserService.UpdateProfileAsync 다중 저장
        Assert.Contains("emailsToSave", serviceSource);
        Assert.Contains("phonesToSave", serviceSource);
    }
}
