namespace JSini.Shared.DTOs;

/// <summary>날씨 푸시 아이콘 분류.</summary>
public static class WeatherIconKinds
{
    public const string Sunny = "sunny";
    public const string Cloudy = "cloudy";
    public const string Rain = "rain";
    public const string Snow = "snow";
    public const string Sleet = "sleet";
    public const string Storm = "storm";
    public const string Wind = "wind";
    public const string Heat = "heat";
    public const string Cold = "cold";
    public const string Temperature = "temperature";
    public const string Humidity = "humidity";
    public const string Warning = "warning";

    /// <summary>날씨 설명을 아이콘 분류로 바꾼다.</summary>
    public static string FromCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return Cloudy;

        if (condition.Contains("천둥", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("번개", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("낙뢰", StringComparison.OrdinalIgnoreCase))
            return Storm;

        if (condition.Contains("비/눈", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("진눈깨비", StringComparison.OrdinalIgnoreCase))
            return Sleet;

        if (condition.Contains("눈", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("대설", StringComparison.OrdinalIgnoreCase))
            return Snow;

        if (condition.Contains("비", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("소나기", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("빗방울", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("호우", StringComparison.OrdinalIgnoreCase))
            return Rain;

        if (condition.Contains("바람", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("풍랑", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("태풍", StringComparison.OrdinalIgnoreCase))
            return Wind;

        if (condition.Contains("구름", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("흐림", StringComparison.OrdinalIgnoreCase))
            return Cloudy;

        if (condition.Contains("맑음", StringComparison.OrdinalIgnoreCase)
            || condition.Contains("쾌청", StringComparison.OrdinalIgnoreCase))
            return Sunny;

        return Cloudy;
    }

    /// <summary>실황 기준의 측정 분류를 아이콘 분류로 바꾼다.</summary>
    public static string FromStandardCategory(string? category) =>
        category?.Trim().ToUpperInvariant() switch
        {
            "WIND" or "WSD" => Wind,
            "RAIN" or "RN1" => Rain,
            "SNOW" => Snow,
            "HEAT" => Heat,
            "COLD" => Cold,
            "REH" => Humidity,
            "T1H" => Temperature,
            _ => Warning,
        };

    /// <summary>특보 종류 문구를 아이콘 분류로 바꾼다.</summary>
    public static string FromWarning(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return Warning;

        if (summary.Contains("낙뢰", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("뇌우", StringComparison.OrdinalIgnoreCase))
            return Storm;
        if (summary.Contains("대설", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("눈", StringComparison.OrdinalIgnoreCase))
            return Snow;
        if (summary.Contains("호우", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("강우", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("비", StringComparison.OrdinalIgnoreCase))
            return Rain;
        if (summary.Contains("강풍", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("풍랑", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("태풍", StringComparison.OrdinalIgnoreCase))
            return Wind;
        if (summary.Contains("폭염", StringComparison.OrdinalIgnoreCase))
            return Heat;
        if (summary.Contains("한파", StringComparison.OrdinalIgnoreCase))
            return Cold;

        return Warning;
    }

    /// <summary>알려진 분류만 허용하고 알 수 없는 값은 기본 날씨 아이콘으로 접는다.</summary>
    public static string Normalize(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            Sunny => Sunny,
            Cloudy => Cloudy,
            Rain => Rain,
            Snow => Snow,
            Sleet => Sleet,
            Storm => Storm,
            Wind => Wind,
            Heat => Heat,
            Cold => Cold,
            Temperature => Temperature,
            Humidity => Humidity,
            Warning => Warning,
            _ => Cloudy,
        };
}
