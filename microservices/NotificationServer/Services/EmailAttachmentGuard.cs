using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>메일 첨부를 보내기 전에 본다: 개수·총량·base64 가 성한지. SMTP 가 거절하면 어느 파일인지 알 수 없어 여기서 파일 이름을 짚는다.
/// 상한은 알림 직발송과 포털 메일 보내기가 같이 쓴다(따로 적으면 한쪽만 늘어난다).</summary>
public static class EmailAttachmentGuard
{
    /// <summary>첨부 개수 상한. 메일 서버가 세는 값은 아니고 <b>화면과 맞춘 값</b>이다.</summary>
    public const int MaxCount = 5;

    /// <summary>첨부 총량 상한. <b>메일 서버가 거절하기 전에 우리가 말한다.</b></summary>
    public const long MaxBytes = 15L * 1024 * 1024;

    /// <summary>첨부를 검사한다. 걸리면 error 에 사람이 읽는 말이, code 에 부르는 쪽이 가를 코드가 담긴다.</summary>
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

            // base64 가 깨졌으면 여기서 잡는다(보내다 던지면 502 가 되어 메일 서버가 거절한 것처럼 읽힌다).
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

    /// <summary>base64 는 길이만 확인하고 버린다(바이트를 들고 있으면 보내는 쪽이 다시 디코딩해 큰 파일이 두 벌 메모리에 오른다).</summary>
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
