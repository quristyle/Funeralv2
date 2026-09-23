namespace ProjMngServer.Models;

// EAI 인터페이스 카탈로그 — `projmng.if_*` 일곱 표와 뷰 넷.
//
// **조회는 뷰로, 편집은 원본 표로** 한다. 뷰가 코드값을 이름으로 풀어 주고
// (방향·상태·주기) 단계·메모 건수까지 세어 주기 때문에 목록 화면이 조인을
// 다시 짤 일이 없다. 갱신시각은 **DB 트리거가 찍는다** — 어느 길로 고쳐도
// 빠지지 않는다.

/// <summary>공통코드 한 줄. <b>프로젝트를 가리지 않는다.</b></summary>
public sealed class IfCodeRow
{
    public string? CodeGrp { get; set; }
    public string? Code { get; set; }
    public string? CodeNm { get; set; }
    public string? CodeDesc { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>연계 대상 시스템. <b>비밀번호는 담지 않는다.</b></summary>
public sealed class IfSystemRow
{
    public int SystemId { get; set; }
    public string? SystemCd { get; set; }
    public string? SystemNm { get; set; }
    public string? SystemKind { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? DbNm { get; set; }
    public string? SchemaNm { get; set; }
    public string? SystemDesc { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>목록 한 줄 — <c>v_if_master</c> + <c>v_if_flow</c>.</summary>
public sealed class IfMasterRow
{
    public int IfId { get; set; }
    public string? IfCd { get; set; }
    public string? IfNm { get; set; }

    public string? DomainCd { get; set; }
    public string? DirectionCd { get; set; }
    public string? DirectionNm { get; set; }

    public string? SrcSystemNm { get; set; }
    public string? TgtSystemNm { get; set; }

    public string? StatusCd { get; set; }
    public string? StatusNm { get; set; }
    public string? CycleCd { get; set; }
    public string? CycleNm { get; set; }

    public string? OwnerNm { get; set; }

    public string? PlanSdt { get; set; }
    public string? PlanEdt { get; set; }
    public string? OpenDt { get; set; }

    public int StepCnt { get; set; }
    public int NoteCnt { get; set; }

    /// <summary>안 끝난 이슈(<c>note_type_cd = 'ISSUE'</c> 이고 <c>done_yn = 'N'</c>).</summary>
    public int OpenIssueCnt { get; set; }

    public string? Remark { get; set; }
    public string? UseYn { get; set; }
    public int SortOrder { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// 처리 단계를 한 줄로 이어 붙인 글자
    /// (<c>1. EAI.T_IF  →  2. HHIP.SP_LOAD</c>). 목록에서 흐름이 바로 보인다.
    /// </summary>
    public string? Flow { get; set; }
}

/// <summary>상세의 기본정보 — 편집하는 원본 표 그대로다.</summary>
public sealed class IfMasterDetail
{
    public int IfId { get; set; }
    public string? IfCd { get; set; }
    public string? IfNm { get; set; }
    public string? IfDesc { get; set; }

    public string? DirectionCd { get; set; }
    public int? SrcSystemId { get; set; }
    public int? TgtSystemId { get; set; }
    public string? DomainCd { get; set; }
    public string? TriggerCd { get; set; }
    public string? CycleCd { get; set; }
    public string? StatusCd { get; set; }

    public string? OwnerNm { get; set; }
    public string? OwnerBpId { get; set; }

    public string? PlanSdt { get; set; }
    public string? PlanEdt { get; set; }
    public string? OpenDt { get; set; }

    public string? Remark { get; set; }

    /// <summary>덧붙이는 값. 글자로 내보낸다 — 화면이 그대로 보여 주고 그대로 돌려준다.</summary>
    public string? Ext { get; set; }

    public int SortOrder { get; set; }
    public string? UseYn { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>처리 단계 한 줄 — <c>v_if_step</c>.</summary>
public sealed class IfStepRow
{
    public int StepId { get; set; }
    public int IfId { get; set; }

    /// <summary>차례. <b>인터페이스 안에서 겹칠 수 없다</b>(유일 제약).</summary>
    public int StepNo { get; set; }

    public string? StepNm { get; set; }
    public string? StepTypeCd { get; set; }
    public string? StepTypeNm { get; set; }
    public string? SystemNm { get; set; }

    public string? ObjectOwner { get; set; }
    public string? ObjectNm { get; set; }

    /// <summary><c>소유자.이름</c>. 소유자가 없으면 이름만.</summary>
    public string? ObjectFullNm { get; set; }

    public string? ObjectType { get; set; }
    public string? StepDesc { get; set; }
    public string? Params { get; set; }
    public string? Ext { get; set; }
    public string? UseYn { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>메모 · 이슈 · 변경이력 한 줄.</summary>
public sealed class IfNoteRow
{
    public int NoteId { get; set; }
    public int IfId { get; set; }

    /// <summary>채우면 그 단계에 붙는다. 비면 인터페이스 전체에 붙는다.</summary>
    public int? StepId { get; set; }

    public string? StepNm { get; set; }
    public string? NoteTypeCd { get; set; }
    public string? NoteTypeNm { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? WriterNm { get; set; }
    public string? NoteDt { get; set; }

    /// <summary><c>Y</c>/<c>N</c>. 이슈이면서 <c>N</c> 인 것이 미해결로 집계된다.</summary>
    public string? DoneYn { get; set; }

    public int SortOrder { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>추가 관리항목 한 칸 — <c>v_if_attr</c>.</summary>
/// <remarks>
/// 정의 × 인터페이스를 모두 펼친 것이라 <b>값이 없는 항목도 한 줄로 나온다.</b>
/// 화면은 이 목록만으로 입력 칸을 만든다.
/// </remarks>
public sealed class IfAttrRow
{
    public int AttrDefId { get; set; }
    public string? AttrCd { get; set; }
    public string? AttrNm { get; set; }

    /// <summary><c>TEXT</c>·<c>NUMBER</c>·<c>DATE</c>·<c>YN</c>·<c>CODE</c>·<c>JSON</c>.</summary>
    public string? AttrType { get; set; }

    /// <summary><c>AttrType</c> 이 <c>CODE</c> 일 때 고를 코드 묶음.</summary>
    public string? CodeGrp { get; set; }

    public string? RequiredYn { get; set; }
    public string? AttrVal { get; set; }
    public int SortOrder { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>첨부 한 개.</summary>
public sealed class IfFileRow
{
    public string? FileId { get; set; }
    public string? Name { get; set; }
    public long Size { get; set; }
    public string? Modified { get; set; }
}

/// <summary>인터페이스별 첨부 개수. 목록의 배지가 읽는다.</summary>
public sealed class IfFileCount
{
    public int IfId { get; set; }
    public int Cnt { get; set; }
}

/// <summary>상세 한 벌. 화면이 한 번에 받는다.</summary>
public sealed class IfDetail
{
    public IfMasterDetail? Master { get; set; }
    public List<IfStepRow> Steps { get; set; } = [];
    public List<IfAttrRow> Attrs { get; set; } = [];
    public List<IfNoteRow> Notes { get; set; } = [];

    /// <inheritdoc cref="IfMasterRow.Flow"/>
    public string? Flow { get; set; }
}
