namespace JSini.PublicSite.Site;

/// <summary>개인정보처리방침·이용약관에 적는 회사 정보(Legal 설정 구역). 문서 본문은 화면에 있고 바뀌는 값만 설정으로 뺀다. 비어 있는 칸은 줄째 그리지 않는다(모르는 값을 지어 넣지 않는다). 사업자등록번호는 싣지 않는다.</summary>
public sealed class LegalInfo
{
    /// <summary>문서가 「회사」라고 부르는 운영 주체. 법인이면 법적 상호를 적는다.</summary>
    public string OperatorName { get; set; } = "JSINI";

    /// <summary>대표자. 모르면 비워 둔다.</summary>
    public string Representative { get; set; } = string.Empty;

    /// <summary>주소. 모르면 비워 둔다.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>문의·권리 행사를 받는 대표 이메일.</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>개인정보 보호책임자.</summary>
    public PrivacyOfficer Officer { get; set; } = new();

    /// <summary>시행일(<c>yyyy-MM-dd</c>).</summary>
    public string EffectiveDate { get; set; } = string.Empty;
}

/// <summary>개인정보 보호책임자. 이메일은 비면 <see cref="LegalInfo.ContactEmail"/> 을 쓴다.</summary>
public sealed class PrivacyOfficer
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
