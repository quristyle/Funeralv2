using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>
/// 메일 첨부를 <b>보내기 전에</b> 본다 — 개수 · 총량 · base64 가 성한지.
/// </summary>
/// <remarks>
/// <para>
/// [왜 보내기 전인가]
/// </para>
/// <para>
/// SMTP 에 붙인 뒤 거절당하면 <b>어느 파일이 문제인지 알 수 없다.</b> 메일
/// 서버마다 문구가 달라 화면이 옮길 말도 없고, 대개 20~25MB 어디쯤에서
/// 거절당하는데 그 선이 적혀 있는 곳도 없다. 여기서 걸면 파일 이름을 짚어
/// 말할 수 있다.
/// </para>
/// <para>
/// [상한을 한 곳에 둔다]
/// </para>
/// <para>
/// 메일을 내는 자리가 둘이다 — 알림 직발송(<c>EmailEndpoints</c>)과 포털의
/// 「메일 보내기」(<c>MailEndpoints</c>). 상한을 양쪽에 적어 두면 어느 날
/// 한쪽만 늘어나고, 그 어긋남은 <b>「화면에서는 되는데 서버가 거절한다」</b>로
/// 나타난다. 화면도 같은 값을 쓰지만 그쪽은 <b>먼저 말해 주려는 것</b>이라
/// 여기서 다시 본다 — 다른 길로 들어온 요청이 있을 수 있다.
/// </para>
/// </remarks>
public static class EmailAttachmentGuard
{
    /// <summary>첨부 개수 상한. 메일 서버가 세는 값은 아니고 <b>화면과 맞춘 값</b>이다.</summary>
    public const int MaxCount = 5;

    /// <summary>첨부 총량 상한. <b>메일 서버가 거절하기 전에 우리가 말한다.</b></summary>
    public const long MaxBytes = 15L * 1024 * 1024;

    /// <summary>
    /// 첨부를 검사한다. 걸리면 <paramref name="error"/> 에 사람이 읽는 말이,
    /// <paramref name="code"/> 에 부르는 쪽이 가를 코드가 담긴다.
    /// </summary>
    /// <returns>보내도 되면 <c>true</c>.</returns>
    public static bool TryCheck(
        IReadOnlyList<EmailAttachmentDto> files,
        out long totalBytes,
        out string? error,
        out string? code)
    {
        totalBytes = 0;
        error = null;
        code = null;

        if (files.Count > MaxCount)
        {
            error = $"첨부는 {MaxCount}개까지입니다.";
            code = "TOO_MANY_FILES";
            return false;
        }

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                error = "첨부 파일 이름이 비어 있습니다.";
                code = "INVALID";
                return false;
            }

            // base64 가 깨져 있으면 여기서 잡는다. 보내다 던지면 502 가 되어
            // 「메일 서버가 거절했다」로 읽힌다 — 사실은 우리가 보낸 값이 틀렸다.
            if (!TryDecode(file.Content, out var bytes))
            {
                error = $"「{file.FileName}」 을 읽지 못했습니다.";
                code = "BAD_ATTACHMENT";
                return false;
            }

            totalBytes += bytes;
        }

        if (totalBytes > MaxBytes)
        {
            error = $"첨부가 모두 합쳐 {MaxBytes / 1024 / 1024}MB 를 넘습니다 "
                    + $"(지금 {totalBytes / 1024 / 1024}MB). 메일 서버가 거절합니다.";
            code = "ATTACHMENT_TOO_LARGE";
            return false;
        }

        return true;
    }

    /// <summary>
    /// base64 를 <b>길이만 확인하고 버린다.</b> 여기서 바이트를 들고 있어 봐야
    /// 보내는 쪽이 다시 디코딩하므로, 큰 파일을 두 벌 메모리에 올리게 된다.
    /// </summary>
    private static bool TryDecode(string? content, out long bytes)
    {
        bytes = 0;

        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        var buffer = new byte[((content.Length * 3) + 3) / 4];

        if (!Convert.TryFromBase64String(content, buffer, out var written))
        {
            return false;
        }

        bytes = written;
        return written > 0;
    }
}
