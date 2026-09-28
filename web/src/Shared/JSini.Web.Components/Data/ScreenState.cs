namespace JSini.Web.Components.Data;

/// <summary>
/// 화면이 떠날 때 짐을 맡기고 돌아오면 되찾는 자리 — vben 의 <c>keep-alive</c> 를
/// 대신한다.
/// </summary>
/// <remarks>
/// <para>
/// [부품을 살려 두지 않고 값만 맡아 둔다]
/// </para>
///
/// <para>
/// vben 은 떠난 화면의 <b>컴포넌트를 통째로 살려</b> 두었다. Blazor 에서 같은
/// 일을 하려면 라우터가 화면을 감춰 둬야 하는데, 그러면 화면 서른 개가 회로
/// 하나에 살아 있게 되고 그중 스스로 갱신하는 화면(빈소현황 · SM 모니터링)이
/// <b>보이지 않는 채로 계속 서버를 부른다.</b> 그래서 탭은 주소 목록으로만
/// 두었다(<see cref="Layout.PortalTabs"/> 머리말).
/// </para>
///
/// <para>
/// 여기는 그 결정을 뒤집지 않는다. 화면은 여전히 떠날 때 죽고 돌아올 때 새로
/// 그려지지만, <b>죽기 직전에 남긴 값</b>이 여기 남아 있어서 새로 그려진
/// 화면이 그것으로 제 모습을 되살린다. 살아 있는 것은 <b>값뿐</b>이라
/// 타이머도, 조회도, 회로도 딸려 오지 않는다.
/// </para>
///
/// <para>
/// [scoped 다 — 회로 하나가 곧 창 하나다]
/// </para>
///
/// <para>
/// 싱글턴으로 두면 남이 걸어 둔 조건이 내 화면에 뜬다. 브라우저에 적어 두지도
/// 않는다 — 새로고침(F5)은 「처음부터」라는 뜻이고, 그때까지 되살리면 조건을
/// 지우려고 F5 를 누른 사람이 빠져나갈 길이 없어진다.
/// </para>
///
/// <para>
/// [열쇠는 화면이 정한다]
/// </para>
///
/// <para>
/// <c>DataPage</c> 가 대신 정해 주지 않는다. 한 화면이 여러 덩이를 맡길 수
/// 있고(조건 · 표의 모습 · 구른 자리), 그 이름을 화면이 직접 적어 두는 편이
/// 나중에 무엇이 남는지 읽기 쉽다. 관례는 <c>화면경로:이름</c> 이다.
/// </para>
/// </remarks>
public sealed class ScreenState
{
    /// <summary>
    /// 맡아 둘 수 있는 짐의 수.
    ///
    /// <para>
    /// 넘으면 <b>가장 오래 안 찾아간 것</b>부터 버린다. 상한이 없으면 회로
    /// 하나가 화면을 돌아다닌 만큼 목록을 들고 있게 되는데, 여기 담기는 것에는
    /// <b>조회 결과 수백 줄</b>이 섞여 있어서 그대로 두면 회로 하나의 메모리가
    /// 계속 자란다. 짐을 잃어도 화면은 처음 모습으로 그려질 뿐이다.
    /// </para>
    /// </summary>
    private const int MaxSlots = 12;

    private sealed class Slot
    {
        public required object Value { get; set; }
        public DateTime LastSeen { get; set; }
    }

    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);

    /// <summary>맡겨 둔 짐을 찾는다. 없거나 형이 다르면 <c>null</c> 이다.</summary>
    /// <remarks>
    /// 찾아가도 <b>지우지 않는다.</b> 화면은 만들어질 때 한 번 찾고 떠날 때 다시
    /// 맡기는데, 그 사이에 회로가 끊기면(새로고침·연결 끊김) 맡긴 적이 없는
    /// 것이 되어 버린다.
    /// </remarks>
    public T? Get<T>(string key) where T : class
    {
        if (!_slots.TryGetValue(key, out var slot))
        {
            return null;
        }

        slot.LastSeen = DateTime.UtcNow;
        return slot.Value as T;
    }

    /// <summary>짐을 맡긴다. 같은 열쇠가 있으면 덮어쓴다.</summary>
    public void Set<T>(string key, T value) where T : class
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (_slots.TryGetValue(key, out var slot))
        {
            slot.Value = value;
            slot.LastSeen = DateTime.UtcNow;
            return;
        }

        _slots[key] = new Slot { Value = value, LastSeen = DateTime.UtcNow };
        Trim();
    }

    /// <summary>맡긴 것을 버린다. 「처음부터 다시」가 필요한 자리에서 부른다.</summary>
    public void Clear(string key) => _slots.Remove(key);

    /// <summary>상한을 넘으면 가장 오래 안 찾아간 것부터 버린다.</summary>
    private void Trim()
    {
        while (_slots.Count > MaxSlots)
        {
            var oldest = _slots.OrderBy(pair => pair.Value.LastSeen).First().Key;
            _slots.Remove(oldest);
        }
    }
}
