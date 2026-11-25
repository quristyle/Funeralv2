namespace JSini.Web.HelpDesk.Api;

/// <summary>요청을 바꾼 화면이 회로 안에 남기는 표시. 「요청 처리」는 맡겨 둔 목록을 다시 묻지 않고 되살리므로,
/// 상세에서 접수·완료·삭제하고 돌아오면 바뀌기 전 줄이 그대로 선다 — 맡길 때의 <see cref="Version"/> 과 견줘 다르면 다시 읽는다.</summary>
public sealed class RequestChanges
{
    private long _version;

    /// <summary>회로가 열린 뒤 요청이 바뀐 횟수.</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>바뀐 요청 번호. 떠 있는 목록이 듣는다.</summary>
    public event Action<int>? Changed;

    public void Notify(int requestId)
    {
        Interlocked.Increment(ref _version);
        Changed?.Invoke(requestId);
    }
}
