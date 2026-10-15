namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 로그 번역을 <b>화면 밖에</b> 들고 있는 곳.
/// </summary>
/// <remarks>
/// <para>
/// [무엇을 고친 것인가]
/// </para>
/// <para>
/// 비교 판(<see cref="LogTranslateView"/>)은 로그 탭 <b>안</b>에 있고, 그
/// 탭은 <c>DxTabs</c> 가 쥐고 있다. 「지시와 결과」로 한 번 건너가면 탭의
/// 알맹이가 통째로 버려지고 돌아올 때 <b>새로 태어난다</b> — 판이 들고 있던
/// 번역도 함께 없어진다.
/// </para>
/// <para>
/// 그 값이 비싸다. 줄 하나를 옮기는 데 로컬 LLM 왕복이 한 번이고, 사람은
/// 걸린 자리를 찾느라 열 줄 스무 줄을 짚는다. 결과를 보러 건너갔다 오는 것은
/// <b>이 화면에서 가장 흔한 동작</b>인데, 그때마다 짚은 줄이 통째로 원문으로
/// 돌아가 있으면 같은 줄을 또 눌러야 한다.
/// </para>
/// <para>
/// 그래서 옮긴 것을 화면이 아니라 여기 둔다. 돌아오면 판은 <b>짚어 둔 줄을
/// 그대로 들고</b> 선다. 「빠른 지시」의 임시본·고른 값을 화면 밖에 두는 것과
/// 같은 까닭이다(<see cref="AiTaskDraftStore"/> · <see cref="AiAskPrefs"/>).
/// </para>
/// <para>
/// <b>브라우저에 적지 않는다.</b> 임시본과 갈리는 자리다 — 저것은 사람이
/// 적은 글이라 창을 닫아도 살아야 하지만, 이것은 <b>다시 만들 수 있는
/// 것</b>이고 로그 자체가 회로 안에만 있다. scoped 로 두면 회로 하나가
/// 사용자 한 명의 창 하나다.
/// </para>
/// </remarks>
public sealed class AiLogXlatStore
{
    /// <summary>
    /// 들고 있을 건수. 넘으면 <b>가장 오래 안 본 것부터</b> 버린다 —
    /// 로그 삼백 줄 두 벌이 한 건이라 무한정 쌓이면 회로가 무거워진다.
    /// </summary>
    /// <remarks>
    /// <b>한 건이 자리 하나가 아니다</b>(2026-10-16). 결과문도 이 통을 쓰게
    /// 되면서(<see cref="LogTranslateView"/> 를 「지시와 결과」 탭이 함께
    /// 쓴다) 건 하나가 <c>로그 하나 + 차수마다 결과 하나</c>를 차지한다.
    /// 넷으로 두면 <b>3차까지 간 건 하나를 펴 두는 것만으로 꽉 차서</b>,
    /// 탭을 한 번 건너갔다 오면 옮겨 둔 줄이 사라진다.
    /// </remarks>
    private const int MaxKeys = 12;

    private readonly Dictionary<string, AiLogXlatState> _byKey = [];

    /// <summary>최근에 쓴 차례. 앞이 오래된 것이다.</summary>
    private readonly List<string> _order = [];

    /// <summary>
    /// 그 건의 번역 상태를 꺼낸다. <b>없으면 만들어 준다</b> — 부르는 쪽이
    /// 「처음인가」를 가릴 일이 없게 한다.
    /// </summary>
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

/// <summary>
/// 한 건의 번역 상태. <b>판이 이것을 그린다</b> — 판은 들고 있지 않는다.
/// </summary>
public sealed class AiLogXlatState
{
    /// <summary>
    /// 옮기는 줄들. <b>목록이다</b> — 돌고 있는 건의 로그는 바닥으로 자라는데,
    /// 배열이면 자랄 때마다 통째로 갈아 끼워야 해서 옮기던 덩이가 옛 배열에
    /// 쓰인다(다 옮겼는데 화면에는 원문이 남는다).
    /// </summary>
    public List<string> Source { get; set; } = [];

    /// <summary>오른쪽 칸에 적을 줄들. <b>아직 안 옮긴 자리에는 원문이 있다.</b></summary>
    public List<string> Out { get; set; } = [];

    /// <summary>
    /// 옮긴 줄의 자리(0부터). <b>앞에서부터 센 숫자가 아니라 자리의 모음이다</b> —
    /// 사람이 누른 줄만 옮기므로 옮긴 자리가 띄엄띄엄 흩어진다.
    /// </summary>
    /// <remarks>
    /// 로그가 자라도 <b>자리는 안 밀린다</b>(뒤에만 붙는다). 그래서 담아 둔
    /// 자리가 그대로 맞고, 앞이 잘려 나갔을 때만 <see cref="Reset"/> 이 비운다.
    /// </remarks>
    public HashSet<int> DoneLines { get; } = [];

    /// <summary>
    /// 들고 있는 줄이 <b>통째로 갈린 횟수.</b> 옮기던 덩이가 돌아왔을 때
    /// 이것이 달라져 있으면 그 답은 <b>이제 없는 줄의 것</b>이라 버린다.
    /// </summary>
    public int Generation { get; set; }

    /// <summary>실제로 답한 AI. 로컬이 꺼져 있으면 다른 이름이 뜬다.</summary>
    public string? Who { get; set; }

    /// <summary>마지막으로 걸린 사정. 머리띠에 한 줄로 적는다.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// 좌우 칸을 가르는 자리. <b>왼쪽 칸이 차지하는 몫</b>(0~1)이고 반반이
    /// 처음 값이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 사람이 가운데 가름쇠를 끌면 바뀐다. <b>여기 두는 까닭은 번역과 같다</b> —
    /// 로그 탭을 떠나면 편집기가 통째로 버려지고 돌아올 때 새로 서는데, 판이
    /// 들고 있으면 끌어 둔 자리가 그때마다 한가운데로 되돌아간다.
    /// </para>
    /// <para>
    /// <b><see cref="Rewind"/> 도 <see cref="Reset"/> 도 건드리지 않는다.</b>
    /// 저 둘이 버리는 것은 <b>옮긴 글</b>이고, 칸을 얼마나 벌려 놓고 보는지는
    /// 줄의 내용과 아무 상관이 없다.
    /// </para>
    /// </remarks>
    public double SplitRatio { get; set; } = 0.5;

    /// <summary>옮겨 둔 것을 버린다. 줄 자체는 그대로다.</summary>
    public void Rewind()
    {
        Out = [.. Source];
        DoneLines.Clear();
        Who = null;
        Error = null;
    }

    /// <summary>
    /// 줄을 통째로 갈아 끼운다. <b>세대가 하나 올라간다</b> — 지금 옮기고
    /// 있던 줄의 답은 버려진다.
    /// </summary>
    public void Reset(IEnumerable<string> lines)
    {
        Source = [.. lines];
        Out = [.. Source];
        DoneLines.Clear();
        Generation++;
    }
}
