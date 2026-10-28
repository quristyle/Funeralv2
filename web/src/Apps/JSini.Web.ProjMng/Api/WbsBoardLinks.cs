using System.Text.RegularExpressions;

namespace JSini.Web.ProjMng.Api;

/// <summary>상세 목록의 한 줄에서 바깥 화면으로 가는 주소. 고객사 사내망 주소라 그 망 안에서만 열린다 — 주소는 목록에 이미 있는 값(메뉴명의 [코드] · 모듈 · Activity ID)으로 만든다.</summary>
public static partial class WbsBoardLinks
{
    /// <summary>Activity ID 가 아직 없는 줄의 임시 번호 앞머리. 서버(WbsBoardSql.NoIdPrefix)와 같아야 한다.</summary>
    public const string NoIdPrefix = "NOID-";

    /// <summary>운영 화면 실행기.</summary>
    public const string OpsBase = "https://dev-isos-hhip.hd.com/launcher";

    /// <summary>ProjectView.</summary>
    public const string PvBase = "https://dev-wbs.hd.com/projectview";

    /// <summary>개발 PC 의 화면 포트는 3200 + 모듈 번호다(PMM004 → 3204).</summary>
    private const int FePortBase = 3200;

    private const string ScreenPrefix = "MM";

    [GeneratedRegex(@"^\s*\[\s*([0-9A-Za-z_-]+)\s*\]")]
    private static partial Regex ScreenCode();

    [GeneratedRegex(@"(\d+)\s*$")]
    private static partial Regex TrailingNumber();

    /// <summary>메뉴명 앞머리의 화면코드(「[12C0] 운송비 관리」 → 12C0).</summary>
    public static string? ScreenCodeOf(string? menuNm)
        => ScreenCode().Match(menuNm ?? "") is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>운영 화면 주소. 코드나 모듈을 못 읽으면 null.</summary>
    public static string? OpsUrl(WbsBoardRowDto r)
    {
        var code = ScreenCodeOf(r.MenuNm);
        var sys = r.Systemcode?.Trim();

        return code is null || string.IsNullOrEmpty(sys)
            ? null
            : $"{OpsBase}/{sys.ToLowerInvariant()}/{ScreenPrefix}{code}";
    }

    /// <summary>개발 PC(localhost) 화면 주소. 모듈 번호가 없으면 null.</summary>
    public static string? DevUrl(WbsBoardRowDto r)
    {
        var code = ScreenCodeOf(r.MenuNm);
        var sys = r.Systemcode?.Trim() ?? "";

        if (code is null || TrailingNumber().Match(sys) is not { Success: true } m) return null;
        if (!int.TryParse(m.Groups[1].Value, out var no) || no <= 0) return null;

        return $"http://localhost:{FePortBase + no}/#/{(ScreenPrefix + code).ToLowerInvariant()}";
    }

    /// <summary>코드 앞자리가 projectKey 다(AAC-A189 → AAC).</summary>
    private static string KeyOf(string code) => code.Split('-')[0];

    /// <summary>ProjectView 작업(액티비티) 상세.</summary>
    public static string? PvWorkUrl(WbsBoardRowDto r)
        => string.IsNullOrWhiteSpace(r.ActivityId) || r.NoId
            ? null
            : $"{PvBase}/#/PGE_PSW_WorkDetailPage?projectKey={Uri.EscapeDataString(KeyOf(r.ActivityId))}&activityId={Uri.EscapeDataString(r.ActivityId)}";

    /// <summary>ProjectView 일감 상세. 일감 코드는 PV 캐시에서 온다.</summary>
    public static string? PvTaskUrl(WbsBoardRowDto r)
        => string.IsNullOrWhiteSpace(r.PvTaskCode)
            ? null
            : $"{PvBase}/#/PGE_PST_TaskDetailPage?projectKey={Uri.EscapeDataString(KeyOf(r.PvTaskCode))}&taskId={Uri.EscapeDataString(r.PvTaskCode)}";

    /// <summary>ProjectView 단위테스트 상세. 코드는 원장(pv_test_case_id)에 있다.</summary>
    public static string? PvTestUrl(WbsBoardRowDto r)
        => string.IsNullOrWhiteSpace(r.PvTestCaseId)
            ? null
            : $"{PvBase}/#/PGE_PSU_TestDetailPage?projectKey={Uri.EscapeDataString(KeyOf(r.PvTestCaseId))}&testCaseId={Uri.EscapeDataString(r.PvTestCaseId)}";

    /// <summary>현재단계를 ProjectView 와 같은 「(90%) PI검토」 모양으로.</summary>
    public static string StageText(WbsBoardRowDto r)
        => string.IsNullOrEmpty(r.PvStatus)
            ? ""
            : (r.PvFinishRate is { } rate ? $"({rate:0.#}%) " : "") + r.PvStatus;

    /// <summary>현재단계 색 — 완료 초록 · 검토 주황 · 개발 파랑 · 그 밖 회색.</summary>
    public static string StageClass(string? status) => status switch
    {
        null or "" => "none",
        var s when s.Contains("완료") => "done",
        var s when s.Contains("검토") => "review",
        var s when s.Contains("개발") => "dev",
        _ => "todo",
    };

    /// <summary>트리거 발화 — 없음 · 확인필요 · 실제 발화로 갈라 색을 다르게 한다.</summary>
    public static string TriggerClass(string? v) => v switch
    {
        null or "" => "none",
        var s when s.Contains("없음") && !s.Contains("배치") => "none",
        var s when s.Contains("미확인") || s.Contains("확인필요") => "warn",
        _ => "fire",
    };
}
