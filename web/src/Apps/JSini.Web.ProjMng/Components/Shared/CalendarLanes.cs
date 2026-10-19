namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>월 달력에서 한 주 안의 막대를 줄(lane)에 나눠 담는다. 화면 안에 두면 확인할 길이 없어 순수 계산으로 꺼내 테스트로 못 박는다.</summary>
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

    /// <summary>기간들을 한 주의 줄에 담는다. 차례는 바꾸지 않고(화면이 정한 순서), 넘치는 줄도 자르지 않는다 — 몇 줄을 보일지는 그리는 때 정한다.</summary>
    /// <param name="spans">기간들. 이 주에 걸치지 않는 것은 <b>알아서 건너뛴다</b>.</param>
    /// <param name="from">이 주의 첫날</param>
    /// <returns>담긴 자리들. 넣어 준 차례를 그대로 지킨다.</returns>
    public static IReadOnlyList<Slot> Pack(
        IReadOnlyList<(DateOnly Start, DateOnly End)> spans, DateOnly from)
    {
        var to = from.AddDays(Cols - 1);

        // 줄마다 어느 칸이 찼는지 든다. 칸이 일곱뿐이라 이보다 단순한 자료는 없다.
        var lanes = new List<bool[]>();
        var slots = new List<Slot>(spans.Count);

        for (var i = 0; i < spans.Count; i++)
        {
            var (start, end) = spans[i];

            // 거꾸로 든 기간은 화면이 바로잡아 준다. 여기까지 오면 줄을 만들지 않는다(음수 길이).
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

    /// <summary>col 부터 endCol 까지 비어 있는 첫 줄. 없으면 줄을 하나 늘린다 — 위에서부터 채워야 빈 줄이 안 생긴다.</summary>
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
