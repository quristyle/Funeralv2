using Dapper;
using Npgsql;
using ProjModel;
using System.Data;
using System.Dynamic;
using System.Runtime.Intrinsics.Arm;

namespace ProjMngServer.Services;
public class BaseService {

  protected IConfiguration _configuration;


  /// <summary>
  /// 포로시저의 파라미터정보를 리턴
  /// </summary>
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
  protected void GetRes<T>(ref ResultInfo<T> ri, IDictionary<string, string> param
    , DateTime sdt, DateTime spdt, DateTime epdt
    ) {

    var rcnt = 0;

    ri.Res = new Dictionary<string, object>(){
          { "p", param },
          { "sdt", sdt.ToString("yyyy.MM.dd HH:mm:ss") },
          { "edt", DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") },
          { "dtgap", (DateTime.Now-sdt).TotalSeconds },
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
        string columnName = row["ColumnName"].ToString();
        string dataType = row["DataType"].ToString();
        expandoObject.Add(columnName, dataType);
      }
    }
    return expandoObject;
  }


  // dbinfo 정보 을 가져온다.
  public DbInfo GetDbInfo(string db_nick) {

    DbInfo result = null;// AppData.DB_Infos.TryGetValue(db_nick, out var dbValue) ? dbValue :null;

    foreach( var di in AppData.DB_Infos) {
      if( di.Db_nick == db_nick) {
        result = di; break;
      }
    }

    if (result == null) { // 없으면 가져온다.

      var connectionString = _configuration.GetConnectionString("jsini");
      using (IDbConnection db = new NpgsqlConnection(connectionString)) {

        var parameters = new DynamicParameters();
        parameters.Add(ConstInfo.db_nick_key, db_nick);
        result = db.Query<DbInfo>(sql: ConstInfo.dbConQuery, param: parameters).ToList().FirstOrDefault();

        AppData.DB_Infos.Add(result);
      }
    }

    return result;
  }







}

