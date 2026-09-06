namespace JSini.Web.Components.Data;

/// <summary>
/// 사용자가 고르고 <b>셸 서버가 받아 둔</b> 파일 하나 (D5).
/// </summary>
/// <param name="Name">원본 파일 이름. 서버에 그대로 보낸다.</param>
/// <param name="ContentType">MIME 타입. 브라우저가 모르면 <c>application/octet-stream</c>.</param>
/// <param name="Size">바이트 수</param>
/// <param name="TempPath">
/// 셸 서버의 임시 파일 경로.
///
/// <para>
/// <b>브라우저 스트림을 그대로 들고 있지 않는 이유</b> — <c>IBrowserFile</c> 의
/// 스트림은 그 상호작용 동안에만 유효하다. 파일을 고른 뒤 폼을 마저 채우고
/// 저장을 누르면 그때는 못 읽는 경우가 생기고, 증상이 「어제는 됐는데」라
/// 원인을 찾기 어렵다. 고르는 순간 받아 두고 저장할 때 이것을 읽는다.
/// </para>
///
/// <para>
/// 이 파일은 <c>FilePicker</c> 가 사라질 때 지운다. 화면이 따로 지울 필요가 없다.
/// </para>
/// </param>
public sealed record PickedFile(string Name, string ContentType, long Size, string TempPath)
{
    /// <summary>올릴 때 읽는다. 다 읽고 나면 닫는다.</summary>
    public Stream OpenRead() => File.OpenRead(TempPath);
}
