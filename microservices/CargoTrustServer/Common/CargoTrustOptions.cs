namespace CargoTrustServer.Common;

/// <summary>설정 <c>CargoTrust:*</c></summary>
public class CargoTrustOptions
{
    public const string Section = "CargoTrust";

    /// <summary>
    /// 이 역할 중 하나가 X-User-Roles 에 있으면 관리자다. 기본값을 초기값으로 두지 않는다 — 배열 바인딩은 초기값 뒤에 덧붙여서 설정으로 목록을 줄일 수 없다.
    /// </summary>
    public string[] AdminRoles { get; set; } = [];

    /// <summary>설정이 비었을 때의 관리자 역할.</summary>
    public static readonly string[] DefaultAdminRoles = ["ADMINISTRATOR", "SYSTEM_ADMINISTRATOR"];

    /// <summary>실제로 쓰는 관리자 역할.</summary>
    public string[] EffectiveAdminRoles => AdminRoles.Length > 0 ? AdminRoles : DefaultAdminRoles;

    /// <summary>목록 상한. 서버 페이징을 쓰지 않는 대신 둔다.</summary>
    public int ListLimit { get; set; } = 500;
}
