namespace JSini.Web.ProjMng.Api;

/// <summary>「그림」 고르개 한 칸. DB 접속 하나에 저장된 그림 중 하나다.
/// ERD 화면과 흐름 화면이 같은 줄을 나눠 보므로 여기 한 벌만 둔다. 이름이 아니라 줄(DbPrid)을 들고 다닌다 — 같은 이름이 두 줄일 수 있다.</summary>
/// <param name="Prop">저장본 한 줄. 저장은 이 줄의 <c>DbPrid</c> 로 찾는다.</param>
/// <param name="Label">고르개에 보일 이름.</param>
public sealed record DrawingOption(ProjectDbPropDto Prop, string Label)
{
    /// <summary>속성 목록에서 그림만 골라 고르개 항목으로 만든다. 같은 표의 공통코드·서식 질의를 거르지 않으면 빈 캔버스가 뜬다(<see cref="ErdModel.IsDiagram"/>).
    /// 보관본(MXGRAPH)은 뺀다 — 같은 이름이 두 벌 뜬다. 번호는 이름이 겹칠 때만 붙인다.</summary>
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

    /// <summary>옛 도구 원본(보관본)인가. 고르개에서 뺀다.</summary>
    private static bool IsArchive(ProjectDbPropDto prop) =>
        string.Equals(prop.DbPtype, ArchiveType, StringComparison.OrdinalIgnoreCase);

    /// <summary>옛 도구 원본 표시. 옮기는 스크립트(scripts/projmng-drawio-to-diagram.py)와 같은 글자여야 한다 — 어긋나면 보관본이 고르개에 섞여 보인다.</summary>
    public const string ArchiveType = "MXGRAPH";

    /// <summary>열어 둘 그림을 고른다 — 보던 것 → 기본 이름 → 첫 그림 순서. 접속을 바꿨을 때 빈 화면을 주지 않는다.</summary>
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
