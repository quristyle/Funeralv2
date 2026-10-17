namespace JSini.Web.Models;

/// <summary>
/// 레이아웃 상단 도구 아이콘 DTO
/// </summary>
public sealed record LayoutHeaderIconDto(
    string Id,
    string IconKey,
    string Title,
    string Icon,
    string? RouteKey,
    string? Path,
    bool DefaultEnabled,
    int OrderNo,
    string? Description
);
