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
/// <c>sp_projCommon</c> 에 그 코드가 <b>없었다</b> — 어떤 프로젝트로 물어도
/// 0 건이고, 아무 이름이나 넣었을 때와 결과가 같았다. 즉 그 드롭다운은 늘
/// 비어 있었고, 소스를 못 고르니 소스 추적·Glue 추적 화면이 실제로는
/// 동작하지 않았다.
/// </para>
/// <para>
/// 소스 목록의 정본은 「소스 정보」 화면이 등록하고 읽는 그 표다
/// (<c>projmng/source-infos</c>). 코드 한 겹을 거치지 않고 그것을 직접 읽는다.
/// </para>
/// </remarks>
/// <param name="Rid">소스 키 (<c>src_rid</c>). 화면이 조회에 실어 보내는 값이다.</param>
/// <param name="Nick">사람이 읽는 이름 (<c>src_nick</c>). 비어 있으면 키로 지어 준다.</param>
/// <param name="Source">그 소스의 등록 정보 전체.</param>
public sealed record SourceItem(
    string Rid,
    string Nick,
    SourceInfoDto Source)
{
    /// <summary>소스의 주 언어 (<c>blazor</c> · <c>jsp</c> · <c>java</c> …).</summary>
    public string Lang => Source.SrcLang ?? string.Empty;

    /// <summary>등록된 소스 경로. 서버 장비 기준이다.</summary>
    public string Path => Source.SrcPath ?? string.Empty;
}
