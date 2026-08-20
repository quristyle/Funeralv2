namespace HelpDeskServer.Utilities;

/// <summary>
/// 태그에서 추출한 원본 바이트를 DataType 별 규칙에 따라 표현 문자열로 변환한다.
/// 규칙은 <see cref="TagValueConverter.Register"/> 로 교체/추가할 수 있으므로
/// 추후 DB 에서 읽어온 규칙으로 런타임 교체가 가능하다.
/// </summary>
public interface IValueConversionRule {
  DataTypeEnum DataType { get; }
  string Convert(byte[] value, ITagCodeBook codeBook);
}

public static class TagValueConverter {
  private static readonly Dictionary<DataTypeEnum, IValueConversionRule> rules = new();

  static TagValueConverter() {
    Register(new NumberValueRule());
    Register(new DateValueRule());
    Register(new DateTimeValueRule());
    Register(new LengthValueRule());
    Register(new AddressValueRule(DataTypeEnum.DESTINATION));
    Register(new AddressValueRule(DataTypeEnum.SOURCE));
    Register(new CodeLookupValueRule(DataTypeEnum.CONTROL));
    Register(new CodeLookupValueRule(DataTypeEnum.REQUEST_CODE));
    Register(new CodeLookupValueRule(DataTypeEnum.RESPONSE_CODE));
    Register(new CodeLookupValueRule(DataTypeEnum.APP_CODE));
    Register(new SubAppValueRule());
    Register(new DataValueRule());
    Register(new DataSingleValueRule());
    Register(new EnergyLimitValueRule());
  }

  /// <summary>규칙을 등록/교체한다. DB 기반 규칙으로 바꿀 때 사용.</summary>
  public static void Register(IValueConversionRule rule) {
    rules[rule.DataType] = rule;
  }

  public static string Convert(byte[] value, DataTypeEnum dataType, ITagCodeBook codeBook) {
    if (rules.TryGetValue(dataType, out IValueConversionRule? rule)) {
      return rule.Convert(value, codeBook);
    }
    return DefaultValueRule.Instance.Convert(value, codeBook);
  }
}

internal static class DefaultValueRule {
  public static readonly IValueConversionRule Instance = new Rule();

  private sealed class Rule : IValueConversionRule {
    public DataTypeEnum DataType => DataTypeEnum.DATA;
    public string Convert(byte[] value, ITagCodeBook codeBook) {
      if (value.Length == 0) return string.Empty;
      if (value.Length >= 8) return BitConverter.ToUInt64(value, 0).ToString("N0");
      if (value.Length >= 4) return BitConverter.ToInt32(value, 0).ToString("N0");
      if (value.Length >= 2) return BitConverter.ToUInt16(value, 0).ToString("N0");
      return value[0].ToString("N0");
    }
  }
}

public sealed class NumberValueRule : IValueConversionRule {
  public DataTypeEnum DataType => DataTypeEnum.NUMBER;
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    if (value.Length < 4) return DefaultValueRule.Instance.Convert(value, codeBook);
    return BitConverter.ToUInt32(value, 0).ToString("N0");
  }
}

public sealed class DateValueRule : IValueConversionRule {
  public DataTypeEnum DataType => DataTypeEnum.DATE;
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    if (value.Length < 4) return DefaultValueRule.Instance.Convert(value, codeBook);
    ushort year = BitConverter.ToUInt16(value, 0);
    byte month = value[2];
    byte day = value[3];
    return $"{year}-{month:D2}-{day:D2}";
  }
}

public sealed class DateTimeValueRule : IValueConversionRule {
  public DataTypeEnum DataType => DataTypeEnum.DATETIME;
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    if (value.Length < 7) return DefaultValueRule.Instance.Convert(value, codeBook);
    ushort year = BitConverter.ToUInt16(value, 0);
    byte month = value[2];
    byte day = value[3];
    byte hour = value[4];
    byte minute = value[5];
    byte second = value[6];
    return $"{year}-{month:D2}-{day:D2} {hour:D2}:{minute:D2}:{second:D2}";
  }
}

public sealed class LengthValueRule : IValueConversionRule {
  public DataTypeEnum DataType => DataTypeEnum.LENGTH;
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    return value.Length == 0 ? string.Empty : value[0].ToString("N0");
  }
}

public sealed class AddressValueRule : IValueConversionRule {
  public DataTypeEnum DataType { get; }
  public AddressValueRule(DataTypeEnum dataType) {
    DataType = dataType;
  }
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    return BitConverter.ToString(value).Replace("-", " ");
  }
}

public sealed class SubAppValueRule : IValueConversionRule {
  public DataTypeEnum DataType => DataTypeEnum.SUB_APP;
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    if (value.Length == 0) return string.Empty;
    string bits = System.Convert.ToString(value[0], 2).PadLeft(8, '0');
    return $"{bits[..4]} {bits[4..]}";
  }
}

public sealed class DataValueRule : IValueConversionRule {
  public DataTypeEnum DataType => DataTypeEnum.DATA;
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    if (value.Length < 4) return DefaultValueRule.Instance.Convert(value, codeBook);
    return BitConverter.ToUInt32(value, 0).ToString("N0");
  }
}

public sealed class DataSingleValueRule : IValueConversionRule {
  public DataTypeEnum DataType => DataTypeEnum.DATA_SINGLE;
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    if (value.Length < 4) return DefaultValueRule.Instance.Convert(value, codeBook);
    return (BitConverter.ToUInt32(value, 0) / 100.0f).ToString("N2");
  }
}

public sealed class EnergyLimitValueRule : IValueConversionRule {
  public DataTypeEnum DataType => DataTypeEnum.ENERGY_LIMIT;
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    if (value.Length < 1) return DefaultValueRule.Instance.Convert(value, codeBook);
    return codeBook.TryResolve(DataType, value[0], out string? name) ? name : value[0].ToString("N0");
  }
}

/// <summary>
/// 코드-이름 매핑을 사용하는 타입(CONTROL/REQUEST_CODE/RESPONSE_CODE/APP_CODE) 공통 규칙.
/// </summary>
public sealed class CodeLookupValueRule : IValueConversionRule {
  public DataTypeEnum DataType { get; }
  public CodeLookupValueRule(DataTypeEnum dataType) {
    DataType = dataType;
  }
  public string Convert(byte[] value, ITagCodeBook codeBook) {
    if (value.Length == 0) return string.Empty;

    uint code = value.Length switch {
      1 => value[0],
      2 => BitConverter.ToUInt16(value, 0),
      _ => BitConverter.ToUInt32(value, 0)
    };

    if (codeBook != null && codeBook.TryResolve(DataType, code, out string? name)) {
      return name;
    }
    return code.ToString("N0");
  }
}
