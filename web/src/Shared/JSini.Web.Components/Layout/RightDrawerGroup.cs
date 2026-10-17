namespace JSini.Web.Components.Layout;

/// <summary>
/// 오른쪽에서 밀려 들어오는 판들을 <b>한 번에 하나만</b> 펴 두는 자리.
/// </summary>
/// <remarks>
/// <para>
/// [한 자리에 넷이 겹쳐 선다]
/// </para>
///
/// <para>
/// 알림함 · 화면 설정 · 빠른 지시 · AI 대화 — 넷 다 <c>RightSideDrawer</c> 로
/// 그려지고 <c>position: fixed; right: 0; z-index: 1040</c> 으로 <b>똑같은
/// 자리</b>에 선다. 쌓임 순서를 가르는 것은 <c>MainLayout</c> 에 적힌 차례뿐이라,
/// 펴 둔 판이 있는데 그보다 앞에 적힌 판을 열면 <b>뒤에 가려 한 뼘도 안 보인다</b> —
/// 단추는 눌렸고 판도 열렸는데 화면은 그대로여서 「단추가 고장 났다」로 읽힌다.
/// </para>
///
/// <para>
/// [여는 쪽이 아니라 손잡이가 막는다]
/// </para>
///
/// <para>
/// 여는 자리가 여섯이다(<c>HeaderTools</c> · <c>ThemeToggle</c> ·
/// <c>AiChatToggle</c> · <c>UserMenu</c> · <c>HeaderNavTools</c> ·
/// <c>MobileBottomNav</c>). 거기마다 「다른 판을 먼저 접어라」를 적으면
/// 반드시 몇 자리를 빠뜨리고, 빠뜨린 자리에서만 판이 겹친다 — 그래서
/// <b>손잡이 넷</b>(<see cref="NotificationDrawer"/> · <see cref="ThemeDrawer"/> ·
/// <see cref="QuickAskReveal"/> · <see cref="AiChatReveal"/>)이 제가 펴질 때
/// 스스로 <see cref="CloseOthers"/> 를 부른다. 여는 길이 몇이든 반드시 여기를
/// 지난다.
/// </para>
///
/// <para>
/// <b>못 박아 둔 판도 접는다.</b> 고정핀의 뜻은 「화면을 옮겨도 펴 둔 채로
/// 두라」지 「다른 판을 가려도 좋다」가 아니다 — 꽂아 둔 핀은 그대로 남아
/// 다음에 그 판을 열면 도로 박힌 채로 열린다.
/// </para>
///
/// <para>
/// 손잡이는 저마다 자기를 <see cref="Register"/> 로 걸어 둔다. 걸지 않은
/// 판은 접히지 않을 뿐 아무것도 깨뜨리지 않는다.
/// </para>
///
/// <para>scoped 다. 한 사람이 열었다고 모두의 판이 접히면 안 된다.</para>
/// </remarks>
public sealed class RightDrawerGroup
{
    private readonly Dictionary<object, Action> _closers = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// 접는 중인가. <b>되돌아 들어오는 것을 막는다</b> — 접히는 판이 제
    /// 손잡이를 다시 울려 여기로 돌아오면 넷이 서로를 접으며 돈다.
    /// </summary>
    private bool _closing;

    /// <summary>
    /// 판 하나를 이 무리에 건다. 같은 주인을 두 번 걸면 뒤의 것으로 갈린다.
    /// </summary>
    /// <param name="owner">판의 손잡이. <see cref="CloseOthers"/> 가 이것으로 자기를 가린다.</param>
    /// <param name="close">그 판을 접는 길. 이미 접혀 있으면 아무 일도 하지 않아야 한다.</param>
    public void Register(object owner, Action close)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(close);

        _closers[owner] = close;
    }

    /// <summary>
    /// <paramref name="owner"/> 말고 <b>펴져 있는 판을 모두 접는다.</b>
    /// 펴는 쪽이 펴기 직전에 부른다.
    /// </summary>
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
