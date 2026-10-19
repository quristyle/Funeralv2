using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Components.Layout;

/// <summary>「빠른 지시」 서랍을 바깥에서 여닫는 손잡이. 단추는 HeaderTools 에, 판은 QuickAskDrawer 에 있어 형제도 부모 자식도 아니라 사이에 통을 둔다. 여닫힘을 여기가 드는 까닭은 단추가 토글이라서다 — 판이 상태를 들면 단추는 열린 줄 몰라 열기밖에 못 해 한 번 연 서랍을 같은 단추로 닫을 수 없다. scoped — 한 사람이 열었다고 모두의 서랍이 열리면 안 된다.</summary>
public sealed class QuickAskReveal
{
    /// <summary>알맹이가 등록됐는지 보려고 들고 있다. QuickAskContent 는 없을 수 있어 생성자로 못 받는다.</summary>
    private readonly IServiceProvider _services;

    /// <summary>같은 자리에 겹쳐 서는 오른쪽 판 무리. 펴기 전에 다른 판을 접는다(RightDrawerGroup).</summary>
    private readonly RightDrawerGroup _group;

    public QuickAskReveal(IServiceProvider services, RightDrawerGroup group)
    {
        _services = services;
        _group = group;
        _group.Register(this, Close);
    }

    /// <summary>서랍이 지금 펴져 있는지를 알맹이에게 흘려 주는 이름(CascadingValue). 값은 bool? 다 — 서랍 안이 아니면 아예 안 내려간다: 같은 알맹이가 화면으로도 뜨는데 거기서 false 를 받으면 안 보이는 중으로 오해해 목록 따라가기를 멈춘다. 알맹이는 값이 거짓에서 참으로 갈리는 순간 목록을 다시 읽는다 — 서랍은 닫아도 알맹이를 안 지우므로 안 그러면 처음 열었을 때의 카드가 그대로 앉아 있다. 파라미터로 못 넘기는 까닭은 QuickAskContent 와 같다.</summary>
    public const string OpenCascade = "QuickAskOpen";

    /// <summary>지금 펴져 있나.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>여닫힘이 갈렸다. 판과 단추가 함께 듣는다.</summary>
    public event Action? Changed;

    /// <summary>
    /// 서랍에 그릴 부품. 업무 모듈이 등록하지 않았으면 <c>null</c> 이다.
    /// </summary>
    public Type? PanelType => _services.GetService<QuickAskContent>()?.PanelType;

    /// <summary>
    /// 서랍으로 열 수 있나. <b>거짓이면 단추는 화면으로 옮겨 간다.</b>
    /// </summary>
    public bool CanDock => PanelType is not null;

    /// <summary>여닫는다. <b>펴지는 쪽일 때만</b> 다른 오른쪽 판을 접는다.</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        Open();
    }

    /// <summary>편다. <b>다른 오른쪽 판을 먼저 접는다.</b></summary>
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        // 먼저 접고 편다. 뒤에 접으면 방금 편 것까지 함께 접힌다.
        _group.CloseOthers(this);

        IsOpen = true;
        Changed?.Invoke();
    }

    /// <summary>닫는다. 이미 닫혀 있으면 아무 일도 하지 않는다.</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        Changed?.Invoke();
    }
}
