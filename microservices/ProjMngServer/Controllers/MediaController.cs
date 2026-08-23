using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Services;
using ProjModel;
using System.Collections.Generic;

namespace ProjMngServer.Controllers;


  /* description : 서버측 IO 관련 자료를 요청에 사용되는 컨트롤 
  * title : 서버측 IO 접근 Controller 
  * sort : 2 
  * credt : 2021-09-01
  * author : quristyle
  */



[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase {
  private readonly ProjService _projService;

  public MediaController(ProjService projService) {
    _projService = projService;
  }

  //[HttpPost]
  //public ActionResult<Dictionary<string, object>> PostBody([FromBody] Dictionary<string, string> parameters) {

  //  string req_cname = parameters.TryGetValue("req_cname", out var stpValue) && stpValue != null ? stpValue.ToString().ToLower() : string.Empty;

  //  var data = _projService.GetMdData(req_cname,parameters);
  //  return Ok(data);
  //}




  [HttpPost]
  public ActionResult<ResultInfo<Dictionary<string, object>>> PostBody([FromBody] RequestDto dto) {
    //ResultInfo<dynamic> data = null;
    //if (
    //dto.MultyData == null || dto.MultyData.Count <= 0) {
    ResultInfo<Dictionary<string, string>> data = null;

    if (dto.ProcName == "md_blazor_scan") {
      data = _projService.GetMdBlazorData(dto);
    }
    else if (dto.ProcName == "md_glue_service") {
      data = _projService.GetMdGlueData (dto);
    }
    else if (dto.ProcName == "md_source_trace") {
      data = _projService.GetMdSourData(dto);
    }
    else if (dto.ProcName == "md_source_context") {
      data = _projService.GetMdContent(dto);
    }


    return Ok(data);
  }



}