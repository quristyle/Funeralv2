using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ProjModel;


public static class BaseModelExtensions {




  public static Dictionary<string, string> ToDictionary(this BaseModel wbs) {
    var dict = new Dictionary<string, string>();
    var props = wbs.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

    foreach (var prop in props) {
      var value = prop.GetValue(wbs);

      // DateTime 또는 DateTime? 처리
      if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?)) {
        var dt = value as DateTime?;
        dict[prop.Name.ToLower()] = dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
      }
      //else if (prop.PropertyType == typeof(DateOnly) || prop.PropertyType == typeof(DateOnly?)) {
      //  var dt = value as DateOnly?;
      //  dict[prop.Name] = dt.HasValue ? dt.Value.ToString("yyyy-MM-dd") : "";
      //}
      else if (value != null) {
        dict[prop.Name.ToLower()] = value.ToString();
      }
      else {
        dict[prop.Name.ToLower()] = "";


      }
    }
    return dict;
  }


  public static string GetValue(this IDictionary<string, string> param, string pkey) {
    if (param == null) { return string.Empty; }
    if (param.TryGetValue(pkey, out var dbValue) && dbValue != null)
      return dbValue.ToString().Trim();

    // 대소문자 무시하고 키 검색
    var key = param.Keys.FirstOrDefault(k => string.Equals(k, pkey, StringComparison.OrdinalIgnoreCase));
    if (key != null && param.TryGetValue(key, out dbValue) && dbValue != null)
      return dbValue.ToString().Trim();

    return string.Empty;
  }


  public static string GetValue(this IDictionary<string, object> param, string pkey) {
    //Console.WriteLine($" dicGetValue : {pkey}");
    if (param == null) { return string.Empty; }
    if (param.TryGetValue(pkey, out var dbValue) && dbValue != null)
      return dbValue.ToString().Trim();

    // 대소문자 무시하고 키 검색
    var key = param.Keys.FirstOrDefault(k => string.Equals(k, pkey, StringComparison.OrdinalIgnoreCase));
    if (key != null && param.TryGetValue(key, out dbValue) && dbValue != null)
      return dbValue.ToString().Trim();

    return string.Empty;
  }



  public static List<T> ConvertDynamicList<T>(this IEnumerable<dynamic> source)
        where T : new() {
    var result = new List<T>();
    var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    foreach (var item in source) {
      var dict = item as IDictionary<string, object>;
      if (dict == null) continue;

      // 딕셔너리 키를 소문자로 변환하여 lookup 생성
      var dictLower = dict.ToDictionary(
          kv => kv.Key.ToLowerInvariant(),
          kv => kv.Value
      );

      var obj = new T();
      foreach (var prop in props) {
        // 속성명 소문자로 변환 후 매칭
        if (dictLower.TryGetValue(prop.Name.ToLowerInvariant(), out var value) && value != null) {
          prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
        }
      }
      result.Add(obj);
    }
    return result;
  }




  // IDictionary<string, string> → List<T> 변환용
  public static List<T> ConvertStringDictionaryList<T>(this IEnumerable<IDictionary<string, string>> source)  where T : new() {
    var result = new List<T>();
    var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    foreach (var dict in source) {
      if (dict == null) continue;

      var dictLower = dict.ToDictionary(
          kv => kv.Key.ToLowerInvariant(),
          kv => kv.Value
      );

      var obj = new T();
      foreach (var prop in props) {
        if (dictLower.TryGetValue(prop.Name.ToLowerInvariant(), out var value) && value != null) {
          prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
        }
      }
      result.Add(obj);
    }
    return result;
  }


  public static List<T> ConvertStringDictionaryList<T>(this IEnumerable<IDictionary<string, object>> source) where T : new() {
    var result = new List<T>();
    var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    foreach (var dict in source) {
      if (dict == null) continue;

      var dictLower = dict.ToDictionary(
          kv => kv.Key.ToLowerInvariant(),
          kv => kv.Value
      );

      var obj = new T();
      foreach (var prop in props) {
        if (dictLower.TryGetValue(prop.Name.ToLowerInvariant(), out var value) && value != null) {
          prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
        }
      }
      result.Add(obj);
    }
    return result;
  }




}


public static class ModelHelper {
  public static Dictionary<string, string> ToCols(Type modelType) {
    Dictionary<string, string> col = new Dictionary<string, string>();

    var properties = modelType.GetProperties();

    foreach (var prop in properties) {
      string name = prop.Name.ToLower();
      string type = prop.PropertyType.FullName;

      if (prop.PropertyType == typeof(DateTime?))
        type = "System.DateTime";

      col[name] = type;
    }

    return col;
  }

  // 제네릭 오버로드도 제공 가능
  public static Dictionary<string, string> ToCols<T>() where T : BaseModel {
    return ToCols(typeof(T));
  }
}


