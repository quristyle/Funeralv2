namespace JSini.Web.Components.Data;

/// <summary>화면이 떠날 때 짐을 맡기고 돌아오면 되찾는 자리(vben 의 keep-alive 를 대신한다). 떠난 화면의 부품을 살려 두면 화면 서른 개가 회로 하나에 살아 자동 갱신 화면이 보이지 않는 채 서버를 계속 부르므로, 살아 있는 것은 값뿐이고 타이머와 조회와 회로는 따라오지 않는다.
/// scoped — 싱글턴이면 남이 걸어 둔 조건이 내 화면에 뜬다. 브라우저에 적지 않는다 — 새로고침(F5)은 처음부터라는 뜻이고, 되살리면 조건을 지우려고 F5 를 누른 사람이 빠져나갈 길이 없다. 열쇠는 화면이 정한다 — 관례는 화면경로:이름이다.</summary>
public sealed class ScreenState
{
    /// <summary>맡아 둘 수 있는 짐의 수. 넘으면 가장 오래 안 찾아간 것부터 버린다 — 여기 담기는 것에는 조회 결과 수백 줄이 섞여 있어 상한이 없으면 회로 하나의 메모리가 계속 자란다. 짐을 잃으면 화면은 처음 모습으로 그려질 뿐이다.</summary>
    private const int MaxSlots = 12;

    private sealed class Slot
    {
        public required object Value { get; set; }
        public DateTime LastSeen { get; set; }
    }

    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);

    /// <summary>맡겨 둔 짐을 찾는다. 없거나 형이 다르면 null 이다. 찾아가도 지우지 않는다 — 화면은 만들어질 때 찾고 떠날 때 맡기는데, 그 사이에 회로가 끊기면 맡긴 적이 없는 것이 된다.</summary>
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
