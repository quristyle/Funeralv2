using Dapper;
using Npgsql;
using ProjModel;
using System.Data;
using System.Dynamic;
using System.Runtime.Intrinsics.Arm;

namespace ProjMngServer.Services;
public class BaseService {

  protected readonly IConfiguration _configuration;

  /// <summary>파생 서비스가 반드시 설정을 넘겨주도록 생성자에서 받는다.</summary>
  /// <param name="configuration">연결 문자열 등을 읽는다</param>
  protected BaseService(IConfiguration configuration) {
    _configuration = configuration;
  }


  /// <summary>포로시저의 파라미터정보를 리턴</summary>
  /// <param name="db"></param>
  /// <param name="schema_name"></param>
  /// <param name="procedureName"></param>
  /// <returns></returns>
  protected IEnumerable<dynamic> ProcParams(IDbConnection db, string schema_name, string procedureName) {
    string getProcParamsQuery = $@"
                SELECT
                    p.parameter_name,
                    p.data_type,
                    p.specific_name,
                    p.parameter_mode
                FROM
                    information_schema.parameters p
                WHERE 1=1
                    -- p.specific_schema = '{schema_name}' 
                    and p.specific_name ~ ('^{procedureName.ToLower()}(_[0-9]+)?$')
                ORDER BY
                    p.ordinal_position;
            ";

    return db.Query(getProcParamsQuery);
  }


  /// <summary> 되돌려줄 response dictionary </summary>
  // 매개변수 없이 부르는 자리가 있다 (직접 쿼리 실행).
  protected void GetRes<T>(ref ResultInfo<T> ri, IDictionary<string, string>? param
    , DateTime sdt, DateTime spdt, DateTime epdt
    ) {

    var rcnt = 0;

    ri.Res = new Dictionary<string, object?>(){
          { "p", param },
          { "sdt", sdt.ToString("yyyy.MM.dd HH:mm:ss") },
          { "edt", DateTime.UtcNow.ToString("yyyy.MM.dd HH:mm:ss") },
          { "dtgap", (DateTime.UtcNow-sdt).TotalSeconds },
          { "spdt", spdt.ToString("yyyy.MM.dd HH:mm:ss") },
          { "epdt", epdt.ToString("yyyy.MM.dd HH:mm:ss") },
          { "tot_sgap", (epdt-sdt).TotalSeconds },
          { "tot_mgap", (epdt-sdt).Milliseconds },
          { "cnt", rcnt },
        };

  }

  public static List<Dictionary<string, object>> ConvertToListOfDictionaries(IEnumerable<dynamic> data) {
  
      var result = new List<Dictionary<string, object>>();
    if (data == null) return result;

    foreach (var item in data) {
      var dict = new Dictionary<string, object>();
      if (item is IDictionary<string, string> stringDict) {
        foreach (var kvp in stringDict) {
          dict[kvp.Key] = kvp.Value;
        }
      }
      else if (item is IDictionary<string, object> objectDict) {
        foreach (var kvp in objectDict) {
          dict[kvp.Key] = kvp.Value;
        }
      }
      result.Add(dict);
    }
    return result;
  }







  protected Dictionary<string, string> GetColumns(IDataReader idr) {

    var expandoObject = new Dictionary<string, string>();
    var schemaTable = idr.GetSchemaTable();
    if (schemaTable != null) {

      foreach (DataRow row in schemaTable.Rows) {
        // 스키마 표의 칸이 비어 있는 드라이버가 있다. 빈 이름은 담지 않는다.
        var columnName = row["ColumnName"]?.ToString();
        if (string.IsNullOrEmpty(columnName)) continue;
        expandoObject.Add(columnName, row["DataType"]?.ToString() ?? string.Empty);
      }
    }
    return expandoObject;
  }


  /// <summary>등록된 DB 접속 정보를 가져온다. 없으면 <c>null</c>. 캐시하지 않는다 — 정적 목록에 담으면 DB 등록 화면에서 바꾼 주소가 재시작 전까지 옛 값으로 남는다.</summary>
  public DbInfo? GetDbInfo(string db_nick) {

    // 빈 이름은 물어볼 것이 없다. DB 까지 가지 않는다.
    if (string.IsNullOrWhiteSpace(db_nick)) {
      return null;
    }

    var connectionString = _configuration.GetConnectionString("jsini");
    using (IDbConnection db = new NpgsqlConnection(connectionString)) {

      var parameters = new DynamicParameters();
      parameters.Add(ConstInfo.db_nick_key, db_nick);

      return db.Query<DbInfo>(sql: ConstInfo.dbConQuery, param: parameters).FirstOrDefault();
    }
  }







}

