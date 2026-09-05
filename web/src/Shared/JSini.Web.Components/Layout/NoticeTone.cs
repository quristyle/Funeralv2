namespace JSini.Web.Components.Layout;

/// <summary>
/// 안내 줄의 성격.
///
/// 색을 셋으로만 나눈다. 더 나누면 화면마다 다른 색을 고르게 되고,
/// 그러면 색이 뜻을 잃는다.
/// </summary>
public enum NoticeTone
{
    /// <summary>알림 — 조회 결과 없음, 화면 사용법.</summary>
    Info,

    /// <summary>주의 — 권한이 없어 일부만 보인다, 자료가 오래됐다.</summary>
    Warning,

    /// <summary>실패 — 조회·저장이 안 됐다.</summary>
    Error,
}
