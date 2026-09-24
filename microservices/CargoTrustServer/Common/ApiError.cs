using JSini.Shared.DTOs;

namespace CargoTrustServer.Common;

/// <summary>
/// 실패 응답. 계약(05-api-design.md 「경로와 봉투」)대로 상태코드와 봉투 코드를 짝지어 낸다.
///
/// 메시지는 화면이 그대로 띄운다 — 「무엇을 고치면 되는지」가 읽혀야 한다.
/// </summary>
public static class ApiError
{
    public static IResult BadRequest(string message) =>
        Results.BadRequest(ApiResponse<object>.Fail(message, "E400"));

    public static IResult Unauthorized(string message) =>
        Results.Json(ApiResponse<object>.Fail(message, "E401"), statusCode: StatusCodes.Status401Unauthorized);

    public static IResult Forbidden(string message) =>
        Results.Json(ApiResponse<object>.Fail(message, "E403"), statusCode: StatusCodes.Status403Forbidden);

    public static IResult NotFound(string message) =>
        Results.NotFound(ApiResponse<object>.Fail(message, "E404"));

    public static IResult Conflict(string message) =>
        Results.Conflict(ApiResponse<object>.Fail(message, "E409"));

    /// <summary>
    /// 돌려줄 것이 없는 성공(삭제 · 빈 조회).
    /// <c>Results.Ok(null)</c> 은 응답 래퍼가 감싸지 않고 맨 <c>null</c> 을 내보내므로 봉투를 직접 만든다.
    /// </summary>
    public static IResult Empty() => Results.Ok(ApiResponse<object>.Ok(null));
}

/// <summary>입력 검사 도우미. 실패하면 이유 문자열, 통과하면 null.</summary>
public static class Check
{
    /// <summary>DB 열 길이를 넘으면 저장 단계에서 500 이 난다 — 그 전에 400 으로 돌려보낸다.</summary>
    public static string? MaxLength(string? value, int max, string label) =>
        value is not null && value.Length > max ? $"{label}은(는) {max}자까지입니다." : null;

    /// <summary>앞뒤 공백을 걷고, 비었으면 null 로.</summary>
    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>NUMERIC(15,2) 의 상한.</summary>
    public const decimal MaxAmount = 9_999_999_999_999.99m;
}
