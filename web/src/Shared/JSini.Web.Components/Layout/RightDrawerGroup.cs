namespace JSini.Web.Components.Layout;

/// <summary>오른쪽에서 밀려 들어오는 판들을 한 번에 하나만 펴 두는 자리. 알림함 · 화면 설정 · 빠른 지시 · AI 대화가 똑같이 right: 0 에 서서 펴 둔 판이 있는데 앞에 적힌 판을 열면 뒤에 가려 단추는 눌렸는데 화면이 그대로인 것으로 읽힌다.
/// 여는 자리가 여섯이라 자리마다 「다른 판을 먼저 접어라」를 적으면 빠뜨리므로, 손잡이 넷이 펴질 때 스스로 CloseOthers 를 부른다 — 여는 길이 몇이든 여기를 지난다. 못 박아 둔 판도 접는다 — 고정핀의 뜻은 화면을 옮겨도 펴 둔 채로 두라이지 다른 판을 가려도 좋다가 아니다. scoped — 한 사람이 열었다고 모두의 판이 접히면 안 된다.</summary>
public sealed class RightDrawerGroup
{
    private readonly Dictionary<object, Action> _closers = new(ReferenceEqualityComparer.Instance);

    /// <summary>접는 중인가. 되돌아 들어오는 것을 막는다 — 접히는 판이 제 손잡이를 다시 울려 여기로 돌아오면 넷이 서로를 접으며 돈다.</summary>
    private bool _closing;

    /// <summary>판 하나를 이 무리에 건다. 같은 주인을 두 번 걸면 뒤의 것으로 갈린다.</summary>
    /// <param name="owner">판의 손잡이. CloseOthers 가 이것으로 자기를 가린다.</param>
    /// <param name="close">그 판을 접는 길. 이미 접혀 있으면 아무 일도 하지 않아야 한다.</param>
    public void Register(object owner, Action close)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(close);

        _closers[owner] = close;
    }

    /// <summary>owner 말고 펴져 있는 판을 모두 접는다. 펴는 쪽이 펴기 직전에 부른다.</summary>
    public void CloseOthers(object owner)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        try
        {
            // 접는 사이에 누가 더 걸릴 수 있으므로 떠낸 것을 돈다.
            foreach (var (member, close) in _closers.ToArray())
            {
                if (!ReferenceEquals(member, owner))
                {
                    close();
                }
            }
        }
        finally
        {
            _closing = false;
        }
    }
}
