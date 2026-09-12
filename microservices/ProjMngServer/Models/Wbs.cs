namespace ProjMngServer.Models;

/// <summary>
/// WBS 한 줄. 일정표와 <b>같은 표</b>다 — <see cref="ScheduleType"/> 하나로 갈린다.
/// </summary>
public sealed class WbsItem {

  /// <summary>일감 번호. 표 전체에서 하나다(프로젝트 안에서가 아니다).</summary>
  public int WbsId { get; set; }

  public int? PrjRid { get; set; }

  /// <summary>사람이 붙인 공정 번호. 목록 정렬의 첫 열쇠다.</summary>
  public string? ProcId { get; set; }

  public string? Gb1 { get; set; }
  public string? Gb2 { get; set; }
  public string? ProcNm { get; set; }
  public string? ProcTp { get; set; }
  public string? ProcLvl { get; set; }

  public string? BuildUser { get; set; }
  public string? BuildStatus { get; set; }
  public string? DevUser { get; set; }

  public DateOnly? PlanSdt { get; set; }
  public DateOnly? PlanEdt { get; set; }
  public DateOnly? DevSdt { get; set; }
  public DateOnly? DevEdt { get; set; }

  public string? DevChk { get; set; }
  public string? BuildChk { get; set; }
  public DateOnly? BuildChkDt { get; set; }

  public string? QcUser { get; set; }
  public string? QcChk { get; set; }
  public DateOnly? QcChkDt { get; set; }

  public string? CreUser { get; set; }
  public DateOnly? CreDt { get; set; }
  public string? ModUser { get; set; }
  public DateOnly? ModDt { get; set; }

  public string? Comm { get; set; }

  /// <summary><c>WBS</c> 또는 <c>SCHEDULE</c>. 두 화면을 가르는 값이다.</summary>
  public string? ScheduleType { get; set; }

  /// <summary>계획 기간(일). <b>읽기 전용</b> — 서버가 센다.</summary>
  public int? PlanGap { get; set; }

  /// <summary>
  /// 진행 상태(<c>READY</c> · <c>RUNNING</c> · <c>COMP</c>). <b>읽기 전용</b>.
  /// 개발 시작·종료일로 정해진다 — 따로 적는 칸이 아니다.
  /// </summary>
  public string? WbsState { get; set; }
}

/// <summary>
/// 프로젝트 하나의 WBS 진척 집계. 모니터링 화면의 타일과 차트가 읽는다.
/// </summary>
public sealed class WbsSummary {

  public int TotalTaskCount { get; set; }

  public int CompletedTaskCount { get; set; }
  public decimal CompletedTaskPct { get; set; }

  /// <summary>완료 + 진행 중. 옛 이름은 <c>comp_and_ing_cnt</c> 다.</summary>
  public int CompAndIngCnt { get; set; }

  /// <summary>계획 종료일 안에 끝낸 건수.</summary>
  public int CompletedWithinPlanCount { get; set; }
  public decimal CompletedWithinPlanPct { get; set; }

  /// <summary>계획 종료일이 지났는데 아직 안 끝난 건수.</summary>
  public int DelayedTaskCount { get; set; }
  public decimal DelayedTaskPct { get; set; }

  public int InProgressTaskCount { get; set; }
  public decimal InProgressTaskPct { get; set; }

  public int NotStartedYetTaskCount { get; set; }
  public decimal NotStartedYetPct { get; set; }

  /// <summary>오늘까지 <b>시작</b>하기로 했던 전체 건수.</summary>
  public int PlannedsUntilNowCount { get; set; }
  public decimal PlannedsUntilNowPct { get; set; }

  /// <summary>오늘까지 <b>끝내기로</b> 했던 전체 건수.</summary>
  public int PlannedUntilNowCount { get; set; }
  public decimal PlannedUntilNowPct { get; set; }
}
