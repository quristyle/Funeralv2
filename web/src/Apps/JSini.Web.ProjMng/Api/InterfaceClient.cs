using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// EAI 인터페이스 카탈로그 — <c>projmng/interfaces</c>.
/// </summary>
/// <remarks>
/// 목록·단계는 <b>뷰</b>에서 오므로 코드값이 이미 이름으로 풀려 있고 단계·메모
/// 건수도 세어져 있다. 고칠 때만 원본 표로 간다.
/// </remarks>
public sealed class InterfaceClient(GatewayClient gateway)
{
    private const string Url = "projmng/interfaces";

    // ──────────────────────────────────────────── 코드 · 시스템

    /// <summary>
    /// 선택목록. <b>이것이 비면 화면의 드롭다운이 전부 빈다</b> —
    /// 표만 만들고 코드 자료를 안 넣으면 그렇게 된다.
    /// </summary>
    public Task<IReadOnlyList<IfCodeDto>> CodesAsync(
        string? grp = null, CancellationToken ct = default)
        => gateway.GetListAsync<IfCodeDto>(
            string.IsNullOrWhiteSpace(grp) ? $"{Url}/codes" : $"{Url}/codes?grp={Uri.EscapeDataString(grp)}", ct);

    public Task<IReadOnlyList<IfSystemDto>> SystemsAsync(int prjRid, CancellationToken ct = default)
        => gateway.GetListAsync<IfSystemDto>($"{Url}/systems?prjRid={prjRid}", ct);

    // ──────────────────────────────────────────── 목록 · 상세

    public Task<IReadOnlyList<IfMasterDto>> ListAsync(
        int prjRid, string? status = null, string? domain = null,
        string? q = null, string? useYn = null, CancellationToken ct = default)
    {
        var query = new List<string> { $"prjRid={prjRid}" };

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) query.Add($"{key}={Uri.EscapeDataString(value)}");
        }

        Add("status", status);
        Add("domain", domain);
        Add("q", q);
        Add("useYn", useYn);

        return gateway.GetListAsync<IfMasterDto>($"{Url}?{string.Join('&', query)}", ct);
    }

    public Task<IfDetailDto?> DetailAsync(int prjRid, int ifId, CancellationToken ct = default)
        => gateway.GetOneAsync<IfDetailDto>($"{Url}/{ifId}?prjRid={prjRid}", ct);

    // ──────────────────────────────────────────── 기본정보

    public Task CreateAsync(
        int prjRid, IReadOnlyDictionary<string, object?> body, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}?prjRid={prjRid}", body, ct);

    public Task UpdateAsync(
        int prjRid, int ifId, IReadOnlyDictionary<string, object?> body, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{ifId}?prjRid={prjRid}", body, ct);

    public Task DeleteAsync(int prjRid, int ifId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{ifId}?prjRid={prjRid}", ct);

    // ──────────────────────────────────────────── 처리 단계

    public Task<IReadOnlyList<IfStepDto>> StepsAsync(
        int prjRid, int ifId, CancellationToken ct = default)
        => gateway.GetListAsync<IfStepDto>($"{Url}/{ifId}/steps?prjRid={prjRid}", ct);

    public Task CreateStepAsync(
        int prjRid, int ifId, IReadOnlyDictionary<string, object?> body, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}/{ifId}/steps?prjRid={prjRid}", body, ct);

    public Task UpdateStepAsync(
        int prjRid, int ifId, int stepId, IReadOnlyDictionary<string, object?> body,
        CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{ifId}/steps/{stepId}?prjRid={prjRid}", body, ct);

    public Task DeleteStepAsync(int prjRid, int ifId, int stepId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{ifId}/steps/{stepId}?prjRid={prjRid}", ct);

    /// <summary>차례를 통째로 다시 매긴다. <paramref name="order"/> 는 <b>바뀐 차례대로</b>의 단계 번호다.</summary>
    public Task ReorderStepsAsync(
        int prjRid, int ifId, IReadOnlyList<int> order, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{ifId}/steps/reorder?prjRid={prjRid}", order, ct);

    // ──────────────────────────────────────────── 메모 · 이슈

    public Task<IReadOnlyList<IfNoteDto>> NotesAsync(
        int prjRid, int ifId, CancellationToken ct = default)
        => gateway.GetListAsync<IfNoteDto>($"{Url}/{ifId}/notes?prjRid={prjRid}", ct);

    public Task CreateNoteAsync(
        int prjRid, int ifId, IReadOnlyDictionary<string, object?> body, CancellationToken ct = default)
        => gateway.PostAsync($"{Url}/{ifId}/notes?prjRid={prjRid}", body, ct);

    public Task UpdateNoteAsync(
        int prjRid, int ifId, int noteId, IReadOnlyDictionary<string, object?> body,
        CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{ifId}/notes/{noteId}?prjRid={prjRid}", body, ct);

    public Task DeleteNoteAsync(int prjRid, int ifId, int noteId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{ifId}/notes/{noteId}?prjRid={prjRid}", ct);

    // ──────────────────────────────────────────── 추가 관리항목

    public Task<IReadOnlyList<IfAttrDto>> AttrsAsync(
        int prjRid, int ifId, CancellationToken ct = default)
        => gateway.GetListAsync<IfAttrDto>($"{Url}/{ifId}/attrs?prjRid={prjRid}", ct);

    /// <summary>항목값 한꺼번에 담기. 열쇠가 <see cref="IfAttrDto.AttrDefId"/> 다.</summary>
    public Task SaveAttrsAsync(
        int prjRid, int ifId, IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
        => gateway.PutAsync($"{Url}/{ifId}/attrs?prjRid={prjRid}", values, ct);

    // ──────────────────────────────────────────── 첨부

    public Task<IReadOnlyList<IfFileCountDto>> FileCountsAsync(
        int prjRid, CancellationToken ct = default)
        => gateway.GetListAsync<IfFileCountDto>($"{Url}/files/counts?prjRid={prjRid}", ct);

    public Task<IReadOnlyList<IfFileDto>> FilesAsync(
        int prjRid, int ifId, CancellationToken ct = default)
        => gateway.GetListAsync<IfFileDto>($"{Url}/{ifId}/files?prjRid={prjRid}", ct);

    public Task DeleteFileAsync(int prjRid, int ifId, string fileId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Url}/{ifId}/files/{Uri.EscapeDataString(fileId)}?prjRid={prjRid}", ct);

    /// <summary>내려받기 주소. 셸의 중계를 거치지 않고 게이트웨이로 바로 간다.</summary>
    public static string DownloadPath(int prjRid, int ifId, string fileId)
        => $"{Url}/{ifId}/files/{Uri.EscapeDataString(fileId)}?prjRid={prjRid}";
}

/// <summary>인터페이스 공통코드 한 줄.</summary>
public sealed class IfCodeDto
{
    public string? CodeGrp { get; set; }
    public string? Code { get; set; }
    public string? CodeNm { get; set; }
    public string? CodeDesc { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>연계 대상 시스템.</summary>
public sealed class IfSystemDto
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

/// <summary>목록 한 줄.</summary>
public sealed class IfMasterDto
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

    /// <summary>안 끝난 이슈. 0 보다 크면 목록에서 표시가 붙는다.</summary>
    public int OpenIssueCnt { get; set; }

    public string? Remark { get; set; }
    public string? UseYn { get; set; }
    public int SortOrder { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>처리 단계를 한 줄로 이은 글자. 목록에서 흐름이 바로 보인다.</summary>
    public string? Flow { get; set; }

    /// <summary>첨부 개수. <b>서버가 주는 값이 아니다</b> — 화면이 따로 받아 채운다.</summary>
    public int FileCnt { get; set; }
}

/// <summary>상세의 기본정보.</summary>
public sealed class IfMasterDetailDto
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
    public string? Ext { get; set; }
    public int SortOrder { get; set; }
    public string? UseYn { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>처리 단계 한 줄.</summary>
public sealed class IfStepDto
{
    public int StepId { get; set; }
    public int IfId { get; set; }

    /// <summary>차례. <b>인터페이스 안에서 겹칠 수 없다.</b></summary>
    public int StepNo { get; set; }

    public string? StepNm { get; set; }
    public string? StepTypeCd { get; set; }
    public string? StepTypeNm { get; set; }
    public string? SystemNm { get; set; }
    public string? ObjectOwner { get; set; }
    public string? ObjectNm { get; set; }
    public string? ObjectFullNm { get; set; }
    public string? ObjectType { get; set; }
    public string? StepDesc { get; set; }
    public string? Params { get; set; }
    public string? Ext { get; set; }
    public string? UseYn { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>메모 · 이슈 한 줄.</summary>
public sealed class IfNoteDto
{
    public int NoteId { get; set; }
    public int IfId { get; set; }

    /// <summary>채우면 그 단계에 붙는다.</summary>
    public int? StepId { get; set; }

    public string? StepNm { get; set; }
    public string? NoteTypeCd { get; set; }
    public string? NoteTypeNm { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? WriterNm { get; set; }
    public string? NoteDt { get; set; }

    /// <summary><c>Y</c>/<c>N</c>. 이슈이면서 <c>N</c> 인 것이 미해결로 잡힌다.</summary>
    public string? DoneYn { get; set; }

    public int SortOrder { get; set; }
    public string? UpdatedAt { get; set; }

    /// <summary>
    /// 화면에서 켜고 끄는 값. DB 는 <c>Y</c>/<c>N</c> 글자라 그 사이를 잇는다 —
    /// 편집 폼은 <c>@bind-</c> 로만 묶을 수 있고 확인칸은 참·거짓을 받는다.
    /// </summary>
    public bool Done
    {
        get => DoneYn == "Y";
        set => DoneYn = value ? "Y" : "N";
    }
}

/// <summary>추가 관리항목 한 칸.</summary>
/// <remarks>
/// 값이 없는 항목도 <b>한 줄로 온다</b>(정의 × 인터페이스를 펼친 뷰다).
/// 화면은 이 목록만으로 입력 칸을 만든다.
/// </remarks>
public sealed class IfAttrDto
{
    public int AttrDefId { get; set; }
    public string? AttrCd { get; set; }
    public string? AttrNm { get; set; }

    /// <summary><c>TEXT</c>·<c>NUMBER</c>·<c>DATE</c>·<c>YN</c>·<c>CODE</c>·<c>JSON</c>.</summary>
    public string? AttrType { get; set; }

    public string? CodeGrp { get; set; }
    public string? RequiredYn { get; set; }
    public string? AttrVal { get; set; }
    public int SortOrder { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>첨부 한 개.</summary>
public sealed class IfFileDto
{
    public string? FileId { get; set; }
    public string? Name { get; set; }
    public long Size { get; set; }
    public string? Modified { get; set; }
}

/// <summary>인터페이스별 첨부 개수.</summary>
public sealed class IfFileCountDto
{
    public int IfId { get; set; }
    public int Cnt { get; set; }
}

/// <summary>상세 한 벌.</summary>
public sealed class IfDetailDto
{
    public IfMasterDetailDto? Master { get; set; }
    public List<IfStepDto> Steps { get; set; } = [];
    public List<IfAttrDto> Attrs { get; set; } = [];
    public List<IfNoteDto> Notes { get; set; } = [];
    public string? Flow { get; set; }
}
