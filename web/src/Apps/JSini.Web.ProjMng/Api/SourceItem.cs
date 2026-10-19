namespace JSini.Web.ProjMng.Api;

/// <summary>프로젝트에 등록된 소스 묶음 하나(sp_dev_srcinfo_exec 의 한 행). 소스 목록의 정본은 「소스 정보」 화면이 읽는 표다 — 공통코드 srclist 는 sp_projCommon 에 없어 드롭다운이 늘 비어 있었다.</summary>
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
