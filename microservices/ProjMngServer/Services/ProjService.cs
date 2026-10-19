using Dapper;
using Npgsql;
using ProjModel;
using ProjMngServer.Models;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Dynamic;

namespace ProjMngServer.Services;

public class ProjService : BaseService {

  /// <summary>소스 정보를 읽는 곳. 프로시저를 부르는 범용 통로를 쓰되, <c>md_</c> 갈래(파일 훑기)는 안에서 <c>sp_dev_srcinfo_exec</c>·<c>sp_dev_srcinfo_dtl_exec</c> 를 부른다.</summary>
  private readonly SourceInfoService _sources;

  /// <summary>Glue 자료를 쌓는 곳. <c>md_glue_service</c> 가 훑은 결과를 넣는다.</summary>
  private readonly ActivityInfoService _activities;

  public ProjService(
      IConfiguration configuration, SourceInfoService sources, ActivityInfoService activities)
      : base(configuration) {
    _sources = sources;
    _activities = activities;
  }

  /// <summary>소스 상세를 <c>src_rid</c> 로 읽어 옛 프로시저가 주던 사전 모양으로 맞춘다. 부르는 쪽이 칸 이름을 글자로 집으므로 이름을 바꾸지 않는다.</summary>
  private List<IDictionary<string, object>> SourceDetailRows(IDictionary<string, string> param) {

    if (!int.TryParse(param.GetValue("src_rid"), out var srcRid)) { return []; }

    return [.. _sources.DetailsAsync(srcRid).GetAwaiter().GetResult()
      .Select(d => (IDictionary<string, object>)new Dictionary<string, object> {
        ["src_dtl_rid"] = d.SrcDtlRid,
        ["src_rid"] = d.SrcRid ?? 0,
        ["src_extend"] = d.SrcExtend ?? string.Empty,
        ["src_pattern_grp"] = d.SrcPatternGrp ?? string.Empty,
        ["url_pattern"] = d.UrlPattern ?? string.Empty,
        ["src_pattern_comment"] = d.SrcPatternComment ?? string.Empty,
        ["src_pattern_nullvalue"] = d.SrcPatternNullvalue ?? string.Empty,
      })];
  }

  private LogInfo InParamInit(string tname, RequestDto dto) {

    if(dto.MainParam != null) {
      dto.MainParam["req_type"] = dto.ProcType;
      dto.MainParam["req_ss_user_id"] = dto.SSUserId;
    }

    LogInfo log = new LogInfo() { TicKs = DateTime.UtcNow.Ticks.ToString(), Title = tname };

    log.Message =
      $" {Environment.NewLine} {tname} : {dto.Start} :: {DateTime.UtcNow.ToLongTimeString()} ----------------------------------------------"
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



  public ResultInfo<Dictionary<string, string>> GetMdBlazorData(RequestDto dto) {

    IDictionary<string, string> param = dto.MainParam;
    //param["req_type"] = "srch";

    ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();
    GetBlazorFile(ri, param);
    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
    return ri;

  }

  /// <summary>소스 상세에서 그 확장자에 딸린 행을 찾는다. 경로가 적힌 행을 먼저 고른다. <c>url_pattern</c> 의 뜻은 <c>src_pattern_grp</c> 에 따라 다르다.</summary>
  IDictionary<string,object>? GetUrlPattern(IDictionary<string, string> param, string src_extend) {

    var all = SourceDetailRows(param);

    var rows = all
      .Where(d => d.ContainsKey("src_extend") && d["src_extend"]?.ToString() == src_extend)
      .ToList();

    var byExtend = rows.FirstOrDefault(d => d.GetValue("src_pattern_grp") == "src_path");
    if (byExtend != null) { return byExtend; }

    // 확장자가 없는 경로 행을 쓴다 — 소스 하나에 뿌리 경로는 보통 하나다.
    var anyPath = all.FirstOrDefault(d => d.GetValue("src_pattern_grp") == "src_path");

    return anyPath ?? rows.FirstOrDefault();
  }

  /// <summary>훑을 뿌리 경로가 쓸 만한지 본다. 아니면 이유를 <paramref name="reason"/> 에 담는다. 서버가 훑는 것은 서버 장비의 디스크라 등록된 경로가 그 장비에 없는 것은 흔하고, 그것이 500 일 이유는 없다.</summary>
  static bool CanScan(string path, out string reason) {

    if (string.IsNullOrWhiteSpace(path)) {
      reason = "소스 상세에 훑을 경로(src_pattern_grp='src_path')가 등록되어 있지 않습니다.";
      return false;
    }

    if (!Directory.Exists(path)) {
      reason = $"등록된 경로가 서버에 없습니다: {path}";
      return false;
    }

    reason = string.Empty;
    return true;
  }

  public ResultInfo<Dictionary<string, string>> GetMdGlueData(RequestDto dto) {

    IDictionary<string, string> param = dto.MainParam;
    param["req_type"] = "srch";

    ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();
    string? src_rid = param["src_rid"]?.ToString();

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




    if (!CanScan(path, out string reason)) {

      // 훑을 수 없으면 DB 를 건드리지 않는다 — 화면이 왜 아무것도 안 늘었는지 알 수 있어야 한다.
      ri.Code = -88;
      ri.Message = reason;
      ri.Data = aaa;

      GetRes<Dictionary<string, string>>(ref ri, param, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
      return ri;
    }

    {
      var activeList = ActivityParser.ParseActivityFiles(path);

      // 훑은 것으로 그 소스의 자료를 통째로 갈아 끼운다 — 줄마다 덮어쓰기만 하면 파일에서 없어진 서비스가 DB 에 남는다.
      _activities.ReplaceAsync(
        src_rid ?? string.Empty,
        [.. activeList.Select(a => new ActivityInfoRow {
          ServiceName = a.ServiceName,
          TransitionName = a.TransitionName,
          TransitionValue = a.TransitionValue,
          Dao = a.Dao,
          ProcedureName = a.ProcedureName,
          ResultKey = a.ResultKey,
          Activity = a.Activity,
          ActivityType = a.Activity_Type,
          ActiveContext = a.Active_context,
          SrcRid = src_rid ?? string.Empty,
        })]
      ).GetAwaiter().GetResult();

    }
    ri.Data = aaa;

    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
    return ri;

  }




  public ResultInfo<Dictionary<string, string>> GetMdSourData(RequestDto dto) {

    IDictionary<string, string> param = dto.MainParam;
    param["req_type"] = "srch";

    ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();
    string? src_rid = param["src_rid"]?.ToString();

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

    if (!CanScan(path, out string reason)) {

      ri.Message = reason;
      ri.Code = -88;
      ri.Data = rowdata;

      GetRes<Dictionary<string, string>>(ref ri, param, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
      return ri;
    }

    {
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

    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
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

    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
    return ri;

  }



  public void GetBlazorFile(ResultInfo<Dictionary<string, string>> ri, IDictionary<string, string> param) { 


    // 소스를 골라 왔으면 그것을, 아니면 그 프로젝트의 첫 소스를 훑는다 — 빈 src_rid 를 넘기면 다른 프로젝트의 소스를 훑을 수 있다.
    SourceInfo? found;

    if (int.TryParse(param.GetValue("src_rid"), out var srcRid)) {
      found = _sources.ListAsync(srcRid: srcRid).GetAwaiter().GetResult().FirstOrDefault();
    }
    else if (int.TryParse(param.GetValue("prj_rid"), out var prjRid)) {
      found = _sources.ListAsync(prjRid: prjRid).GetAwaiter().GetResult().FirstOrDefault();
    }
    else {
      ri.Code = -88;
      ri.Message = "프로젝트나 소스를 고르십시오.";
      return;
    }

    // 등록된 소스가 없으면 훑을 것이 없다.
    if (found == null) {
      ri.Code = -88;
      ri.Message = "소스 정보를 찾지 못했습니다.";
      return;
    }

    var srcInfo = new SrcInfo {
      Src_rid = found.SrcRid.ToString(),
      Src_os = found.SrcOs ?? string.Empty,
      Src_path = found.SrcPath ?? string.Empty,
      Src_nick = found.SrcNick ?? string.Empty,
      Src_type = found.SrcType ?? string.Empty,
      Src_lang = found.SrcLang ?? string.Empty,
      Src_comm = found.SrcComm ?? string.Empty,
      Prj_rid = found.PrjRid?.ToString() ?? string.Empty,
      Src_ui_root = found.SrcUiRoot ?? string.Empty,
      Prj_namespace = found.PrjNamespace ?? string.Empty,
    };

    // 상세는 실제로 고른 소스의 것이어야 한다 — 프로젝트만 받았을 때 요청의 src_rid 는 비어 있다.
    param["src_rid"] = found.SrcRid.ToString();

    srcInfo.SiDtlList = [.. SourceDetailRows(param).Select(d => new SrcInfoDtl {
      Src_dtl_rid = d.GetValue("src_dtl_rid"),
      Src_rid = d.GetValue("src_rid"),
      Src_extend = d.GetValue("src_extend"),
      Src_pattern_grp = d.GetValue("src_pattern_grp"),
      Url_pattern = d.GetValue("url_pattern"),
      Src_pattern_comment = d.GetValue("src_pattern_comment"),
      Src_pattern_nullvalue = d.GetValue("src_pattern_nullvalue"),
    })];

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
