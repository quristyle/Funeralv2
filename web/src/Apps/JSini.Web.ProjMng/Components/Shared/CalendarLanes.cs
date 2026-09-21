namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 월 달력에서 <b>한 주 안의 막대를 줄(lane)에 나눠 담는 일</b>.
///
/// <para>
/// 구글 캘린더의 월 보기가 다른 달력들과 갈리는 지점이 이것이다 — 여러 날에
/// 걸친 일이 칸마다 따로 찍히지 않고 <b>칸을 가로질러 한 줄로 흐른다.</b>
/// 그 줄이 서로 겹치지 않도록 자리를 나누는 계산이 여기 있다.
/// </para>
///
/// <para>
/// [화면에서 뽑아낸 이유]
/// </para>
///
/// <para>
/// 화면(<c>ProjectScheduler</c>) 안에 두면 이 계산을 <b>확인할 길이 없다.</b>
/// 잘못 담겨도 오류가 나지 않고 막대가 겹치거나 한 줄 밀려 그려질 뿐이라,
/// 사람이 눈으로 보기 전에는 아무도 모른다. 순수한 계산이므로 밖으로 꺼내
/// 테스트로 못 박는다.
/// </para>
/// </summary>
public static class CalendarLanes
{
    /// <summary>
    /// 한 주에 놓인 막대 하나의 자리.
    /// </summary>
    /// <param name="Index">넣어 준 목록에서 몇 번째 것인가 — 화면이 이것으로 원래 줄을 찾는다</param>
    /// <param name="Col">이 주의 몇째 칸에서 시작하나 (0 = 첫 칸)</param>
    /// <param name="Span">몇 칸을 가로지르나 (1 이상)</param>
    /// <param name="Lane">위에서 몇째 줄인가 (0 = 맨 위)</param>
    /// <param name="ClipStart">앞 주에서 이어져 왔나 — 왼쪽 모서리를 편다</param>
    /// <param name="ClipEnd">다음 주로 이어지나 — 오른쪽 모서리를 편다</param>
    public readonly record struct Slot(
        int Index, int Col, int Span, int Lane, bool ClipStart, bool ClipEnd);

    /// <summary>한 주는 일곱 칸이다.</summary>
    private const int Cols = 7;

    /// <summary>
    /// 기간들을 한 주(<paramref name="from"/> ~ 그 주 마지막 날)의 줄에 담는다.
    ///
    /// <para>
    /// <b>차례를 여기서 바꾸지 않는다.</b> 넣어 준 차례대로 위에서부터 자리를
    /// 잡는다 — 무엇이 위에 서야 하는지는 화면이 아는 일이고(먼저 시작한 것,
    /// 같이 시작했으면 긴 것), 여기서 또 정렬하면 두 곳이 규칙을 나눠 갖는다.
    /// </para>
    ///
    /// <para>
    /// <b>넘치는 것을 자르지 않는다.</b> 몇 줄까지 보일지는 화면 폭에 딸린
    /// 값이라 그리는 때 갈린다 — 여기서 자르면 폭이 바뀔 때마다 다시 담아야
    /// 한다. 자리는 다 주고, 감추는 것은 화면이 한다.
    /// </para>
    /// </summary>
    /// <param name="spans">기간들. 이 주에 걸치지 않는 것은 <b>알아서 건너뛴다</b>.</param>
    /// <param name="from">이 주의 첫날</param>
    /// <returns>담긴 자리들. 넣어 준 차례를 그대로 지킨다.</returns>
    public static IReadOnlyList<Slot> Pack(
        IReadOnlyList<(DateOnly Start, DateOnly End)> spans, DateOnly from)
    {
        var to = from.AddDays(Cols - 1);

        // 줄마다 「어느 칸이 찼나」를 들고 간다. 칸이 일곱뿐이라 이보다 단순한
        // 자료는 없다 — 겹침을 재는 일이 곧 칸을 보는 일이다.
        var lanes = new List<bool[]>();
        var slots = new List<Slot>(spans.Count);

        for (var i = 0; i < spans.Count; i++)
        {
            var (start, end) = spans[i];

            // 거꾸로 들어간 기간은 화면이 이미 바로잡아 준다. 그래도 여기까지
            // 흘러오면 아무 줄도 만들지 않는다 — 음수 길이를 그릴 수는 없다.
            if (end < start || start > to || end < from)
            {
                continue;
            }

            var col = start < from ? 0 : start.DayNumber - from.DayNumber;
            var endCol = end > to ? Cols - 1 : end.DayNumber - from.DayNumber;
            var lane = FreeLane(lanes, col, endCol);

            for (var c = col; c <= endCol; c++)
            {
                lanes[lane][c] = true;
            }

            slots.Add(new Slot(i, col, endCol - col + 1, lane, start < from, end > to));
        }

        return slots;
    }

    /// <summary>
    /// <paramref name="col"/> 부터 <paramref name="endCol"/> 까지가 비어 있는 첫 줄.
    /// 없으면 줄을 하나 늘린다 — <b>위에서부터 채우는 것</b>이 요점이다.
    /// 빈 자리를 건너뛰면 달력에 이 빠진 줄이 생긴다.
    /// </summary>
    private static int FreeLane(List<bool[]> lanes, int col, int endCol)
    {
        for (var lane = 0; ; lane++)
        {
            if (lane == lanes.Count)
            {
                lanes.Add(new bool[Cols]);
                return lane;
            }

            var free = true;

            for (var c = col; c <= endCol && free; c++)
            {
                free = !lanes[lane][c];
            }

            if (free)
            {
                return lane;
            }
        }
    }
}
