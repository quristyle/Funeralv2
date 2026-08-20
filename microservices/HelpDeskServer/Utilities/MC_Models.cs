
using System.Security.Permissions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Utilities;

/// <summary>
/// 특정 장비에 대한 정보
/// </summary>
public class MC_Models {
  [Key]
  public int Id { get; set; }
  public string MC_NAME { get; set; } = string.Empty;
  // 전문의 시작 코드값을 지정하는 속성, 예: "AA 55" 또는 "55 AA" 등
  public string StartKey { get; set; } = string.Empty;
  public List<ParseItem> ParseItems { get; set; } = new List<ParseItem>();

  public List<MC_ACK_FIND> AckFinds { get; set; } = new List<MC_ACK_FIND>();
  public List<HelpDeskServer.Models.BinarySample> Samples { get; set; } = new List<HelpDeskServer.Models.BinarySample>();

  [NotMapped]
  public ITagCodeBook CodeBook { get; set; } = null!;

  public ParseItem AddItem(string desc, string ptype, int keyIdx, IEnumerable<byte> keys, string bptype = "number", string plength = "8") {
    ParseItem temp = new ParseItem { Desc = desc, PTYPE = ptype, KeyIdx = keyIdx, Keys = keys.ToList(), BlocParseType = bptype, BlocParseLength = plength };
    ParseItems.Add(temp);
    return temp;
  }
  public ParseItem? FindMatchingItem(string matchPType, byte[] lineBytes) {
      return ParseItems.FirstOrDefault(item => item.IsMatch(matchPType, lineBytes));
  }
  public MC_Models CreateModel(string mc_name, string ptype, string startKey, string desc, int keyIdx, IEnumerable<byte> keys, string bptype = "date", string plength = "8") {
    this.MC_NAME = mc_name;
    this.StartKey = startKey;
    AddItem(desc,ptype, keyIdx, keys, ptype, plength);
    return this;
  }



}


// 끊어진 ACK 전문을 찾기 위한 조건을 담는 클래스
public class MC_ACK_FIND {
  [Key]
  public int Id { get; set; }

  public int MC_ModelsId { get; set; }
  
 // RX start point 
  public string startCalcArrow{get;set;} = "up";
  public string startCalcTarget{get;set;} = "TX";
  public string startCalcIdx{get;set;} = "10";
  public string startCalcValue{get;set;} = "80";
  public string startCalcEquals{get;set;} = "not";


 // RX end point
  public string endCalcArrow{get;set;} = "up";
  public string endCalcTarget{get;set;} = "TX";
  public string endCalcIdx{get;set;} = "10";
  public string endCalcValue{get;set;} = "80";
  public string endCalcEquals{get;set;} = "not";

}


/// <summary>
/// 하나의 전문에 대한 정보
/// </summary>
public class ParseItem {
    [Key]
    public int Id { get; set; }
    public int MC_ModelsId { get; set; }

    public string Desc { get; set; } = string.Empty;

    public string PTYPE {get;set;} = "RX"; // RX, TX 구분

    public int KeyIdx { get; set; } // 키가 되는 바이트의 인덱스 위치 (0부터 시작)
    public List<byte> Keys { get; set; } = new List<byte>();

    public string BlocParseType { get; set; } = "number"; // "date" or "number", 블록 분석 방식 지정
    public string BlocParseLength { get; set; } = "8";//"4,2,1,1"; //"8"; // 8바이트 단위로 블록 분석

    public string Separator => string.Join(" ", Keys.Select(k => k.ToString("X2")));

    public bool IsMatch( string matchPType, byte[] lineBytes) {

      Console.WriteLine($"Checking ParseItem: {Desc}, KeyIdx: {KeyIdx}, Keys: {Separator} against lineBytes length: {lineBytes.Length}");


        if (Keys.Count == 0 || lineBytes.Length < KeyIdx + Keys.Count) return false;
        var extractedKeys = lineBytes.Skip(KeyIdx).Take(Keys.Count).ToArray();
        Console.WriteLine($"Checking ParseItem: {Desc}, KeyIdx: {KeyIdx}, Keys: {Separator} against lineBytes: {BitConverter.ToString(lineBytes)}");
        Console.WriteLine($"Extracted keys: {string.Join(" ", extractedKeys.Select(k => k.ToString("X2")))}");
        return PTYPE == matchPType && Keys.SequenceEqual(extractedKeys);
    }


   public List<TagItem> TagItems { get; set; } = new List<TagItem>();

   public override string ToString() {
    return $"ParseItem: {Desc}, PTYPE: {PTYPE}, KeyIdx: {KeyIdx}, Keys: {Separator}, BlocParseType: {BlocParseType}, BlocParseLength: {BlocParseLength}";
   }
}



/// <summary>
/// 하나의 전문에 소속된 테그 정보
/// </summary>
public class TagItem {
    [Key]
    public int Id { get; set; }
    public int ParseItemId { get; set; }

    public string Desc { get; set; } = string.Empty;


    public int TagIdx { get; set; } // 키가 되는 바이트의 인덱스 위치 (0부터 시작)
    public int TagLength { get; set; } // 키가 되는 바이트의 길이

    public int SortNo { get; set; }

    public DataTypeEnum DataType { get; set; } = DataTypeEnum.NUMBER; // "date" or "number", 블록 분석 방식 지정

    /// <summary>
    /// 태그가 가리키는 원본 바이트 구간을 반환한다.
    /// 표현 문자열로의 변환은 <see cref="ValueConverter"/> 에서 수행한다.
    /// </summary>
    public byte[] getValue(byte[] bytes) {
      if (TagLength <= 0 || TagIdx < 0 || TagIdx + TagLength > bytes.Length) {
        return Array.Empty<byte>();
      }
      byte[] result = new byte[TagLength];
      Array.Copy(bytes, TagIdx, result, 0, TagLength);
      return result;
    }
}


public interface ITagCodeBook {
  bool TryResolve(DataTypeEnum dataType, uint code, out string value);
}

public class InMemoryTagCodeBook : ITagCodeBook {
  private readonly Dictionary<(DataTypeEnum DataType, uint Code), string> map;

  public InMemoryTagCodeBook(Dictionary<(DataTypeEnum DataType, uint Code), string> map) {
    this.map = map;
  }

  public bool TryResolve(DataTypeEnum dataType, uint code, out string value) {
    return map.TryGetValue((dataType, code), out value!);
  }
}

public enum DataTypeEnum{
  NUMBER,
  DATE,
  DATETIME,
  LENGTH,
  DESTINATION,
  SOURCE,
  CONTROL,
  SUB_APP,
  REQUEST_CODE,
  RESPONSE_CODE,
  APP_CODE,
  DATA,
  DATA_SINGLE,
  ENERGY_LIMIT
}


public class BinaryCalcInfo {
    public string OriginalContent { get; set; } = string.Empty;
    public string TargetContent { get; set; } = string.Empty;

    public string TargetContentNotCRC { get; set; } = string.Empty;
    public string TargetContentNotHead { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public List<object> Results { get; set; } = new List<object>();
}

