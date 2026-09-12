namespace ProjMngServer.Models;

/// <summary>
/// Glue 서비스 정의 한 줄. 파일에서 훑어 쌓은 것이다.
/// </summary>
/// <remarks>
/// 열쇠가 <see cref="ServiceName"/> · <see cref="TransitionName"/> ·
/// <see cref="SrcRid"/> 셋이다. 번호 칸이 없다 — 표를 그렇게 만들었다.
/// </remarks>
public sealed class ActivityInfoRow {

  public string ServiceName { get; set; } = string.Empty;
  public string TransitionName { get; set; } = string.Empty;
  public string? TransitionValue { get; set; }

  /// <summary>자료 접근 객체 이름.</summary>
  public string? Dao { get; set; }

  public string? ProcedureName { get; set; }
  public string? ResultKey { get; set; }
  public string? Activity { get; set; }

  /// <summary><c>sql</c> 이면 <see cref="ActiveContext"/> 에 질의 본문이 있다.</summary>
  public string? ActivityType { get; set; }

  /// <summary>질의 본문. 종류가 <c>proc</c> 이면 비어 있다 — 본문은 DB 에 있다.</summary>
  public string? ActiveContext { get; set; }

  /// <summary>어느 소스에서 훑은 것인가. <b>거르는 열쇠다.</b></summary>
  public string? SrcRid { get; set; }
}
