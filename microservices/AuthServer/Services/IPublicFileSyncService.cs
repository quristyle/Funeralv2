namespace AuthServer.Services;

/// <summary>
/// 공지의 공개 여부에 맞춰 첨부파일의 익명 열람 허용(<c>scom.filemetadatas.ispublic</c>)을 맞춘다.
/// </summary>
/// <remarks>
/// <b>왜 필요한가.</b> 로그인 전 화면에도 뜨는 공개 공지(<c>GET /notices/popup/public</c>)에는
/// 첨부 링크가 함께 나간다. 그런데 파일 읽기는 FileServer 가 <c>ispublic</c> 으로 판정하므로
/// (docs/analysis/27-jsini-site-brand.md 5절), 공지만 공개로 두면 그 첨부는 404 가 된다.
/// **공지를 공개로 두면 첨부도 공개로 본다** 는 결정(D-S10)을 코드로 옮긴 것이다.
///
/// <b>왜 AuthServer 가 FileServer 의 표를 건드리나.</b> 서비스 간 인증이 아직 없다.
/// FileServer 의 <c>PUT /api/file/public/{id}</c> 는 게이트웨이가 인증을 요구하고,
/// 서비스가 직접 부르면 <c>X-User-Id</c> 가 없어 401 이 된다. 그 통로를 새로 여는 것은
/// 인증 설계를 건드리는 일이라(각 서비스 appsettings 의 <c>//kestrel</c> 주석 참고) 여기서는
/// **같은 DB · 같은 <c>scom</c> 스키마** 라는 사실을 쓴다. 한 문장짜리 UPDATE 다.
///
/// 서비스를 다른 DB 로 갈라야 할 때가 오면 이 클래스 하나만 HTTP 호출로 바꾸면 된다.
/// 그때 필요한 것은 서비스 간 인증이다.
///
/// <b>남이 켜 둔 것은 끄지 않는다.</b> 끄는 것은 <c>updatedby = 'NoticeSync'</c> 인 행뿐이다.
/// 소개 사이트 자료실처럼 다른 이유로 공개된 파일을, 공지에서 뗐다는 이유로 닫아 버리면 안 된다.
/// </remarks>
public interface IPublicFileSyncService
{
    /// <summary>
    /// 주어진 파일들의 <c>ispublic</c> 을 다시 계산해 맞춘다.
    /// '공개 · 활성 · 삭제 안 된 공지' 에 붙어 있으면 켜고, 아니면 끈다.
    /// </summary>
    /// <returns>값이 바뀐 행 수</returns>
    Task<int> SyncAsync(IEnumerable<Guid> fileIds);
}
