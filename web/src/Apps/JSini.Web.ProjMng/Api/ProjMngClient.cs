using JSini.Web.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.ProjMng.Api;

/// <summary>ProjMngServer 의 DB 밖 통로 셋 — 등록된 질의 실행(JsCont) · 보낸 문장 그대로 실행(RawSql) · 서버 디스크 스캔(MdCont).
/// 업무 자료는 자기 이름의 REST 통로로 간다(프로시저는 백엔드로 옮겼다).</summary>
public sealed class ProjMngClient(GatewayClient gateway, ILogger<ProjMngClient> logger)
{
    /// <summary>게이트웨이가 이 서비스로 라우팅하는 접두사.</summary>
    private const string Prefix = "projmng";

    /// <summary>개발 도구 — 프로젝트 DB 메타 조회.</summary>
    private const string DevUrl = $"{Prefix}/Dev";

    /// <summary>생 SQL 통로 주소. 액션 이름이 아니라 <c>{ db_nick, query }</c> 를 받는다.</summary>
    private const string DevSqlUrl = $"{Prefix}/Dev/sql";

    /// <summary>서버측 파일 스캔 (<c>md_*</c>).</summary>
    private const string MediaUrl = $"{Prefix}/Media";

    /// <summary>보낸 SQL 을 그대로 대상 DB 에서 실행한다. 등록된 질의(<see cref="JsContAsync"/>)와 달리 문장을 직접 보낸다 — DML 이 그대로 나가므로 화면이 그 사실을 밝혀야 한다.</summary>
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

    /// <summary>개발 도구 조회. 액션 이름은 <c>projmng.devsqlresp</c> 에 등록된 이름이어야 한다 — 없으면 「정의되지 않았습니다」로 끝난다.</summary>
    /// <param name="actionName">액션 이름</param>
    /// <param name="parameters">파라미터</param>
    /// <param name="isProjDb">꺼짐은 DB 종류별 시스템 쿼리(devsqlresp), 켜짐은 그 DB 에만 등록된 쿼리(dev_db_prop). 켜야 할 곳에서 끄면 못 찾는다.</param>
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

    /// <summary>생 SQL 통로용 보내기. 본문이 평평한 사전이라 따로 둔다. 실패해도 예외를 던지지 않고 빈 결과를 돌려준다 — 프로시저 하나가 실패했다고 화면 전체가 사라지면 안 된다.</summary>
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

            return new ProjMngResult
            {
                ProcCode = -1,
                Cols = [],
                Rows = [],
                Message = ex.Message,
            };
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

            // 문구를 버리지 않는다 — 서버의 업무 실패 문구가 화면에 없으면 무엇을 고칠지 알 수 없다.
            return new ProjMngResult
            {
                ProcCode = -1,
                Cols = [],
                Rows = [],
                Message = ex.Message,
            };
        }
    }

    private static bool IsValidName(string name, string prefix) =>
        !string.IsNullOrEmpty(name) && name.StartsWith(prefix, StringComparison.Ordinal) && name.Length >= 6;

    /// <summary>프로시저 파라미터는 전부 문자열로 넘긴다 — 서버 MainParam 이 문자열 사전이라 숫자·불리언을 보내면 400 이다.</summary>
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

                // 불변 문화권으로 찍는다 — 로캘에 따라 소수점이 바뀌면 프로시저가 틀린 값을 받는다.
                IFormattable formattable =>
                    formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        }

        return result;
    }
}
