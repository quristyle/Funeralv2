using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 배포 실행 서비스 인터페이스
/// </summary>
public interface IReleaseService
{
    /// <summary>설정에 등록된 배포 대상 목록</summary>
    List<ReleaseTargetDto> GetTargets();

    /// <summary>
    /// 배포 스크립트 실행을 큐에 넣는다.
    /// </summary>
    /// <param name="key">배포 대상 식별자</param>
    /// <param name="userId">요청한 사용자 (로그에 남긴다)</param>
    ReleaseResultDto Trigger(string key, string? userId);
}
