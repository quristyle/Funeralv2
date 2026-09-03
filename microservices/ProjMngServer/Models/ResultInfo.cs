namespace ProjModel;

/// <summary>
/// 프로시저 실행 결과 (ProjMngWasm 시절의 내부 모양).
/// </summary>
/// <remarks>
/// 예전에는 이것이 **그대로 와이어로 나갔다** — `{ code, message, res, cols, data }`,
/// 숫자 code 에 음수면 실패. 2026-09-04(D-A1)부터는 밖으로 나가지 않는다.
/// <see cref="ProjMngServer.Filters.ApiEnvelopeResultFilter"/> 가 다른 여섯 서비스와
/// 같은 표준 봉투(<c>ApiResponse</c>)로 감싼다. 서비스·프로시저 쪽 규약(음수 코드)은
/// 그대로다 — 바뀐 것은 직렬화 경계뿐이다.
/// </remarks>
public class ResultInfo<T> : IResultInfo {
  public int? Code { get; set; } = 0;
  public string? Message { get; set; } = "success";
  public IDictionary<string, object>? Res { get; set; }
  public IDictionary<string, string>? Cols { get; set; }
  public List<T>? Data { get; set; }
  //public IEnumerable<dynamic>? Data2 { get; set; }

  System.Collections.IEnumerable? IResultInfo.Rows => Data;
}

/// <summary>
/// 결과 필터가 제네릭 인자와 무관하게 <see cref="ResultInfo{T}"/> 를 읽기 위한 창구.
/// </summary>
public interface IResultInfo {
  int? Code { get; }
  string? Message { get; }
  IDictionary<string, object>? Res { get; }
  IDictionary<string, string>? Cols { get; }
  System.Collections.IEnumerable? Rows { get; }
}
