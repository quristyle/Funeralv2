using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace HelpDeskServer.Utilities
{
    /// <summary>
    /// JSON 직렬화/역직렬화 시 "yyyy-MM-dd" 형식의 문자열과 DateTime? 타입을 변환하는 JsonConverter.
    /// </summary>
    public class DateOnlyConverter : JsonConverter<DateTime?>
    {
        private const string DateFormat = "yyyy-MM-dd";

        /// <inheritdoc />
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? dateString = reader.GetString();
                if (dateString != null)
                {
                    DateTime parsedDate;
                    // yyyy-MM-dd 형식으로 파싱 시도
                    if (DateTime.TryParseExact(dateString, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                    {
                        return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                    }
                    // ISO 8601 형식으로도 파싱 시도 (fallback)
                    if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out parsedDate))
                    {
                        // AdjustToUniversal은 이미 UTC로 조정하거나, Unspecified를 UTC로 간주합니다.
                        return parsedDate;
                    }
                }
            }
            return null; 
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToString(DateFormat, CultureInfo.InvariantCulture));
        }
    }
}