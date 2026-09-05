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


  /// <summary>
  /// 등록된 DB 접속 정보를 가져온다. 없으면 <c>null</c>.
  ///
  /// <para>
  /// [캐시에 null 을 넣으면 안 된다 — 실제로 밟았다]
  /// </para>
  /// <para>
  /// 예전에는 못 찾았을 때도 결과를 캐시에 넣었다. 그 값이 <c>null</c> 이라,
  /// 다음 호출부터 캐시를 훑는 반복문이 그 자리에서 NullReferenceException
  /// 으로 터졌다. <b>한 번만 잘못 물으면 프로세스를 다시 띄울 때까지 전부
  /// 죽는다.</b>
  /// </para>
  /// <para>
  /// DB 를 고르지 않고 「DB 쿼리 테스터」·「DB 개체 탐색」의 실행을 누르면
  /// 빈 이름으로 물어보게 되고, 그것이 그 상태를 만든다.
  /// </para>
  /// </summary>
  public DbInfo GetDbInfo(string db_nick) {

    // 빈 이름은 물어볼 것이 없다. DB 까지 가지 않는다.
    if (string.IsNullOrWhiteSpace(db_nick)) {
      return null;
    }

    // null 을 건너뛴다. 예전 판이 넣어 둔 것이 남아 있어도 살아남게.
    foreach (var di in AppData.DB_Infos) {
      if (di != null && di.Db_nick == db_nick) {
        return di;
      }
    }

    var connectionString = _configuration.GetConnectionString("jsini");
    using (IDbConnection db = new NpgsqlConnection(connectionString)) {

      var parameters = new DynamicParameters();
      parameters.Add(ConstInfo.db_nick_key, db_nick);

      var found = db.Query<DbInfo>(sql: ConstInfo.dbConQuery, param: parameters).FirstOrDefault();

      // **찾은 것만 넣는다.** 못 찾은 것을 넣으면 위 반복문이 터진다.
      if (found != null) {
        AppData.DB_Infos.Add(found);
      }

      return found;
    }
  }







}

