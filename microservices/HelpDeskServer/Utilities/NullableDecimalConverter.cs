using System.Text.Json;
using System.Text.Json.Serialization;

namespace HelpDeskServer.Utilities;

/// <summary>
/// JSON 역직렬화 시 빈 문자열("")을 decimal? 타입의 null로 변환하는 JsonConverter.
/// </summary>
public class NullableDecimalConverter : JsonConverter<decimal?> {
  /// <inheritdoc />
  public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    if (reader.TokenType == JsonTokenType.String) {
      string? stringValue = reader.GetString();
      if (string.IsNullOrWhiteSpace(stringValue)) {
        return null;
      }
      if (decimal.TryParse(stringValue, out decimal value)) {
        return value;
      }
    }

    if (reader.TokenType == JsonTokenType.Number) {
      return reader.GetDecimal();
    }

    if (reader.TokenType == JsonTokenType.Null) {
      return null;
    }

    throw new JsonException($"Unable to convert value to decimal?. Token type was {reader.TokenType}.");
  }

  /// <inheritdoc />
  public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options) {
    if (value.HasValue) {
      writer.WriteNumberValue(value.Value);
    }
    else {
      writer.WriteNullValue();
    }
  }
}