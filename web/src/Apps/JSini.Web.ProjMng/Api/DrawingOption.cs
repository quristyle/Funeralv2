namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 「그림」 고르개 한 칸 — DB 접속 하나에 저장된 그림 중 하나.
/// </summary>
/// <remarks>
/// <para>
/// ERD 화면과 업무 흐름 화면이 <b>같은 줄들을 나눠 본다</b>(두 화면 다
/// <c>dev_db_prop</c> 을 읽는다). 고르개를 화면마다 따로 두면 이름을 붙이는
/// 규칙이 갈라지고, 갈라지는 쪽은 언제나 「어느 화면으로 열었느냐에 따라
/// 목록이 다르다」가 된다. 그래서 여기 한 벌만 둔다.
/// </para>
///
/// <para>
/// <b>이름이 아니라 줄을 들고 다닌다.</b> 이 표에는 제약도 인덱스도 없어서
/// 같은 이름이 두 줄일 수 있다 — 운영 자료에 <c>(db_rid 4, 'ER Diagram')</c>
/// 이 둘이고 <b>내용이 서로 다르다</b>. 이름만 담으면 어느 쪽을 연 것인지 알
/// 수 없고, 저장이 엉뚱한 줄을 덮는다.
/// </para>
/// </remarks>
/// <param name="Prop">저장본 한 줄. 저장은 이 줄의 <c>DbPrid</c> 로 찾는다.</param>
/// <param name="Label">고르개에 보일 이름.</param>
public sealed record DrawingOption(ProjectDbPropDto Prop, string Label)
{
    /// <summary>
    /// 속성 목록에서 <b>그림만</b> 골라 고르개 항목으로 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 같은 표에 공통코드 질의(<c>code_master</c> · <c>code_detail</c>)와
    /// 프로시저 서식(<c>sp_fmt</c>)이 함께 들어 있다. 그 이름이 고르개에
    /// 섞이면 <b>열었을 때 빈 캔버스가 뜨고 이유를 알 수 없다</b> —
    /// <see cref="ErdModel.IsDiagram"/> 이 가른다.
    /// </para>
    ///
    /// <para>
    /// <b>보관본은 뺀다.</b> 옛 도구(draw.io)의 그림을 옮기면서 원본을 같은
    /// 이름의 새 줄로 남겨 두었는데(<c>db_ptype = 'MXGRAPH'</c>,
    /// <c>scripts/projmng-drawio-to-diagram.py</c>), 그것도 「그림」이라
    /// 거르지 않으면 고르개에 <b>같은 이름이 두 벌</b> 뜬다. 골라 봐야
    /// 「옛 도구로 그린 그림이라 열지 못한다」만 나온다.
    /// </para>
    ///
    /// <para>
    /// 번호(<c>db_prid</c>)는 <b>이름이 겹칠 때만</b> 붙인다. 안 겹치는데도
    /// 붙이면 읽는 사람에게는 군더더기다.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<DrawingOption> From(IReadOnlyList<ProjectDbPropDto> props)
    {
        var drawings = props
            .Where(p => ErdModel.IsDiagram(p.DbPvalue) && !IsArchive(p))
            .ToList();

        var twice = drawings
            .GroupBy(p => p.DbPkey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. drawings.Select(p => new DrawingOption(
                p,
                twice.Contains(p.DbPkey ?? string.Empty)
                    ? $"{p.DbPkey} #{p.DbPrid}"
                    : p.DbPkey ?? $"#{p.DbPrid}"))
        ];
    }

    /// <summary>
    /// 옛 도구 원본을 남겨 둔 줄인가. 고르개에서 뺀다.
    /// </summary>
    private static bool IsArchive(ProjectDbPropDto prop) =>
        string.Equals(prop.DbPtype, ArchiveType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 옛 도구 원본을 남겨 둔 줄의 표시. <b>옮기는 스크립트와 같은 글자여야 한다</b>
    /// (<c>scripts/projmng-drawio-to-diagram.py</c> 의 <c>DB_BACKUP_TYPE</c>).
    /// 어긋나도 오류가 나지 않고 <b>고르개에 보관본이 섞여 보일 뿐</b>이다.
    /// </summary>
    public const string ArchiveType = "MXGRAPH";

    /// <summary>
    /// 열어 둘 그림을 고른다 — <b>보던 것 → 기본 이름 → 첫 그림</b> 순서.
    /// </summary>
    /// <remarks>
    /// 접속을 바꿨을 때 <b>빈 화면을 주지 않는다.</b> 그림이 있는데 아무것도
    /// 안 열려 있으면 사람은 「이 접속에는 그림이 없구나」로 읽는다.
    /// </remarks>
    /// <param name="drawings">이 접속의 그림들.</param>
    /// <param name="keep">보고 있던 줄. 있으면 그것을 잇는다.</param>
    /// <param name="fallbackKey">기본 이름(<c>erd</c>).</param>
    public static DrawingOption? Resume(
        IReadOnlyList<DrawingOption> drawings, DrawingOption? keep, string fallbackKey) =>
        drawings.FirstOrDefault(d => d.Prop.DbPrid == keep?.Prop.DbPrid)
        ?? drawings.FirstOrDefault(d =>
               string.Equals(d.Prop.DbPkey, fallbackKey, StringComparison.OrdinalIgnoreCase))
        ?? drawings.FirstOrDefault();
}
