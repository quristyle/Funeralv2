using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 와이어에서 받은 행 하나. <b>컬럼이 고정되어 있지 않아 이름으로 접근한다.</b>
///
/// 프로젝트관리는 화면마다 엔드포인트가 있는 구조가 아니다 — 저장 프로시저
/// 이름을 실어 보내면 그 결과를 그대로 돌려주는 범용 통로다. 업무 로직은 전부
/// DB(<c>projmng</c> 스키마)의 프로시저에 있고, 컬럼도 프로시저가 정한다.
/// 그래서 C# 타입으로 못 박을 수가 없다.
///
/// [이 타입은 와이어에서만 쓴다]
///
/// 화면과 그리드는 이것을 직접 다루지 않는다 — <see cref="ProjMngTable"/> 이
/// <see cref="System.Data.DataTable"/> 로 옮긴 뒤부터가 화면의 몫이다.
/// DevExpress 그리드가 사전의 키를 컬럼으로 인식하지 못하기 때문인데,
/// 자세한 사정은 그 클래스의 주석에 있다.
/// </summary>
public sealed class ProjMngRow : Dictionary<string, object?>
{
}

/// <summary>
/// 프로시저 호출 결과.
///
/// 와이어에서는 표준 봉투의 <c>data.result</c> 안에 이 모양으로 온다:
/// <c>{ rows, cols, res, procCode }</c>.
/// </summary>
public sealed class ProjMngResult
{
    /// <summary>
    /// 컬럼 메타. <c>{ 컬럼명: .NET 타입명 }</c> 이고 <b>순서가 곧 표시 순서다.</b>
    ///
    /// 예: <c>{ cm_cd: "System.String", cm_srt: "System.Int32", cre_dt: "System.DateTime" }</c>
    ///
    /// 화면이 컬럼을 미리 알지 못해도 이 메타로 그리드를 만들 수 있다.
    /// </summary>
    [JsonPropertyName("cols")]
    public Dictionary<string, string>? Cols { get; set; }

    [JsonPropertyName("rows")]
    public List<ProjMngRow>? Rows { get; set; }

    /// <summary>실행 시간·파라미터 등 부가 정보.</summary>
    [JsonPropertyName("res")]
    public Dictionary<string, object?>? Res { get; set; }

    /// <summary>
    /// 프로시저 결과 코드. 0 이상이면 성공, 음수면 실패.
    ///
    /// 실패는 클라이언트가 예외로 바꾸므로 화면에 음수가 도달할 일은 거의 없다 —
    /// 다만 저장할 대상이 없을 때(-77) 처럼 예외가 아닌 경우가 있어 남겨 둔다.
    /// </summary>
    [JsonPropertyName("procCode")]
    public int ProcCode { get; set; }

    /// <summary>
    /// 프로시저가 돌려준 안내 문구. 저장·삭제 결과를 사용자에게 보여 줄 때 쓴다.
    ///
    /// 봉투 바깥(<c>ApiResponse.Message</c>)에도 메시지가 있지만 그건 HTTP 층의
    /// 것이다. 업무 결과를 말하는 것은 이쪽이라 프로시저 결과와 함께 둔다.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>빈 결과. 조회 전이거나 실패했을 때 화면이 그대로 그릴 수 있게 한다.</summary>
    public static ProjMngResult Empty { get; } = new() { Cols = [], Rows = [] };
}
