// HelpDeskServer/Utilities/NullableIntConverter.cs (새 파일)

using System.Text.Json;
using System.Text.Json.Serialization;

namespace HelpDeskServer.Utilities;

/// <summary>
/// JSON 역직렬화 시 빈 문자열("")을 int? 타입의 null로 변환하는 JsonConverter.
/// </summary>
public class NullableIntConverter : JsonConverter<int?>
{
    /// <inheritdoc />
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 토큰이 문자열 타입일 때
        if (reader.TokenType == JsonTokenType.String)
        {
            string? stringValue = reader.GetString();
            // 문자열이 비어있거나 공백이면 null 반환
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return null;
            }
            // 숫자 형태의 문자열이면 int로 변환하여 반환
            if (int.TryParse(stringValue, out int value))
            {
                return value;
            }
        }

        // 토큰이 숫자 타입일 때
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        // 토큰이 null 리터럴일 때
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // 그 외의 경우, 변환할 수 없으므로 예외 발생
        throw new JsonException($"Unable to convert value to int?. Token type was {reader.TokenType}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        // int? 값을 JSON으로 쓸 때는 기본 동작을 따릅니다.
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
