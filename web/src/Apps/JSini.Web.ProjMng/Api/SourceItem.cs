namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 프로젝트에 등록된 소스 묶음 하나 (<c>sp_dev_srcinfo_exec</c> 의 한 행).
/// </summary>
/// <remarks>
/// <para>
/// [왜 공통코드가 아닌가]
/// </para>
/// <para>
/// 옛 Vue 는 소스 드롭다운을 공통코드 <c>srclist</c> 로 채웠다. 그런데
/// <c>sp_projCommon</c> 에 그 코드가 <b>없다</b> — 게이트웨이로 직접 불러 보면
/// 어떤 프로젝트로 물어도 0 건이고, 아무 이름이나 넣었을 때와 결과가 같다.
/// 즉 그 드롭다운은 늘 비어 있었고, 소스를 못 고르니 소스 추적·Glue 추적
/// 화면이 실제로는 동작하지 않았다.
/// </para>
/// <para>
/// 소스 목록의 정본은 <c>sp_dev_srcinfo_exec</c> 다 — 「소스 정보」 화면이
/// 등록하고 읽는 바로 그 표다. 코드 한 겹을 거치지 않고 그것을 직접 읽는다.
/// </para>
/// </remarks>
/// <param name="Rid">소스 키 (<c>src_rid</c>). 프로시저에 실어 보내는 값이다.</param>
/// <param name="Nick">사람이 읽는 이름 (<c>src_nick</c>).</param>
/// <param name="Others">함께 온 나머지 컬럼 (<c>src_lang</c> · <c>src_path</c> …).</param>
public sealed record SourceItem(
    string Rid,
    string Nick,
    IReadOnlyDictionary<string, string> Others)
{
    /// <summary>소스의 주 언어 (<c>blazor</c> · <c>jsp</c> · <c>java</c> …).</summary>
    public string Lang => Others.GetValueOrDefault("src_lang", string.Empty);

    /// <summary>등록된 소스 경로. 서버 장비 기준이다.</summary>
    public string Path => Others.GetValueOrDefault("src_path", string.Empty);
}
