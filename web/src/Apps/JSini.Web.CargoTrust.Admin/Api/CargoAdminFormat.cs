using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace JSini.Web.CargoTrust.Admin.Api;

/// <summary>
/// 금액 · 날짜를 화면에 적는 모양. 화면 열 곳이 같은 글자를 쓰게 한 곳에 둔다.
/// </summary>
public static class CargoAdminFormat
{
    /// <summary>「1,234,000원」.</summary>
    public static string Won(decimal amount) =>
        amount.ToString("#,0", CultureInfo.InvariantCulture) + "원";

    /// <summary>「2026-09-24」. 없으면 「-」 — 빈칸은 「아직 안 읽혔다」로 보인다.</summary>
    public static string Day(DateTime? day) =>
        day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-";

    /// <summary>「2026-09-24 13:05」. 서버가 UTC 로 주므로 이 장비의 시각으로 옮긴다.</summary>
    public static string Moment(DateTime? at) =>
        at is null ? "-"
        : (at.Value.Kind == DateTimeKind.Utc ? at.Value.ToLocalTime() : at.Value)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>조회 조건에 싣는 날짜 — 서버의 DateOnly 가 읽는 모양.</summary>
    public static string? QueryDay(DateTime? day) =>
        day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// 감사 기록의 전·후 JSON 을 사람이 읽을 모양으로 편다.
/// </summary>
/// <remarks>
/// <para>
/// [글자로 싸여 오든 객체로 오든 같은 결과를 낸다]
/// </para>
///
/// <para>
/// 계약서는 「JSON 문자열」이라 적었다. 서버가 그대로 따르면 값이
/// <c>"{\"status\":\"ACTIVE\"}"</c> 처럼 한 번 더 싸여 오고, JSONB 를 그대로 흘리면
/// 객체로 온다. 앞엣것을 그냥 찍으면 역슬래시투성이 한 줄이 된다. 글자면 한 번 더
/// 풀어 보고, 풀리지 않으면(JSON 이 아닌 글자) 그 글자를 그대로 보인다.
/// </para>
///
/// <para>
/// 한글은 이스케이프하지 않는다 — 기본 인코더는 「정상」을 <c>정상</c> 로
/// 적어서 전·후를 눈으로 대조할 수 없다. 이 글자는 <c>&lt;pre&gt;</c> 안에 Razor 가
/// 다시 HTML 인코딩해서 넣으므로 느슨한 인코더를 써도 안전하다.
/// </para>
/// </remarks>
public static class CargoAdminJson
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>들여 쓴 JSON. 값이 없으면 <c>null</c>.</summary>
    public static string? Format(JsonElement? value)
    {
        if (value is not { } element
            || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                using var inner = JsonDocument.Parse(text);
                return JsonSerializer.Serialize(inner.RootElement, Pretty);
            }
            catch (JsonException)
            {
                return text;
            }
        }

        return JsonSerializer.Serialize(element, Pretty);
    }
}
