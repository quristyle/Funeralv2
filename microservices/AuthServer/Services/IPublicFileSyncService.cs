namespace AuthServer.Services;

/// <summary>공지의 공개 여부에 맞춰 첨부파일의 익명 열람 허용(ispublic)을 맞춘다.</summary>
/// 로그인 전 화면의 공개 공지 첨부가 404 가 되지 않게 하려는 것이다. 같은 DB·scom 스키마라 표를 직접 쓰며, 다른 DB 로 가르면 이 클래스만 HTTP 로 바꾼다. 남이 켜 둔 공개는 끄지 않는다.
public interface IPublicFileSyncService
{
    /// <summary>주어진 파일들의 ispublic 을 다시 계산해 맞춘다. 공개·활성·삭제 안 된 공지에 붙어 있으면 켜고 아니면 끈다.</summary>
    /// <returns>값이 바뀐 행 수</returns>
    Task<int> SyncAsync(IEnumerable<Guid> fileIds);
}
