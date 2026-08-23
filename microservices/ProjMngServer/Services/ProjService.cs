using Dapper;
using Npgsql;
using ProjModel;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Dynamic;

namespace ProjMngServer.Services;

public class ProjService : BaseService {

  public ProjService(IConfiguration configuration) { _configuration = configuration; }

  private LogInfo InParamInit(string tname, RequestDto dto) {

    if(dto.MainParam != null) {
      dto.MainParam["req_type"] = dto.ProcType;
      dto.MainParam["req_ss_user_id"] = dto.SSUserId;
    }

    LogInfo log = new LogInfo() { TicKs = DateTime.Now.Ticks.ToString(), Title = tname };

    log.Message =
      $" {Environment.NewLine} {tname} : {dto.Start} :: {DateTime.Now.ToLongTimeString()} ----------------------------------------------"
    + $" {Environment.NewLine} ProcName : {dto.ProcName}"
    + $" {Environment.NewLine} ProcType : {dto.ProcType}"
    + $" {Environment.NewLine} SSUserId : {dto.SSUserId}"
    //+ $" {Environment.NewLine} Start : {dto.Start}"
    ;
    if (dto.IsFast) { log.Message += $" {Environment.NewLine} Fast"; }
    if (dto.IsProjDb) { log.Message += $" {Environment.NewLine} ProjDb : { dto.MainParam.GetValue("db_nick") }"; }

    Console.WriteLine(log.Message);

    return log;
  }

  public ResultInfo<dynamic> GetData(RequestDto dto) {

    var log = InParamInit("ProjService [GetData] ", dto);

    //string procedureName = dto.ProcName;
    //IDictionary<string, string> param = dto.MainParam;
    //param["req_type"] = dto.ProcType;
    //param["req_ss_user_id"] = dto.SSUserId;
    return GetData(dto.ProcName, dto.MainParam);
  }


  public ResultInfo<dynamic> GetData(string procedureName, IDictionary<string, string> param) {

    ResultInfo<dynamic> ri = new ResultInfo<dynamic>();

    DateTime sdt = DateTime.Now;
    DateTime spdt = DateTime.Now;
    DateTime epdt = DateTime.Now;

    IEnumerable<dynamic> aaa = Enumerable.Empty<dynamic>();
    IDictionary<string, string> bbb = null;

    var connectionString = _configuration.GetConnectionString("jsini");
    if (string.IsNullOrWhiteSpace(procedureName) || string.IsNullOrWhiteSpace(connectionString)) {

      ri.Code = -88;
      ri.Message = "연결 정보에 문제가 있습니다.";
    }
    else {

      try {

        var consDic = connectionString
       .Split(';', StringSplitOptions.RemoveEmptyEntries)
       .Select(part => part.Split('=', 2))
       .Where(part => part.Length == 2)
       .ToDictionary(sp => sp[0].Trim(), sp => sp[1].Trim());

        string schema_name = consDic.TryGetValue("SearchPath", out var schemaValue) && schemaValue != null ? schemaValue.ToString() : string.Empty;

        IEnumerable<dynamic> procParams;
        var parameters = new DynamicParameters();
        using (IDbConnection db = new NpgsqlConnection(connectionString)) {

          //string getProcParamsQuery = $@"
          //      SELECT
          //          p.parameter_name,
          //          p.data_type,
          //          p.specific_name,
          //          p.parameter_mode
          //      FROM
          //          information_schema.parameters p
          //      WHERE 1=1
          //          -- p.specific_schema = '{schema_name}' 
          //          and p.specific_name ~ ('^{procedureName.ToLower()}(_[0-9]+)?$')
          //      ORDER BY
          //          p.ordinal_position;
          //  ";

          procParams = ProcParams(db, schema_name, procedureName); // db.Query(getProcParamsQuery);

          if (procParams.ToList().Count <= 0) {
            ri.Code = -1;
            ri.Message = $"{procedureName} 정보를 가져오지 못했습니다.";
          }
          spdt = DateTime.Now;

          if (ri.Code >= 0) {

            //Console.WriteLine($" param list : {DateTime.Now.ToShortTimeString()} ");
            //Console.WriteLine($" procedureName : {procedureName} ");
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

                    if (paramName == "ss_user_id") {
                      parameters.Add(paramName, param.GetValue("req_ss_user_id"), DbType.String);
                      //Console.WriteLine($" {paramName} : {param.GetValue("req_ss_user_id")} ");
                    }
                    else {
                      object paramValue = param.TryGetValue(paramKey, out var value) && value != null ? value.ToString() : null;
                      parameters.Add(paramName, paramValue, DbType.String);
                      //Console.WriteLine($" {paramName} : {paramValue} ");
                    }

                  }
                }
              }

              db.Execute(sql: schema_name + "." + procedureName, param: parameters, commandType: CommandType.StoredProcedure);

              // out cursor 처리
              if (!string.IsNullOrEmpty(outCursorParamName)) {

                var cursor = parameters.Get<string>(outCursorParamName);

                if (cursor != null) {

                  using (var cmd = new NpgsqlCommand($"FETCH ALL IN \"{cursor}\"", db as NpgsqlConnection)) // db를 NpgsqlConnection으로 캐스팅
                  using (var rdr = cmd.ExecuteReader(  )) {

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





  public ResultInfo<dynamic> ExcuteMultyData(RequestDto dto) {


   var log = InParamInit("ProjService [ExcuteMultyData] ", dto);


    //string procedureName = dto.ProcName;
    //IDictionary<string, string> param = dto.MainParam;
    //List<Dictionary<string, object>> rowdata = dto.MultyData;

    //param["req_type"] = dto.ProcType;
    return ExcuteMultyData(dto.ProcName, dto.MainParam, dto.MultyData);
  }


  public ResultInfo<dynamic> ExcuteMultyData(string procedureName, IDictionary<string, string> param, List<Dictionary<string, object>> rowdata) {

    var rowdata2 = ConvertToListOfStringDictionaries(rowdata);

    return ExcuteMultyData( procedureName,  param,  rowdata2);
  }


  public List<Dictionary<string, string>> ConvertToListOfStringDictionaries(List<Dictionary<string, object>> source) {
    var result = new List<Dictionary<string, string>>(source.Count);
    foreach (var dict in source) {
      var newDict = new Dictionary<string, string>(dict.Count);
      foreach (var kv in dict) {
        if (kv.Value == null) {
          newDict[kv.Key] = null;
        }
        else if (kv.Value is DateTime dt) {
          newDict[kv.Key] = dt.ToString("yyyyMMdd"); // 필요에 따라 포맷 변경
        }
        else {
          newDict[kv.Key] = kv.Value.ToString();
        }
      }
      result.Add(newDict);
    }
    return result;
  }



  //public ResultInfo<dynamic> ExcuteMultyData(string procedureName, IDictionary<string, string> param, List<Dictionary<string, object>> rowdata) {
  public ResultInfo<dynamic> ExcuteMultyData(string procedureName, IDictionary<string, string> param, List<Dictionary<string, string>> rowdata) {

    ResultInfo<dynamic> ri = new ResultInfo<dynamic>();

    DateTime sdt = DateTime.Now;
    DateTime spdt = DateTime.Now;
    DateTime epdt = DateTime.Now;

    IEnumerable<dynamic> aaa = Enumerable.Empty<dynamic>();
    IDictionary<string, string> bbb = null;

    var connectionString = _configuration.GetConnectionString("jsini");
    if (string.IsNullOrWhiteSpace(procedureName) || string.IsNullOrWhiteSpace(connectionString)) {

      ri.Code = -88;
      ri.Message = "연결 정보에 문제가 있습니다.";
    }
    else {

      try {

        var consDic = connectionString
       .Split(';', StringSplitOptions.RemoveEmptyEntries)
       .Select(part => part.Split('=', 2))
       .Where(part => part.Length == 2)
       .ToDictionary(sp => sp[0].Trim(), sp => sp[1].Trim());

        string schema_name = consDic.TryGetValue("SearchPath", out var schemaValue) && schemaValue != null ? schemaValue.ToString() : string.Empty;

        IEnumerable<dynamic> procParams;
        //var parameters = new DynamicParameters();
        using (IDbConnection db = new NpgsqlConnection(connectionString)) {

          //string getProcParamsQuery = $@"
          //      SELECT
          //          p.parameter_name,
          //          p.data_type,
          //          p.specific_name,
          //          p.parameter_mode
          //      FROM
          //          information_schema.parameters p
          //      WHERE 1=1
          //          -- p.specific_schema = '{schema_name}' 
          //          and p.specific_name ~ ('^{procedureName}(_[0-9]+)?$')
          //      ORDER BY
          //          p.ordinal_position;
          //  ";

          //procParams = db.Query(getProcParamsQuery);
          procParams = ProcParams(db, schema_name, procedureName);

          if (procParams.ToList().Count <= 0) {
            ri.Code = -1;
            ri.Message = $"{procedureName} 정보를 가져오지 못했습니다.";
          }
          spdt = DateTime.Now;

          if (ri.Code >= 0) {

            //Console.WriteLine($"-------------------------------------------------");
            //Console.WriteLine($" param list : {DateTime.Now.ToShortTimeString()} -------------------------------------------");
            //Console.WriteLine($" procedureName : {procedureName} ");
            string outCursorParamName = null;

            db.Open();
            using (var tran = db.BeginTransaction()) {


              foreach (Dictionary<string, string> itm in rowdata) {
                //Dictionary<string, object> itm = null;
                //if ( obj.GetType() is BaseModel) {
                //  itm = obj.ToDictionary();
                //}
                //else {
                //  itm = obj as Dictionary<string, object>;
                //}

                //  Dictionary<string, object> itm = obj as Dictionary<string, object>;

                var parameters = new DynamicParameters();
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
                      //object paramValue = param.TryGetValue(paramKey, out var value) && value != null ? value.ToString() : null;
                      //if (paramValue == null) {
                      //  paramValue = itm.TryGetValue(paramKey, out var itm_value) && itm_value != null ? itm_value.ToString() : null;
                      //}

                      //var paramValue = param.GetValue(paramKey);
                      object paramValue = param.TryGetValue(paramKey, out var value) && value != null ? value.ToString() : null;
                      if (paramValue == null) {
                        paramValue = itm.GetValue(paramKey);// .TryGetValue(paramKey, out var itm_value) && itm_value != null ? itm_value.ToString() : null;
                      }

                      if (paramName == "ss_user_id") {
                        parameters.Add(paramName, param.GetValue("req_ss_user_id"), DbType.String);
                        //Console.WriteLine($" {paramName} : {param.GetValue("req_ss_user_id")} ");
                      }
                      else {
                        parameters.Add(paramName, paramValue, DbType.String);
                        //Console.WriteLine($" {paramName} : {paramValue} ");

                      }


                    }
                  }
                }
                //포로시저 실행
                db.Execute(sql: schema_name + "." + procedureName, param: parameters, commandType: CommandType.StoredProcedure);


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

  /*

  public ResultInfo<Dictionary<string, string>> GetMdData(string action_name, Dictionary<string, string> param) {

    param["req_type"] = "srch";

    ResultInfo<dynamic> srcInfo = GetData("sp_dev_srcinfo_exec", param);

    List<Dictionary<string,object>> srcInfoData = ConvertToListOfDictionaries(srcInfo.Data.AsEnumerable());


      ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();
    if (srcInfoData.Count > 0) {
      string basePath = srcInfoData[0]["src_path"].ToString();
      string projNamespace = srcInfoData[0]["prj_namespace"].ToString();  // @"ProjMngWasm";
      string pageRoot = srcInfoData[0]["src_ui_root"].ToString();         // @"Pages";
      string pagePattern = srcInfoData[0]["url_pattern"].ToString();      // "@page\\s+\"(?<url>[^\"]+)\"";

      List<Dictionary<string, string>> aaa = null;

      aaa = BlazorUtil.GetBlazorMenuList(basePath, projNamespace, pageRoot, pagePattern);

      if (aaa == null || aaa.Count <= 0) {
        // subdir 찾아서 가져오기
        string src_rid = srcInfoData[0]["src_rid"].ToString();
        param.Add("src_rid", src_rid);

        ResultInfo<dynamic> srcInfo_dtl = GetData("sp_dev_srcinfo_dtl_exec", param);

        List<Dictionary<string, object>> srcInfoDtlData = ConvertToListOfDictionaries(srcInfo_dtl.Data.AsEnumerable());

        List<Dictionary<string, object>> srcPathList = srcInfoDtlData.Where(dict => dict.ContainsKey("src_pattern_grp") && dict["src_pattern_grp"]?.ToString() == "src_path").ToList();

        if (srcPathList.Count > 0) {

          basePath = srcPathList[0]["url_pattern"].ToString();

          aaa = BlazorUtil.GetBlazorMenuList(basePath, projNamespace, pageRoot, pagePattern);
        }
      }

      Dictionary<string, string> col = new Dictionary<string, string>();
      foreach (var ad in aaa) {
        foreach (var a in ad) {
          col.Add(a.Key, "System.String");
        }
        break;
      }
      ri.Cols = col;
      ri.Data = aaa;
    }

    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
    return ri;
  }
  */

  public ResultInfo<Dictionary<string, string>> GetMdBlazorData(RequestDto dto) {

    IDictionary<string, string> param = dto.MainParam;
    //param["req_type"] = "srch";

    ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();
    GetBlazorFile(ri, param);
    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
    return ri;

  }

  IDictionary<string,object> GetUrlPattern(IDictionary<string, string> param, string src_extend) {

    var srcInfo = GetData("sp_dev_srcinfo_dtl_exec", param);
   IDictionary<string,object> ccc = srcInfo.Data
  .OfType<IDictionary<string, object>>()
  .FirstOrDefault(d => d.ContainsKey("src_extend") && d["src_extend"]?.ToString() == src_extend);

    // string path = ccc?["url_pattern"]?.ToString();// @"c:\projects\ProjMng\samples\";

    // return path;
    return ccc;
  }

  public ResultInfo<Dictionary<string, string>> GetMdGlueData(RequestDto dto) {

    IDictionary<string, string> param = dto.MainParam;
    param["req_type"] = "srch";

    ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();
    string src_rid = param["src_rid"]?.ToString();

    List<Dictionary<string, string>> aaa = new();
    Dictionary<string, string> col = new Dictionary<string, string>() {
        { "ServiceName", "System.String"},
        { "TransitionName", "System.String"},
        { "TransitionValue", "System.String"},
        { "Dao", "System.String"},
        { "ProcedureName", "System.String"},
        { "ResultKey", "System.String"},
        { "Activity", "System.String"}
      };

    ri.Cols = col;

    var ccc = GetUrlPattern(param, "glue");//  ccc["url_pattern"]?.ToString();// @"c:\projects\ProjMng\samples\"; 
    string path = ccc.GetValue("url_pattern");// string.Empty;
//    string path = GetUrlPattern(param, "glue");//  ccc["url_pattern"]?.ToString();// @"c:\projects\ProjMng\samples\";



    // string path = ccc?["url_pattern"]?.ToString();// @"c:\projects\ProjMng\samples\";

    // return path;




    if (!string.IsNullOrEmpty(path)) {

      List<Dictionary<string, string>> rowdata = new();

      var activeList = ActivityParser.ParseActivityFiles(path);

      foreach (var item in activeList) {

        Dictionary<string, string> sItem = item.ToDictionary();
        sItem["req_type"] = "save";
        sItem["src_rid"] = src_rid;

        rowdata.Add(item.ToDictionary());

      }

      ExcuteMultyData( "sp_dev_activityinfo_exec"
        , new Dictionary<string, string> { { "req_type", "save" }, { "src_rid", src_rid } }
        , rowdata
      );

    }
    ri.Data = aaa;

    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
    return ri;

  }




  public ResultInfo<Dictionary<string, string>> GetMdSourData(RequestDto dto) {

    IDictionary<string, string> param = dto.MainParam;
    param["req_type"] = "srch";

    ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();
    string src_rid = param["src_rid"]?.ToString();

    //List<Dictionary<string, string>> aaa = new();
    //Dictionary<string, string> col = new Dictionary<string, string>() {
    //    { "ServiceName", "System.String"},
    //    { "TransitionName", "System.String"},
    //    { "TransitionValue", "System.String"},
    //    { "Dao", "System.String"},
    //    { "ProcedureName", "System.String"},
    //    { "ResultKey", "System.String"},
    //    { "Activity", "System.String"}
    //  };


    var col = ModelHelper.ToCols<SrcFileInfo>();


    ri.Cols = col;

    string extend = param.GetValue("src_lang");

    var ccc = GetUrlPattern(param, extend);//  ccc["url_pattern"]?.ToString();// @"c:\projects\ProjMng\samples\"; 
    string path = ccc.GetValue("url_pattern");// string.Empty;
    string skipStr = ccc.GetValue("src_pattern_comment");// string.Empty;

    //string path = GetUrlPattern(param, extend); // jsp, blazor 등의 url_patten 을 가져온다.
    List<Dictionary<string, string>> rowdata = new();
    if (!string.IsNullOrEmpty(path)) {


      var activeList = ActivityParser.ParseSrcFiles(path, extend, skipStr);

      foreach (var item in activeList) {

        //Dictionary<string, string> sItem = item.ToDictionary();
        //sItem["req_type"] = "save";
        //sItem["src_rid"] = src_rid;

        rowdata.Add(item.ToDictionary());

      }

      //ExcuteMultyData("sp_dev_activityinfo_exec"
      //  , new Dictionary<string, string> { { "req_type", "save" }, { "src_rid", src_rid } }
      //  , rowdata
      //);

    }
    ri.Data = rowdata;

    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
    return ri;

  }




  

  public ResultInfo<Dictionary<string, string>> GetMdContent(RequestDto dto) {

    IDictionary<string, string> param = dto.MainParam;

    ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();


    string fullpath = param.GetValue("fullpath");

    string context = File.ReadAllText(fullpath);

    ri.Cols = new Dictionary<string, string>() { { "context", "System.String" } };


    List<Dictionary<string, string>> rowdata = new List<Dictionary<string, string>>();
    rowdata.Add( new Dictionary<string, string>() { { "context", context } } );


    ri.Data = rowdata;

    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
    return ri;

  }



  public void GetBlazorFile(ResultInfo<Dictionary<string, string>> ri, IDictionary<string, string> param) { 


    ResultInfo<dynamic> si = GetData("sp_dev_srcinfo_exec" , new Dictionary<string, string>{ { "req_type", "srch" }
        , { "src_rid", param.GetValue("src_rid")  } 
      });

    var srcInfo = si.Data.ConvertDynamicList<SrcInfo>().FirstOrDefault(); 

    ResultInfo<dynamic> si_dtl = GetData("sp_dev_srcinfo_dtl_exec" , new Dictionary<string, string>{ { "req_type", "srch" }
        , { "src_rid", param.GetValue("src_rid")  } 
      }); 

    srcInfo.SiDtlList = si_dtl.Data.ConvertDynamicList<SrcInfoDtl>(); 

    Dictionary<string, string> col = new Dictionary<string, string>();
    List<Dictionary<string, string>> aaa = BlazorUtil.GetBlazorMenuList(srcInfo); 

    foreach (var ad in aaa) {
      foreach (var a in ad) {
        col.Add(a.Key, "System.String");
      }
      break;
    }
    ri.Cols = col;
    ri.Data = aaa;

  }
















}
