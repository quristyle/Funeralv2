using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>와이어에서 받은 행 하나. 컬럼이 고정되지 않아 이름으로 접근한다. DevExpress 그리드는 사전 키를 컬럼으로 못 읽으므로 화면은 <see cref="ProjMngTable"/> 로 옮긴 뒤에 다룬다.</summary>
public sealed class ProjMngRow : Dictionary<string, object?>
{
}

/// <summary>프로시저 호출 결과. 와이어에서는 <c>data.result</c> 안에 <c>{ rows, cols, res, procCode }</c> 모양으로 온다.</summary>
public sealed class ProjMngResult
{
    /// <summary>컬럼 메타. <c>{ 컬럼명: .NET 타입명 }</c>이고 순서가 곧 표시 순서다. 화면이 컬럼을 미리 몰라도 이것으로 그리드를 만든다.</summary>
    [JsonPropertyName("cols")]
    public Dictionary<string, string>? Cols { get; set; }

    [JsonPropertyName("rows")]
    public List<ProjMngRow>? Rows { get; set; }

    /// <summary>실행 시간·파라미터 등 부가 정보.</summary>
    [JsonPropertyName("res")]
    public Dictionary<string, object?>? Res { get; set; }

    /// <summary>프로시저 결과 코드. 0 이상 성공, 음수 실패. 실패는 예외로 바뀌므로 음수가 화면에 닿는 일은 드물다(저장 대상이 없을 때 -77 같은 예외 아닌 경우만 남는다).</summary>
    [JsonPropertyName("procCode")]
    public int ProcCode { get; set; }

    /// <summary>프로시저가 돌려준 안내 문구. 저장·삭제 결과를 사용자에게 보여 줄 때 쓴다.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>빈 결과. 조회 전이거나 실패했을 때 화면이 그대로 그릴 수 있게 한다.</summary>
    public static ProjMngResult Empty { get; } = new() { Cols = [], Rows = [] };
}
