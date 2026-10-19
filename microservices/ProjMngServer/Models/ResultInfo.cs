namespace ProjModel;

/// <summary>프로시저 실행 결과(내부 모양). 밖으로 나가는 와이어 모양이 아니다 — 응답은 다른 형식으로 낸다.</summary>
public class ResultInfo<T> : IResultInfo {
  public int? Code { get; set; } = 0;
  public string? Message { get; set; } = "success";
  public IDictionary<string, object?>? Res { get; set; }
  public IDictionary<string, string>? Cols { get; set; }
  public List<T>? Data { get; set; }
  //public IEnumerable<dynamic>? Data2 { get; set; }

  System.Collections.IEnumerable? IResultInfo.Rows => Data;
}

/// <summary>결과 필터가 제네릭 인자와 무관하게 <see cref="ResultInfo{T}"/> 를 읽기 위한 창구.</summary>
public interface IResultInfo {
  int? Code { get; }
  string? Message { get; }
  IDictionary<string, object?>? Res { get; }
  IDictionary<string, string>? Cols { get; }
  System.Collections.IEnumerable? Rows { get; }
}
