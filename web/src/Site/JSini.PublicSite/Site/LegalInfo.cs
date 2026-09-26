namespace JSini.PublicSite.Site;

/// <summary>
/// 개인정보처리방침 · 이용약관에 적는 <b>회사 정보</b> (<c>Legal</c> 설정 구역).
/// </summary>
/// <remarks>
/// <para>
/// 문서 본문은 화면(<c>Privacy.razor</c> · <c>Terms.razor</c>)에 있고, 사람마다·때마다
/// 바뀌는 값만 여기로 뺐다. 담당자가 바뀌면 코드가 아니라 설정 한 줄을 고친다.
/// </para>
/// <para>
/// <b>비어 있는 칸은 화면에 줄째 그리지 않는다.</b> 모르는 값을 지어 넣지 않으려는
/// 것이다 — 법정 문서에 틀린 이름·주소가 적히는 것이 빈칸보다 나쁘다.
/// 사업자등록번호는 싣지 않기로 했다(2026-09-27 결정).
/// </para>
/// </remarks>
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
