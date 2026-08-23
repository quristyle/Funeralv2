using Microsoft.AspNetCore.Mvc;
using ProjMngServer.Services;
using ProjModel;

namespace ProjMngServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevController : ControllerBase {
  private readonly DevService _devService;

  public DevController(DevService devService) {
    _devService = devService;
  }

  //[HttpPost]
  //public ActionResult<Dictionary<string, object>> PostBody([FromBody] Dictionary<string, string> parameters) {
  //  var data = _devService.GetData(parameters);
  //  return Ok(data);
  //}



  [HttpPost]
  public ActionResult<ResultInfo<Dictionary<string, object>>> PostBody([FromBody] RequestDto dto) {
    ResultInfo<dynamic> data = null;
    if (dto.MultyData == null || dto.MultyData.Count <= 0) {
      data = _devService.GetData(dto);
    }
    else {
      data = _devService.ExcuteMultyData(dto);
    }
    return Ok(data);
  }








  [HttpPost]
  [Route("sql")]
  public ActionResult<Dictionary<string, object>> PostSql([FromBody] Dictionary<string, string> param) {

    string query = param.TryGetValue("query", out var dbValue) && dbValue != null ? dbValue.ToString() : string.Empty;
    string db_nick = param.TryGetValue("db_nick", out var stpValue) && stpValue != null ? stpValue.ToString() : string.Empty;
    string isBreakCnt = param.GetValue("isBreakCnt");

    var data = _devService.GetDataQuery(db_nick, query, isBreakCnt);
    return Ok(data);
  }

  [HttpPost]
  [Route("multy")]
  public ActionResult<Dictionary<string, object>> PostForm([FromForm] IFormCollection formData) {
    var parameters = formData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
    var data = _devService.GetData(parameters);
    return Ok(data);
  }

  [HttpGet]
  public ActionResult<Dictionary<string, object>> Get() {
    var parameters = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
    var data = _devService.GetData(parameters);
    return Ok(data);
  }

}