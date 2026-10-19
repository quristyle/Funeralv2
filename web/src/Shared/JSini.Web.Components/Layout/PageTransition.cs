namespace JSini.Web.Components.Layout;

/// <summary>화면을 옮기는 동안을 표시하는 상태 하나. PageSpinner 가 이것을 본다. 이력은 Vue 의 use-content-spinner 로, 그쪽은 라우터 가드 둘 사이를 덮었다.
/// Blazor Server 에서는 덮을 구간이 다르다 — 라우팅은 즉시 끝나고 부품은 이미 서버에 있어, 사람이 실제로 기다리는 것은 새 화면이 게이트웨이에서 자료를 받는 동안(DataPage.LoadAsync)이다. 그래서 주소가 바뀐 때부터 새 화면의 첫 조회가 끝날 때까지 덮고, 끝나는 때는 첫 그림(Rendered)과 잡은 조회가 다 끝남(Release) 둘 다 필요하다 — 그림만 보면 조회 전에 걷히고, 조회만 보면 조회 안 하는 화면에서 영영 안 걷힌다.
/// Claim·Release 는 옮기는 중일 때만 센다 — 같은 화면의 조회 단추까지 덮으면 조건을 고칠 때마다 화면이 흐려진다.</summary>
public sealed class PageTransition
{
    private int _claims;
    private bool _rendered;

    /// <summary>지금 옮기는 중인가.</summary>
    public bool IsBusy { get; private set; }

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

    /// <summary>이 화면이 조회를 시작한다. 첫 await 앞에서 불러야 한다 — 뒤에서 부르면 그 사이에 첫 그림이 끝나 표시가 먼저 걷힌다.</summary>
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
