namespace JSini.Web.Components.Layout;

/// <summary>
/// 화면을 옮기는 동안을 표시하는 상태 하나. <see cref="PageSpinner"/> 가 이것을 본다.
/// </summary>
/// <remarks>
/// <para>
/// 이력: Vue <c>packages/effects/layouts/src/basic/content/use-content-spinner.ts</c>.
/// 그쪽은 라우터 가드 둘(<c>beforeEach</c>·<c>afterEach</c>) 사이를 덮었다.
/// </para>
///
/// <para>
/// [Blazor 에서는 덮어야 할 구간이 다르다]
/// </para>
///
/// <para>
/// Vue 가 덮던 것은 <b>라우트 조각을 내려받는 동안</b>이었다. 화면마다 따로
/// 묶인 js 를 그때 받아 왔기 때문이다. Blazor Server 에는 그 구간이 없다 —
/// 부품은 이미 서버 메모리에 있고 라우팅은 즉시 끝난다. 그것만 덮으면
/// <b>아무도 못 보는 표시</b>가 된다.
/// </para>
///
/// <para>
/// 사람이 실제로 기다리는 것은 <b>새 화면이 게이트웨이에서 자료를 받는
/// 동안</b>이다(<c>DataPage.LoadAsync</c>). 그동안 화면은 이미 그려져 있고
/// 표만 비어 있어서, 조회 중인지 자료가 없는 것인지 구분이 안 갔다.
/// 그래서 여기서 덮는 것은 <b>주소가 바뀐 때부터 새 화면의 첫 조회가 끝날
/// 때까지</b>다.
/// </para>
///
/// <para>
/// [끝나는 때를 두 조건으로 본다]
/// </para>
///
/// <list type="number">
///   <item><b>첫 그림이 그려졌다</b>(<see cref="Rendered"/>) — 레이아웃이 알린다.</item>
///   <item><b>잡은 조회가 다 끝났다</b>(<see cref="Release"/>) — 화면이 알린다.</item>
/// </list>
///
/// <para>
/// 둘 다 필요하다. 그림만 보면 <b>조회를 시작하기도 전에</b> 걷힌다.
/// 조회만 보면 조회를 안 하는 화면(길잡이·안내·끼워 넣기)에서 <b>영영 안
/// 걷힌다</b>. 잡은 것이 없는 채로 첫 그림이 끝나면 그 자리에서 끝낸다.
/// </para>
///
/// <para>
/// [조회 단추는 이것을 켜지 않는다]
/// </para>
///
/// <para>
/// <see cref="Claim"/>·<see cref="Release"/> 는 <b>옮기는 중일 때만</b> 센다.
/// 같은 화면에서 「조회」를 누른 것까지 덮으면 조건을 고칠 때마다 화면이
/// 통째로 흐려진다 — 그 자리에는 표 자신의 조회 표시가 이미 있다.
/// </para>
/// </remarks>
public sealed class PageTransition
{
    private int _claims;
    private bool _rendered;

    /// <summary>지금 옮기는 중인가.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>임시 진단용. 확인 끝나면 지운다.</summary>
    public string Debug => $"busy={IsBusy} claims={_claims} rendered={_rendered}";

    /// <summary>켜지고 꺼질 때 알린다. 레이아웃이 다시 그린다.</summary>
    public event Action? Changed;

    /// <summary>주소가 바뀌었다. 레이아웃이 부른다.</summary>
    public void Begin()
    {
        _claims = 0;
        _rendered = false;

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// 이 화면이 조회를 시작한다. <b>첫 <c>await</c> 앞에서</b> 불러야 한다 —
    /// 뒤에서 부르면 그 사이에 첫 그림이 끝나 표시가 먼저 걷힌다.
    /// </summary>
    public void Claim()
    {
        if (IsBusy)
        {
            _claims++;
        }
    }

    /// <summary>조회가 끝났다(성공·실패 무관).</summary>
    public void Release()
    {
        if (!IsBusy || _claims == 0)
        {
            return;
        }

        _claims--;
        TryEnd();
    }

    /// <summary>옮긴 뒤 첫 그림이 끝났다. 레이아웃이 부른다.</summary>
    public void Rendered()
    {
        _rendered = true;
        TryEnd();
    }

    private void TryEnd()
    {
        if (!IsBusy || !_rendered || _claims > 0)
        {
            return;
        }

        IsBusy = false;
        Changed?.Invoke();
    }
}
