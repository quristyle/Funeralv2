using Microsoft.AspNetCore.Components;

namespace JSini.PublicSite.Components.Layout;

public partial class RichText
{
    /// <summary>DB 본문. 비어 있으면 아무것도 그리지 않는다.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>바깥에서 주는 글자 크기·색 등.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>문단 사이 여백 클래스.</summary>
    [Parameter] public string Gap { get; set; } = "mt-4";

    private sealed record Part(string Text, bool Bold);

    private IEnumerable<List<Part>> Paragraphs =>
        (Text ?? string.Empty)
            .Split("\n\n", StringSplitOptions.None)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(Split);

    /// <summary>
    /// 한 문단을 굵은 조각과 보통 조각으로 자른다.
    /// `**` 개수가 홀수여도 깨지지 않는다 — 마지막 조각이 그냥 보통 글씨가 된다.
    /// </summary>
    private static List<Part> Split(string paragraph) =>
        [.. paragraph.Split("**").Select((text, i) => new Part(text, i % 2 == 1))];
}
