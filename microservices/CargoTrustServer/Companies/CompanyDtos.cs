using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Statistics;
using CargoTrustServer.Transactions;

namespace CargoTrustServer.Companies;

/// <summary>CompanyInfo. 사업자번호는 관리자가 아니면 뒤 5자리를 가린다.</summary>
public class CompanyInfoDto
{
    public long CompanyId { get; set; }
    public string BusinessNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CeoName { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? Phone { get; set; }
    public string? BusinessType { get; set; }
    public CompanyStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>CompanySummary — 검색 결과 한 칸. 통계는 전체 기간.</summary>
public record CompanySummaryDto(
    long CompanyId,
    string BusinessNumber,
    string CompanyName,
    string? CeoName,
    string? Region,
    string? BusinessType,
    CompanyStatus Status,
    CompanyStatsDto Stats);

/// <summary>CompanyDetail</summary>
public record CompanyDetailDto(
    CompanyInfoDto Company,
    CompanyStatsDto Stats,
    List<CompanyStatsDto> Periods,
    List<PublicTransactionDto> RecentTransactions,
    List<PublicTransactionDto> RecentUnpaid,
    int MyTransactionCount);

/// <summary>CompanyCreateRequest</summary>
public class CompanyCreateRequest
{
    public string? BusinessNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? CeoName { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? Phone { get; set; }
    public string? BusinessType { get; set; }
}

public static class CompanyMap
{
    public static T Info<T>(Company c, bool isAdmin) where T : CompanyInfoDto, new() => new()
    {
        CompanyId = c.CompanyId,
        BusinessNumber = BusinessNumber.Display(c.BusinessNumber, isAdmin),
        CompanyName = c.CompanyName,
        CeoName = c.CeoName,
        Address = c.Address,
        Region = c.Region,
        Phone = c.Phone,
        BusinessType = c.BusinessType,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
    };

    public static CompanyInfoDto Info(Company c, bool isAdmin) => Info<CompanyInfoDto>(c, isAdmin);

    public static CompanySummaryDto Summary(Company c, bool isAdmin, CompanyStatsDto stats) => new(
        c.CompanyId,
        BusinessNumber.Display(c.BusinessNumber, isAdmin),
        c.CompanyName,
        c.CeoName,
        c.Region,
        c.BusinessType,
        c.Status,
        stats);
}

/// <summary>
/// 회사 입력 검사 — 사용자 등록과 관리자 저장이 같은 규칙을 쓴다.
/// </summary>
public static class CompanyInput
{
    /// <summary>검사를 통과하면 null 과 함께 정규화된 사업자번호를 돌려준다.</summary>
    public static string? Validate(CompanyCreateRequest req, out string businessNumber)
    {
        if (!Common.BusinessNumber.TryNormalize(req.BusinessNumber, out businessNumber, out var error))
            return error;
        if (Check.Clean(req.CompanyName) is null)
            return "회사명을 입력하세요.";

        return Check.MaxLength(Check.Clean(req.CompanyName), 200, "회사명")
               ?? Check.MaxLength(Check.Clean(req.CeoName), 100, "대표자명")
               ?? Check.MaxLength(Check.Clean(req.Address), 500, "주소")
               ?? Check.MaxLength(Check.Clean(req.Region), 50, "지역")
               ?? Check.MaxLength(Check.Clean(req.Phone), 50, "전화번호")
               ?? Check.MaxLength(Check.Clean(req.BusinessType), 100, "업종");
    }

    /// <summary>요청 값을 엔티티에 옮긴다(사업자번호 제외).</summary>
    public static void Apply(Company c, CompanyCreateRequest req)
    {
        c.CompanyName = Check.Clean(req.CompanyName)!;
        c.CeoName = Check.Clean(req.CeoName);
        c.Address = Check.Clean(req.Address);
        // 지역을 비워 보내면 주소의 첫 낱말(「서울 강서구 …」→「서울」)로 채운다.
        // 검색 결과가 지역으로 묶여 보이는데, 입력하는 사람은 주소만 적는 일이 많다.
        c.Region = Check.Clean(req.Region) ?? FirstWord(c.Address);
        c.Phone = Check.Clean(req.Phone);
        c.BusinessType = Check.Clean(req.BusinessType);
    }

    public static string? FirstWord(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var word = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return word.Length > 50 ? word[..50] : word;
    }
}

/// <summary>공개 후기(PublicReview) — 누가 썼는지는 싣지 않는다.</summary>
public record PublicReviewDto(
    long ReviewId,
    DateOnly TransportDate,
    decimal Amount,
    PaymentStatus PaymentStatus,
    string Content,
    DateTimeOffset CreatedAt);
