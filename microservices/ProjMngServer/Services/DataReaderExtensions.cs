using Npgsql;
using System.Dynamic;

namespace ProjMngServer.Services;

public static class DataReaderExtensions {
  public static IEnumerable<IDictionary<string, object>> ToDictionaries(this NpgsqlDataReader reader) {
    while (reader.Read()) {
      yield return reader.ToDictionary();
    }
  }

  public static IDictionary<string, object> ToDictionary(this NpgsqlDataReader reader) {
    var dictionary = new ExpandoObject() as IDictionary<string, object>;
    for (int i = 0; i < reader.FieldCount; i++) {
      dictionary.Add(reader.GetName(i), reader.GetValue(i));
    }
    return dictionary;
  }


  public static IEnumerable<dynamic> ToDynamicEnumerable(this NpgsqlDataReader reader) {
    while (reader.Read()) {
      yield return reader.ToDynamic();
    }
  }

  public static dynamic ToDynamic(this NpgsqlDataReader reader) {
    var expandoObject = new ExpandoObject() as IDictionary<string, object>;
    for (int i = 0; i < reader.FieldCount; i++) {
      expandoObject.Add(reader.GetName(i), reader.GetValue(i));
    }
    return expandoObject;
  }


}
