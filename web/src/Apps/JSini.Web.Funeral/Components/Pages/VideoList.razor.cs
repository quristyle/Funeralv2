namespace JSini.Web.Funeral.Components.Pages;

public partial class VideoList
{
    /// <summary>재처리 단추들. 파생물이 없으면 비운다.</summary>
    private static readonly IReadOnlyList<(string Kind, string Label)> Retries = [("thumbnail", "미리보기"), ("webm", "webm")];
}
