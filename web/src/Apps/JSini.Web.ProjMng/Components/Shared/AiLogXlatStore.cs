namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>로그 번역을 화면 밖에 들고 있는 곳. 번역 판은 탭 안에 있어 탭을 건너가면 판이 새로 태어나 번역이 사라지므로 여기 둔다.
/// 브라우저에는 적지 않는다 — 다시 만들 수 있는 값이고, scoped 라 회로 하나가 창 하나다.</summary>
public sealed class AiLogXlatStore
{
    /// <summary>들고 있을 건수. 넘으면 가장 오래 안 본 것부터 버린다. 건 하나가 로그 하나 + 차수마다 결과 하나를 차지하므로 넉넉히 둔다.</summary>
    private const int MaxKeys = 12;

    private readonly Dictionary<string, AiLogXlatState> _byKey = [];

    /// <summary>최근에 쓴 차례. 앞이 오래된 것이다.</summary>
    private readonly List<string> _order = [];

    /// <summary>그 건의 번역 상태. 없으면 만들어 준다.</summary>
    public AiLogXlatState Use(string key)
    {
        if (_byKey.TryGetValue(key, out var found))
        {
            Touch(key);
            return found;
        }

        var fresh = new AiLogXlatState();

        _byKey[key] = fresh;
        Touch(key);

        while (_order.Count > MaxKeys)
        {
            _byKey.Remove(_order[0]);
            _order.RemoveAt(0);
        }

        return fresh;
    }

    private void Touch(string key)
    {
        _order.Remove(key);
        _order.Add(key);
    }
}

/// <summary>한 건의 번역 상태. 판이 이것을 그린다.</summary>
public sealed class AiLogXlatState
{
    /// <summary>옮기는 줄들. 목록이어야 한다 — 돌고 있는 건의 로그는 자라므로 배열이면 옮기던 덩이가 옛 배열에 쓰인다.</summary>
    public List<string> Source { get; set; } = [];

    /// <summary>칸에 적을 줄들. <b>아직 안 옮긴 자리에는 원문이 있다.</b></summary>
    public List<string> Out { get; set; } = [];

    /// <summary>옮긴 줄의 자리(0부터). 사람이 누른 줄만 옮기므로 띄엄띄엄 흩어진다. 로그가 자라도 자리는 밀리지 않는다.</summary>
    public HashSet<int> DoneLines { get; } = [];

    /// <summary>줄이 통째로 갈린 횟수. 옮기던 덩이가 돌아왔을 때 달라져 있으면 그 답은 없는 줄의 것이라 버린다.</summary>
    public int Generation { get; set; }

    /// <summary>실제로 답한 AI. 로컬이 꺼져 있으면 다른 이름이 뜬다.</summary>
    public string? Who { get; set; }

    /// <summary>마지막으로 걸린 사정. 머리띠에 한 줄로 적는다.</summary>
    public string? Error { get; set; }

    /// <summary>옮겨 둔 것을 버린다. 줄 자체는 그대로다.</summary>
    public void Rewind()
    {
        Out = [.. Source];
        DoneLines.Clear();
        Who = null;
        Error = null;
    }

    /// <summary>줄을 통째로 갈아 끼운다. 세대가 올라가 지금 옮기던 줄의 답은 버려진다.</summary>
    public void Reset(IEnumerable<string> lines)
    {
        Source = [.. lines];
        Out = [.. Source];
        DoneLines.Clear();
        Generation++;
    }
}
