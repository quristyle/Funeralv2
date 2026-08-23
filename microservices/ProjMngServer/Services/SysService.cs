using Dapper;
using Npgsql;
using ProjModel;
using System.Data;
using System.Dynamic;

namespace ProjMngServer.Services;

public class SysService : BaseService {

  public SysService(IConfiguration configuration) { _configuration = configuration; }

  public ResultInfo<dynamic> GetData(string procedureName, Dictionary<string, string> param) {

    ResultInfo<dynamic> ri = new ResultInfo<dynamic>();

    DateTime sdt = DateTime.Now;
    DateTime spdt = DateTime.Now;
    DateTime epdt = DateTime.Now;

    IEnumerable<dynamic> aaa = Enumerable.Empty<dynamic>();
    IDictionary<string, string> bbb = null;

    if (string.IsNullOrWhiteSpace(procedureName)) { }
    else {
      var connectionString = _configuration.GetConnectionString("jsini");

      var consDic = connectionString
     .Split(';', StringSplitOptions.RemoveEmptyEntries)
     .Select(part => part.Split('=', 2))
     .Where(part => part.Length == 2)
     .ToDictionary(sp => sp[0].Trim(), sp => sp[1].Trim());

      string schema_name = consDic.TryGetValue("SearchPath", out var schemaValue) && schemaValue != null ? schemaValue.ToString() : string.Empty;

      IEnumerable<dynamic> procParams;
      var parameters = new DynamicParameters();

      try {
        using (IDbConnection db = new NpgsqlConnection(connectionString)) {

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
                    and p.specific_name ~ ('^{procedureName}(_[0-9]+)?$')
                ORDER BY
                    p.ordinal_position;
            ";

          procParams = db.Query(getProcParamsQuery);

          if (procParams.ToList().Count <= 0) {
            ri.Code = -1;
            ri.Message = $"{procedureName} 정보를 가져오지 못했습니다.";
          }
          spdt = DateTime.Now;

          if (ri.Code >= 0) {

            string outCursorParamName = null;

            db.Open();
            using (var tran = db.BeginTransaction()) {

              // 프로시저 파라미터 구성
              if (procParams.Any()) { // 프로시저의 파라미터가 존재하는 경우만 처리.
                foreach (var p in procParams) {
                  string paramName = p.parameter_name;
                  string paramKey = paramName.StartsWith("p_") ? paramName.Substring(2, paramName.Length - 2) : paramName;

                  // check parameter mode
                  string parameterMode = p.parameter_mode.ToString().ToUpper();
                  if (parameterMode == "INOUT" && p.data_type.ToString() == "refcursor") {
                    outCursorParamName = paramName;

                    parameters.Add(paramName, dbType: DbType.Object, direction: ParameterDirection.Output); // Output refcursor
                  }
                  else {
                    object paramValue = param.TryGetValue(paramKey, out var value) && value != null ? value.ToString() : null;
                    parameters.Add(paramName, paramValue, DbType.String);

                  }
                }
              }

              db.Execute(sql: schema_name + "." + procedureName, param: parameters, commandType: CommandType.StoredProcedure);

              // out cursor 처리
              if (!string.IsNullOrEmpty(outCursorParamName)) {

                var cursor = parameters.Get<string>(outCursorParamName);

                if (cursor != null) {

                  using (var cmd = new NpgsqlCommand($"FETCH ALL IN \"{cursor}\"", db as NpgsqlConnection)) // db를 NpgsqlConnection으로 캐스팅
                  using (var rdr = cmd.ExecuteReader()) {

                    // var expandoObject2 = new ExpandoObject() as IDictionary<string, object>;
                    var resultList = new List<dynamic>();
                    var resultList2 = new List<dynamic>();


                    if (rdr.HasRows) {
                      while (rdr.Read()) {

                        var expandoObject = new ExpandoObject() as IDictionary<string, object>;
                        string nm = "";
                        object oval = null;
                        string empty = null;
                        for (int i = 0; i < rdr.FieldCount; i++) {
                          nm = rdr.GetName(i);
                          oval = rdr.GetValue(i);

                          // oval 값이 Dbnull 인 경우 json 으로 {} 넘어 간다... 이를 클라이언트에서 처리시 잘못하면 parse error 가 난다.
                          if (oval.GetType() == typeof(System.DBNull)) {
                            expandoObject.Add(nm, empty);
                          }
                          else {
                            expandoObject.Add(nm, oval);
                          }
                        }
                        resultList.Add(expandoObject);
                      }

                    }

                    //var schemaTable = rdr.GetSchemaTable();

                    ri.Data = resultList;
                   ri.Cols = GetColumns(rdr);

                  }

                }
              }
              else { // out cursor 가 없는 경우, 그냥 쿼리 실행 해서 결과가 있으면 넣어준다.
                if (!procParams.Any()) { //프로시저의 파라미터가 없는 경우에만
                  aaa = db.Query<dynamic>(sql: schema_name + "." + procedureName, param: parameters, commandType: CommandType.StoredProcedure);
                  ri.Data = aaa.ToList();
                
                }
              }

              tran.Commit();

              epdt = DateTime.Now;



            }



          }



        }


      }
      catch (Exception ee) {
        ri.Code = -99;
        ri.Message = ee.Message;
      }
      finally {
      }
    }

     GetRes(ref ri, param, sdt, spdt, epdt);


    return ri;
  }


  /// <summary> AppData clear </summary>
  public ResultInfo<dynamic> AppDataClear(string action_name, IDictionary<string, string> param) {

    ResultInfo<dynamic> ri = new ResultInfo<dynamic>();

    AppData.DB_Infos.Clear();
    AppData.DsrInfos.Clear();


    return ri;
  }

}
