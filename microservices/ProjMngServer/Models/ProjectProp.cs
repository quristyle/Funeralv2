namespace ProjMngServer.Models;

/// <summary>
/// 프로젝트에 딸린 속성 한 줄. 그림·문서처럼 <b>프로젝트 단위로 하나씩</b>
/// 저장하는 것들이 여기 들어온다(유스케이스 도형 등).
/// </summary>
/// <remarks>
/// 열쇠가 셋이다 — <see cref="PrjRid"/> · <see cref="PropCd"/> ·
/// <see cref="PropType"/>. 번호 칸이 없다.
/// </remarks>
public sealed class ProjectProp {

  /// <summary>프로젝트 번호. <b>글자다</b> — 표가 그렇게 생겼다.</summary>
  public string? PrjRid { get; set; }

  /// <summary>속성 이름. 사람이 붙인 이름이 그대로 열쇠다.</summary>
  public string? PropCd { get; set; }

  /// <summary>속성 값. 도형 JSON 처럼 긴 글이 들어온다.</summary>
  public string? PropVal { get; set; }

  public string? PropComm { get; set; }

  public string? PropUseYn { get; set; }

  /// <summary>속성 갈래(<c>USE_CASE</c> …). 같은 이름이라도 갈래가 다르면 다른 줄이다.</summary>
  public string? PropType { get; set; }
}
