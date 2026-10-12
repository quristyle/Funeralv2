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
/// 그 값이 비싸다. 번역은 로컬 LLM 왕복이 수십 번이라 삼백 줄이면 몇 분이
/// 걸린다. 결과를 보러 건너갔다 오는 것은 <b>이 화면에서 가장 흔한
/// 동작</b>인데, 그때마다 처음부터 다시 돌면 아무도 끝까지 못 본다.
/// </para>
/// <para>
/// 그래서 옮긴 것을 화면이 아니라 여기 둔다. 돌아오면 판은 <b>멈춘 자리에서
/// 이어 간다</b>. 「빠른 지시」의 임시본·고른 값을 화면 밖에 두는 것과 같은
/// 까닭이다(<see cref="AiTaskDraftStore"/> · <see cref="AiAskPrefs"/>).
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
    private const int MaxKeys = 4;

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

    /// <summary>앞에서부터 몇 줄까지 손댔나. 그 뒤가 남은 몫이다.</summary>
    public int Done { get; set; }

    /// <summary>
    /// 들고 있는 줄이 <b>통째로 갈린 횟수.</b> 옮기던 덩이가 돌아왔을 때
    /// 이것이 달라져 있으면 그 답은 <b>이제 없는 줄의 것</b>이라 버린다.
    /// </summary>
    public int Generation { get; set; }

    /// <summary>옮겨 갈 말(<c>ko</c> · <c>en</c>).</summary>
    public string Lang { get; set; } = "ko";

    /// <summary>실제로 답한 AI. 로컬이 꺼져 있으면 다른 이름이 뜬다.</summary>
    public string? Who { get; set; }

    /// <summary>마지막으로 걸린 사정. 머리띠에 한 줄로 적는다.</summary>
    public string? Error { get; set; }

    /// <summary>아직 안 옮긴 줄 수.</summary>
    public int Remaining => Math.Max(0, Source.Count - Done);

    /// <summary>옮겨 둔 것을 버리고 처음부터 센다. 줄 자체는 그대로다.</summary>
    public void Rewind()
    {
        Out = [.. Source];
        Done = 0;
        Who = null;
        Error = null;
    }

    /// <summary>
    /// 줄을 통째로 갈아 끼운다. <b>세대가 하나 올라간다</b> — 지금 옮기고
    /// 있던 덩이의 답은 버려진다.
    /// </summary>
    public void Reset(IEnumerable<string> lines)
    {
        Source = [.. lines];
        Out = [.. Source];
        Done = 0;
        Generation++;
    }
}
