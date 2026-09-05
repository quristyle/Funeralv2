using JSini.Web.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// ProjMngServer 호출. 게이트웨이의 <c>/projmng</c> 아래로 나간다.
///
/// [엔드포인트가 화면 수만큼 있지 않다]
///
/// 이 서비스는 <b>저장 프로시저 이름을 실어 보내면 그 결과를 그대로 돌려주는
/// 범용 통로</b>다. 업무 로직은 전부 DB 의 프로시저에 있다. 그래서 프론트가
/// 알아야 할 것은 둘뿐이다 — 어떤 프로시저를 어떤 파라미터로 부르는가,
/// 그리고 결과의 컬럼 메타를 어떻게 그리는가.
///
/// 옛 Blazor(<c>ProjMngWasm</c>)의 <c>BaseComponent</c> 헬퍼들과 Vue 의
/// <c>api/projmng/proc.ts</c> 가 같은 일을 했다. 이름을 그대로 이어받는다.
/// </summary>
public sealed class ProjMngClient(GatewayClient gateway, ILogger<ProjMngClient> logger)
{
    /// <summary>게이트웨이가 이 서비스로 라우팅하는 접두사.</summary>
    private const string Prefix = "projmng";

    /// <summary>업무 프로시저 (<c>sp_*</c>) 와 다건 저장.</summary>
    private const string ProjUrl = $"{Prefix}/Proj";

    /// <summary>캐시를 타지 않아야 하는 시스템 조회.</summary>
    private const string ProjSysUrl = $"{Prefix}/Proj/sys";

    /// <summary>개발 도구 — 프로젝트 DB 메타 조회.</summary>
    private const string DevUrl = $"{Prefix}/Dev";

    /// <summary>
    /// 생 SQL 실행 통로. 등록된 액션이 아니라 <b>보낸 문장을 그대로</b> 돌린다.
    ///
    /// 봉투가 다른 것들과 달라서 전용 주소를 쓴다 — 여기는 액션 이름이 아니라
    /// <c>{ db_nick, query }</c> 를 받는다.
    /// </summary>
    private const string DevSqlUrl = $"{Prefix}/Dev/sql";

    /// <summary>서버측 파일 스캔 (<c>md_*</c>).</summary>
    private const string MediaUrl = $"{Prefix}/Media";

    /// <summary>
    /// 조회.
    /// </summary>
    /// <param name="procName">프로시저 이름. <c>sp_</c> 로 시작해야 한다.</param>
    /// <param name="parameters">프로시저 파라미터</param>
    /// <param name="procType">프로시저가 <c>req_type</c> 으로 받는 값</param>
    /// <param name="isServerFix">
    /// 켜면 <c>/Proj/sys</c> 로 보낸다. 서버 캐시를 타지 않아야 하는 조회다.
    /// </param>
    /// <param name="isProjDb">프로젝트에 등록된 외부 DB 로 붙는다</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<ProjMngResult> DbContAsync(
        string procName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        string procType = "srch",
        bool isServerFix = false,
        bool isProjDb = false,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidName(procName, "sp_"))
        {
            // 오타가 그대로 DB 로 나가는 것을 막는다. 옛 Blazor 때부터 있던 방어다.
            logger.LogWarning("프로시저 이름 규칙 위반: {ProcName}", procName);
            return ProjMngResult.Empty;
        }

        return await PostAsync(
            isServerFix ? ProjSysUrl : ProjUrl,
            new ProjMngRequest
            {
                ProcName = procName,
                ProcType = procType,
                IsProjDb = isProjDb,
                MainParam = ToParam(parameters),
            },
            cancellationToken);
    }

    /// <summary>
    /// 변경된 행을 다건 저장한다.
    ///
    /// 어느 행이 변경되었는지는 <see cref="ProjMngTable.ChangedRows"/> 가 정한다 —
    /// DataTable 의 <c>RowState</c> 를 보므로 표시를 손으로 붙이고 뗄 일이 없다.
    /// (Vue 는 <c>quri_ischange</c> 를 직접 붙였고, 그 표시를 지우는 것을 잊으면
    /// 다음 저장 때 같은 행이 또 나갔다.)
    /// </summary>
    /// <returns>
    /// 보낼 행이 없으면 <c>ProcCode = -77</c> 인 결과. 예외가 아니다 —
    /// 사용자가 아무것도 안 고치고 저장을 누른 것은 오류가 아니라 안내 대상이다.
    /// </returns>
    public async Task<ProjMngResult> DbSaveAsync(
        string procName,
        IReadOnlyList<Dictionary<string, object?>> changedRows,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidName(procName, "sp_"))
        {
            logger.LogWarning("프로시저 이름 규칙 위반: {ProcName}", procName);
            return ProjMngResult.Empty;
        }

        if (changedRows.Count == 0)
        {
            return new ProjMngResult { ProcCode = -77, Cols = [], Rows = [] };
        }

        return await PostAsync(
            ProjUrl,
            new ProjMngRequest
            {
                ProcName = procName,
                ProcType = "save",
                MainParam = ToParam(parameters),
                MultyData = [.. changedRows],
            },
            cancellationToken);
    }

    /// <summary>단건 삭제. 그리드는 이미 화면에서 그 행을 지운 상태로 부른다.</summary>
    public Task<ProjMngResult> DbDeleteAsync(
        string procName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
        => DbContAsync(procName, parameters, "delete", cancellationToken: cancellationToken);

    /// <summary>
    /// **보낸 SQL 을 그대로** 대상 DB 에서 실행한다.
    ///
    /// [등록된 액션과 다르다]
    ///
    /// <see cref="JsContAsync"/> 는 <c>projmng.devsqlresp</c> 에 <b>미리
    /// 등록된</b> 질의를 이름으로 부른다. 여기는 문장을 직접 보낸다.
    ///
    /// 한동안 「DB 쿼리 테스터」가 <c>dbtester</c> 라는 액션을 부르고 있었는데
    /// 그런 이름이 등록돼 있지 않아 늘 「정의되지 않았습니다」로 끝났다.
    /// 옛 화면이 쓰던 길이 이쪽이다.
    ///
    /// <b>DML 이 그대로 나간다.</b> 화면이 그 사실을 사용자에게 밝혀야 한다.
    /// </summary>
    /// <param name="dbNick">대상 DB 의 별칭 (<c>projmng.devdbinfo.db_nick</c>)</param>
    /// <param name="query">실행할 문장</param>
    /// <param name="breakOnCount">건수가 많으면 중간에 끊을지</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public Task<ProjMngResult> RawSqlAsync(
        string dbNick,
        string query,
        bool breakOnCount = true,
        CancellationToken cancellationToken = default)
        => PostAsync(
            DevSqlUrl,
            new Dictionary<string, string>
            {
                ["db_nick"] = dbNick,
                ["query"] = query,
                ["isBreakCnt"] = breakOnCount ? "true" : string.Empty,
            },
            cancellationToken);

    /// <summary>
    /// 개발 도구 조회. 프로시저 이름 규칙을 따르지 않는 액션 이름을 쓴다
    /// (<c>tablelist</c> · <c>proclist</c> · <c>columnsOftable</c> …).
    ///
    /// <b>이름이 <c>projmng.devsqlresp</c> 에 등록돼 있어야 한다.</b> 없는 이름을
    /// 보내면 「dbtype … 가 정의 되지 않았습니다」로 끝난다 — 화면이 지어낸
    /// 이름을 쓰다가 실제로 그랬다.
    /// </summary>
    /// <param name="actionName">액션 이름</param>
    /// <param name="parameters">파라미터</param>
    /// <param name="isProjDb">
    /// 서버가 이 플래그로 쿼리를 어디서 찾을지 가른다.
    /// 꺼짐(기본)은 DB 종류별 시스템 쿼리(<c>projmng.devsqlresp</c>),
    /// 켜짐은 그 DB 한 개에만 등록된 쿼리(<c>projmng.dev_db_prop</c>).
    /// 켜야 할 곳에서 끄면 쿼리를 못 찾아 실패한다.
    /// </param>
    /// <param name="cancellationToken">취소 토큰</param>
    public Task<ProjMngResult> JsContAsync(
        string actionName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        bool isProjDb = false,
        CancellationToken cancellationToken = default)
        => PostAsync(
            DevUrl,
            new ProjMngRequest
            {
                ProcName = actionName,
                IsProjDb = isProjDb,
                MainParam = ToParam(parameters),
            },
            cancellationToken);

    /// <summary>서버 파일시스템을 훑어 소스·서비스 정의를 읽어 온다 (<c>md_*</c>).</summary>
    public async Task<ProjMngResult> MdContAsync(
        string mdName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidName(mdName, "md_"))
        {
            logger.LogWarning("스캔 이름 규칙 위반: {Name}", mdName);
            return ProjMngResult.Empty;
        }

        return await PostAsync(
            MediaUrl,
            new ProjMngRequest { ProcName = mdName, MainParam = ToParam(parameters) },
            cancellationToken);
    }

    /// <summary>
    /// 실패해도 예외를 위로 던지지 않는다.
    ///
    /// 화면은 빈 결과를 받아 계속 그린다 — 옛 Blazor 와 Vue 모두 그렇게 동작했고,
    /// 프로시저 하나가 실패했다고 화면 전체가 사라지면 사용자가 무엇을 했는지
    /// 알 수 없게 된다. 오류 안내는 화면이 <see cref="ProjMngResult.ProcCode"/> 와
    /// 로그로 판단한다.
    /// </summary>
    /// <summary>
    /// 생 SQL 통로용 보내기. 본문이 <see cref="ProjMngRequest"/> 가 아니라
    /// 평평한 사전이라 따로 둔다.
    /// </summary>
    private async Task<ProjMngResult> PostAsync(
        string url,
        Dictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await gateway.PostObjectAsync<ProjMngResult>(url, payload, cancellationToken)
                   ?? ProjMngResult.Empty;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "생 SQL 실행 실패: {Url}", url);
            return ProjMngResult.Empty;
        }
    }

    private async Task<ProjMngResult> PostAsync(
        string url,
        ProjMngRequest payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await gateway.PostObjectAsync<ProjMngResult>(url, payload, cancellationToken)
                   ?? ProjMngResult.Empty;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "프로시저 호출 실패: {ProcName}", payload.ProcName);
            return new ProjMngResult
            {
                ProcCode = -1,
                Cols = [],
                Rows = [],
            };
        }
    }

    private static bool IsValidName(string name, string prefix) =>
        !string.IsNullOrEmpty(name) && name.StartsWith(prefix, StringComparison.Ordinal) && name.Length >= 6;

    /// <summary>
    /// 프로시저 파라미터는 <b>전부 문자열로</b> 넘긴다.
    ///
    /// 서버의 <c>MainParam</c> 이 <c>Dictionary&lt;string, string&gt;</c> 라
    /// 숫자·불리언을 그대로 보내면 역직렬화에서 400 이 난다. 이건 요청 규약이므로
    /// 호출부가 아니라 여기서 지킨다.
    /// </summary>
    private static Dictionary<string, string> ToParam(
        IReadOnlyDictionary<string, object?>? source)
    {
        var result = new Dictionary<string, string>();
        if (source is null)
        {
            return result;
        }

        foreach (var (key, value) in source)
        {
            result[key] = value switch
            {
                null => string.Empty,
                DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss"),
                DateOnly date => date.ToString("yyyy-MM-dd"),
                bool flag => flag ? "true" : "false",

                // 숫자·문자열은 그대로 문자열로. 로캘에 따라 소수점이 달라지지
                // 않게 불변 문화권으로 찍는다 — 프로시저는 "1.5" 를 기대하지
                // "1,5" 를 기대하지 않는다.
                IFormattable formattable =>
                    formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        }

        return result;
    }
}
