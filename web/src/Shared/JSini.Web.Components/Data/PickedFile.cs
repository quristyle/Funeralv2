namespace JSini.Web.Components.Data;

/// <summary>사용자가 고르고 셸 서버가 받아 둔 파일 하나(D5).</summary>
/// <param name="Name">원본 파일 이름. 서버에 그대로 보낸다.</param>
/// <param name="ContentType">MIME 타입. 브라우저가 모르면 application/octet-stream.</param>
/// <param name="Size">바이트 수</param>
/// <param name="TempPath">셸 서버의 임시 파일 경로. 브라우저 스트림은 고르는 상호작용 동안에만 유효해 고르는 즉시 받아 두고, 이 파일은 FilePicker 가 사라질 때 지운다.</param>
public sealed record PickedFile(string Name, string ContentType, long Size, string TempPath)
{
    /// <summary>올릴 때 읽는다. 다 읽고 나면 닫는다.</summary>
    public Stream OpenRead() => File.OpenRead(TempPath);
}
