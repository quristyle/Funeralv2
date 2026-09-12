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

  /// <summary>
  /// 소스 정보를 읽는 곳. <b>프로시저를 부르지 않는다.</b>
  ///
  /// <para>
  /// 이 클래스는 프로시저를 부르는 범용 통로인데, <c>md_</c> 갈래(파일 훑기)만은
  /// <b>자기도 자료를 읽어야</b> 해서 안에서 <c>sp_dev_srcinfo_exec</c> ·
  /// <c>sp_dev_srcinfo_dtl_exec</c> 를 불렀다. 프로시저를 지우면 그 세 자리가
  /// 조용히 빈 결과를 내므로(오류가 아니다) 함께 옮긴다.
  /// </para>
  /// </summary>
  private readonly SourceInfoService _sources;

  /// <summary>
  /// Glue 자료를 쌓는 곳. <c>md_glue_service</c> 가 훑은 결과를 여기에 넣는다.
  /// 옛 길은 <c>sp_dev_activityinfo_exec</c> 를 줄 수만큼 부르는 것이었다.
  /// </summary>
  private readonly ActivityInfoService _activities;

  public ProjService(
      IConfiguration configuration, SourceInfoService sources, ActivityInfoService activities)
      : base(configuration) {
    _sources = sources;
    _activities = activities;
  }

  /// <summary>
  /// 소스 상세를 <c>src_rid</c> 로 읽어 옛 프로시저가 주던 사전 모양으로 맞춘다.
  /// 부르는 쪽이 칸 이름을 글자로 집기 때문에 <b>이름을 바꾸지 않는다.</b>
  /// </summary>
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

  // [프로시저를 부르던 곳이 여기 있었다]
  //
  // `GetData(procName, …)` · `ExcuteMultyData(…)` 가 이 자리에 있었다.
  // 이름을 받아 그대로 부르는 통로였고, 그 이름은 **브라우저가 정했다.**
  // 2026-09-12 에 업무 프로시저를 전부 백엔드로 옮기면서 부르는 쪽이
  // 없어졌고, 통로(`/api/Proj` · `/api/Sys`)와 함께 걷어냈다.
  //
  // 이 클래스에 남은 것은 **파일을 훑는 일**(`md_*`)뿐이다. 그쪽이 읽어야 하는
  // 소스 정보는 `SourceInfoService` 가 낸다 — 위 생성자 주석 참고.

  // 주석으로만 남아 있던 옛 `GetMdData` 를 걷어냈다(2026-09-12).
  // 하던 일은 아래 `GetMdBlazorData` 가 그대로 한다 — 그쪽은 소스 정보를
  // 프로시저가 아니라 `SourceInfoService` 에서 읽는다.


  public ResultInfo<Dictionary<string, string>> GetMdBlazorData(RequestDto dto) {

    IDictionary<string, string> param = dto.MainParam;
    //param["req_type"] = "srch";

    ResultInfo<Dictionary<string, string>> ri = new ResultInfo<Dictionary<string, string>>();
    GetBlazorFile(ri, param);
    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
    return ri;

  }

  /// <summary>
  /// 소스 상세에서 그 확장자에 딸린 행을 찾는다. <b>경로가 적힌 행을 먼저</b> 고른다.
  ///
  /// <para>
  /// [같은 칸에 다른 것이 들어 있다]
  /// </para>
  /// <para>
  /// <c>url_pattern</c> 은 <c>src_pattern_grp</c> 에 따라 뜻이 다르다 —
  /// <c>src_path</c> 면 훑을 뿌리 경로이고, <c>url</c> 이면 화면 주소를 뽑는
  /// 정규식이다. 예전에는 확장자만 보고 첫 행을 집어서, <c>url</c> 행이 먼저
  /// 오는 소스(razor)에서는 <b>정규식을 경로라고 들고 가</b> 파일 훑기가
  /// 그 자리에서 터졌다. 「소스 추적」이 늘 500 이던 이유다.
  /// </para>
  /// <para>
  /// 그런 행이 없으면 예전처럼 첫 행을 준다 — 옛 자료를 깨지 않는다.
  /// </para>
  /// </summary>
  IDictionary<string,object>? GetUrlPattern(IDictionary<string, string> param, string src_extend) {

    var all = SourceDetailRows(param);

    var rows = all
      .Where(d => d.ContainsKey("src_extend") && d["src_extend"]?.ToString() == src_extend)
      .ToList();

    var byExtend = rows.FirstOrDefault(d => d.GetValue("src_pattern_grp") == "src_path");
    if (byExtend != null) { return byExtend; }

    // 확장자에 딸린 경로가 없다. 소스 하나에 뿌리 경로는 보통 하나이므로
    // **확장자를 안 적어 둔 경로 행**을 쓴다 — 그렇게 등록된 소스가 실제로 있다.
    var anyPath = all.FirstOrDefault(d => d.GetValue("src_pattern_grp") == "src_path");

    return anyPath ?? rows.FirstOrDefault();
  }

  /// <summary>
  /// 훑을 뿌리 경로가 쓸 만한지 본다. 아니면 그 이유를 <paramref name="reason"/> 에 담는다.
  ///
  /// <para>
  /// 서버가 훑는 것은 <b>서버 장비의 디스크</b>다. 등록된 경로가 그 장비에
  /// 없는 것은 흔한 일이고(개발 장비에 등록해 둔 경로가 대부분이다),
  /// 그것이 500 일 이유가 없다. 빈 결과와 안내로 돌려준다.
  /// </para>
  /// </summary>
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

      // 훑을 수 없으면 **DB 를 건드리지 않는다.** 예전에는 여기서 그냥 지나쳐
      // 「수집했다」처럼 끝났고, 화면은 왜 아무것도 안 늘었는지 알 수 없었다.
      ri.Code = -88;
      ri.Message = reason;
      ri.Data = aaa;

      GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
      return ri;
    }

    {
      var activeList = ActivityParser.ParseActivityFiles(path);

      // **훑은 것으로 그 소스의 자료를 통째로 갈아 끼운다.**
      //
      // 옛 길은 줄마다 프로시저를 불러 덮어쓰기만 했다. 그래서 파일에서
      // 없어진 서비스가 DB 에 그대로 남았고, 추적 화면에는 있는데 소스에는
      // 없는 줄이 되었다 — 재수집해도 사라지지 않으니 사람이 알 방법이 없다.
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

    GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
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

      GetRes<Dictionary<string, string>>(ref ri, param, DateTime.Now, DateTime.Now, DateTime.Now);
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


    // 소스를 골라 왔으면 그것을, 안 골랐으면 **그 프로젝트의 첫 소스**를 훑는다.
    //
    // 프로젝트만 주는 화면이 있다(소스 스캐너 · 진행 현황). 옛 길은 빈
    // `src_rid` 를 프로시저에 그대로 넘겨 **등록된 소스 전부 중 첫 줄**을
    // 집었다 — 다른 프로젝트의 소스를 훑고 있어도 알 수가 없었다.
    // 적어도 고른 프로젝트 안에서 고른다.
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

    // 등록된 소스가 없으면 훑을 것도 없다. 전에는 바로 아래에서 터졌다.
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

    // 상세는 **실제로 고른 소스**의 것이어야 한다. 프로젝트만 받았을 때
    // 요청의 `src_rid` 는 비어 있다.
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
