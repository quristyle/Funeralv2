namespace AuthServer.DTOs;

/// <summary>회사 사용처의 처리 담당자 한 줄.</summary>
public class CompanyUsageManagerDto
{
    public int Id { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string CodeValue { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    /// <summary>담당자 본인의 소속 회사 — 맡은 회사와 다를 수 있다(우리 직원이 고객사를 맡는다).</summary>
    public string? CompanyName { get; set; }
    public string? DeptName { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>담당자 넣기. 여럿을 한 번에 넣는다.</summary>
public class CompanyUsageManagerAddDto
{
    public string CompanyId { get; set; } = string.Empty;
    public string CodeValue { get; set; } = string.Empty;
    public List<string> AccountIds { get; set; } = [];
}
