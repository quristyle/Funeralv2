using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Company;

/// <summary>
/// 회사 속성 판이 쓰는 복사·비교·저장 값 만들기.
/// 「저장 안 함」 판정과 실제로 보내는 값이 같은 칸 목록을 보게 한곳에 둔다.
/// </summary>
public static class CompanyForm
{
    public static CompanyDto Copy(CompanyDto c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        ShortName = c.ShortName,
        BusinessNumber = c.BusinessNumber,
        Representative = c.Representative,
        ZipCode = c.ZipCode,
        Address = c.Address,
        AddressDetail = c.AddressDetail,
        Remark = c.Remark,
        Status = c.Status,
        SortOrder = c.SortOrder,
        ApprovalDate = c.ApprovalDate,
        UsageLocations = [.. c.UsageLocations],
        UserCount = c.UserCount,
        DeptCount = c.DeptCount,
    };

    /// <summary>서버로 보낼 값. 사용처는 늘 전체를 보낸다 — 빈 목록이 「전부 해제」다.</summary>
    public static SaveCompanyDto Payload(CompanyDto c) => new()
    {
        Name = c.Name,
        ShortName = c.ShortName,
        BusinessNumber = c.BusinessNumber,
        Representative = c.Representative,
        ZipCode = c.ZipCode,
        Address = c.Address,
        AddressDetail = c.AddressDetail,
        Remark = c.Remark,
        Status = c.Status,
        SortOrder = c.SortOrder,
        ApprovalDate = c.ApprovalDate,
        UsageLocations = [.. c.UsageLocations],
    };

    /// <summary>
    /// 보낼 값이 같은가. 빈 글자와 null 은 같게 본다 — 칸을 눌렀다 비우기만
    /// 해도 「저장 안 함」이 붙지 않게.
    /// </summary>
    public static bool Same(CompanyDto a, CompanyDto b)
    {
        static string N(string? s) => s?.Trim() ?? string.Empty;

        return N(a.Name) == N(b.Name)
            && N(a.ShortName) == N(b.ShortName)
            && N(a.BusinessNumber) == N(b.BusinessNumber)
            && N(a.Representative) == N(b.Representative)
            && N(a.ZipCode) == N(b.ZipCode)
            && N(a.Address) == N(b.Address)
            && N(a.AddressDetail) == N(b.AddressDetail)
            && N(a.Remark) == N(b.Remark)
            && a.Status == b.Status
            && a.SortOrder == b.SortOrder
            && a.ApprovalDate?.Date == b.ApprovalDate?.Date
            && a.UsageLocations.Order(StringComparer.Ordinal)
                .SequenceEqual(b.UsageLocations.Order(StringComparer.Ordinal));
    }

    public sealed record StatusOption(int Code, string Name);

    public static readonly StatusOption[] Statuses =
    [
        new(1, "사용"),
        new(0, "중지"),
    ];
}
