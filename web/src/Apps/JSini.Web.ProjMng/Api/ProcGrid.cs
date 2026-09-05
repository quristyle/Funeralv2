using System.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로시저 하나에 붙는 그리드의 조회·저장·삭제를 묶어 둔 것.
/// Vue 의 <c>useProcGrid</c> 를 옮겼다.
///
/// 프로젝트관리 화면은 대부분 같은 모양이다.
///   ① 조건을 골라 프로시저를 조회한다
///   ② 그리드에서 행을 편집한다
///   ③ 변경된 행만 같은 프로시저에 <c>save</c> 로 보낸다
///   ④ 삭제는 같은 프로시저에 <c>delete</c> 로 보낸다
///
/// 옛 Blazor 에서는 화면마다 이 넷을 손으로 적었다. 여기 한 번만 두면 화면은
/// 프로시저 이름과 조건만 넘기면 된다 — 화면 27개가 그만큼 얇아진다.
///
/// 화면마다 하나씩 만든다. 마스터-디테일 화면은 둘을 만든다.
/// </summary>
public sealed class ProcGrid(ProjMngClient client, string procName)
{
    /// <summary>마지막 조회 조건. 저장 뒤 같은 조건으로 다시 읽으려고 기억한다.</summary>
    private IReadOnlyDictionary<string, object?>? _lastParameters;

    /// <summary>그리드에 넘길 표. 조회할 때마다 새로 만든다.</summary>
    public ProjMngTable Data { get; private set; } = ProjMngTable.Empty;

    public bool IsLoading { get; private set; }

    /// <summary>마지막 호출의 프로시저 메시지. 화면이 안내로 쓴다.</summary>
    public string? LastMessage { get; private set; }

    /// <summary><c>/Proj/sys</c> 로 보낸다 — 서버 캐시를 타지 않아야 하는 조회.</summary>
    public bool IsServerFix { get; init; }

    /// <summary>프로젝트에 등록된 외부 DB 로 붙는다.</summary>
    public bool IsProjDb { get; init; }

    /// <summary>
    /// 저장·삭제 뒤 다시 조회할지. 서버가 채운 키·순번을 보려면 켜 둔다(기본값).
    /// </summary>
    public bool ReloadAfterWrite { get; init; } = true;

    /// <summary>조회한다. 조건을 주지 않으면 마지막 조건을 다시 쓴다.</summary>
    public async Task LoadAsync(
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (parameters is not null)
        {
            _lastParameters = parameters;
        }

        IsLoading = true;
        try
        {
            var result = await client.DbContAsync(
                procName, _lastParameters,
                isServerFix: IsServerFix, isProjDb: IsProjDb,
                cancellationToken: cancellationToken);

            Data = ProjMngTable.From(result);
            LastMessage = result.Message;
        }
        finally
        {
            // finally 로 감싸는 이유: 조회가 실패해도 로딩 표시는 반드시 꺼야 한다.
            // 안 그러면 화면이 영원히 회전판을 돌린 채 멈춘 것처럼 보인다.
            IsLoading = false;
        }
    }

    /// <summary>마지막 조건으로 다시 조회한다.</summary>
    public Task ReloadAsync(CancellationToken cancellationToken = default)
        => LoadAsync(cancellationToken: cancellationToken);

    /// <summary>
    /// 추가·수정된 행만 저장한다.
    /// </summary>
    /// <param name="extraParameters">
    /// 조회 조건과 별도로 넘길 파라미터. 마스터-디테일에서 부모 키를 넘길 때 쓴다.
    /// </param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>
    /// 저장할 대상이 없으면 <c>ProcCode = -77</c>. 예외가 아니다 — 아무것도
    /// 안 고치고 저장을 누른 것은 오류가 아니라 안내 대상이다.
    /// </returns>
    public async Task<ProjMngResult> SaveAsync(
        IReadOnlyDictionary<string, object?>? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        var changed = Data.ChangedRows();
        if (changed.Count == 0)
        {
            LastMessage = "수정 대상이 없습니다.";
            return new ProjMngResult { ProcCode = -77, Cols = [], Rows = [] };
        }

        var saved = await client.DbSaveAsync(
            procName, changed, Merge(_lastParameters, extraParameters), cancellationToken);

        LastMessage = saved.Message;

        if (saved.ProcCode >= 0)
        {
            if (ReloadAfterWrite)
            {
                await ReloadAsync(cancellationToken);
            }
            else
            {
                // 다시 조회하지 않으면 표가 변경 상태로 남는다.
                // 안 지우면 다음 저장 때 같은 행이 또 나간다.
                Data.AcceptChanges();
            }
        }

        return saved;
    }

    /// <summary>행 하나를 지운다.</summary>
    public async Task<ProjMngResult> DeleteAsync(
        DataRow row,
        CancellationToken cancellationToken = default)
    {
        var parameters = Merge(_lastParameters, ToDictionary(row));

        var deleted = await client.DbDeleteAsync(procName, parameters, cancellationToken);
        LastMessage = deleted.Message;

        if (deleted.ProcCode >= 0 && ReloadAfterWrite)
        {
            await ReloadAsync(cancellationToken);
        }

        return deleted;
    }

    /// <summary>결과를 비운다. 마스터 선택이 풀렸을 때 디테일을 지우는 데 쓴다.</summary>
    public void Clear()
    {
        Data = ProjMngTable.Empty;
        LastMessage = null;
    }

    private static Dictionary<string, object?> ToDictionary(DataRow row)
    {
        var values = new Dictionary<string, object?>();
        foreach (DataColumn column in row.Table.Columns)
        {
            var value = row[column];
            values[column.ColumnName] = value == DBNull.Value ? null : value;
        }
        return values;
    }

    private static Dictionary<string, object?> Merge(
        IReadOnlyDictionary<string, object?>? first,
        IReadOnlyDictionary<string, object?>? second)
    {
        var merged = new Dictionary<string, object?>();

        if (first is not null)
        {
            foreach (var (key, value) in first)
            {
                merged[key] = value;
            }
        }

        if (second is not null)
        {
            foreach (var (key, value) in second)
            {
                merged[key] = value;
            }
        }

        return merged;
    }
}
