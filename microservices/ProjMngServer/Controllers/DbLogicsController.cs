using Microsoft.AspNetCore.Mvc;
using JSini.Shared.DTOs;
using ProjMngServer.Models;
using ProjMngServer.Services;

namespace ProjMngServer.Controllers;

/// <summary>
/// 시스템 질의 관리. 옛 <c>sp_devsqlresp_base_exec</c> 를 대신한다.
/// </summary>
[ApiController]
[Route("api/db-logics")]
public sealed class DbLogicsController(DbLogicService service) : ControllerBase {

  /// <summary>이름표 목록.</summary>
  [HttpGet]
  public async Task<ActionResult<ApiResponse<IReadOnlyList<DbLogicBase>>>> List(
      [FromQuery] string? dslCd, CancellationToken ct)
    => Ok(ApiResponse<IReadOnlyList<DbLogicBase>>.Ok(await service.ListAsync(dslCd, ct)));

  /// <summary>이름표를 넣거나 고친다.</summary>
  [HttpPost]
  public async Task<ActionResult<ApiResponse<DbLogicBase>>> Save(
      [FromBody] DbLogicBase item, CancellationToken ct) {

    if (string.IsNullOrWhiteSpace(item.DslCd)) {
      return BadRequest(ApiResponse<object>.Fail("질의 이름을 입력하세요.", "INVALID"));
    }

    return Ok(ApiResponse<DbLogicBase>.Ok(await service.SaveAsync(item, ct)));
  }

  /// <summary>이름표를 지운다. 딸린 질의가 있으면 막는다.</summary>
  [HttpDelete("{dslCd}")]
  public async Task<ActionResult<ApiResponse<bool>>> Delete(string dslCd, CancellationToken ct) {
    var reason = await service.DeleteAsync(dslCd, ct);

    return reason is null
      ? Ok(ApiResponse<bool>.Ok(true))
      : BadRequest(ApiResponse<object>.Fail(reason, "CONFLICT"));
  }

  /// <summary>이름표에 딸린 DB 종류별 질의.</summary>
  [HttpGet("{dslCd}/queries")]
  public async Task<ActionResult<ApiResponse<IReadOnlyList<DbLogicQuery>>>> Queries(
      string dslCd, CancellationToken ct)
    => Ok(ApiResponse<IReadOnlyList<DbLogicQuery>>.Ok(await service.QueriesAsync(dslCd, ct)));

  /// <summary>질의를 새로 넣는다.</summary>
  [HttpPost("{dslCd}/queries")]
  public async Task<ActionResult<ApiResponse<DbLogicQuery>>> CreateQuery(
      string dslCd, [FromBody] DbLogicQuery item, CancellationToken ct) {

    // 경로가 정본이다 — 본문이 다른 이름표를 가리켜도 여기 것으로 붙인다.
    item.DslCd = dslCd;

    return Ok(ApiResponse<DbLogicQuery>.Ok(await service.CreateQueryAsync(item, ct)));
  }

  /// <summary>질의를 고친다.</summary>
  [HttpPut("{dslCd}/queries/{dslId:long}")]
  public async Task<ActionResult<ApiResponse<DbLogicQuery>>> UpdateQuery(
      string dslCd, long dslId, [FromBody] DbLogicQuery item, CancellationToken ct) {

    item.DslCd = dslCd;
    item.DslId = dslId;

    var saved = await service.UpdateQueryAsync(item, ct);

    return saved is null
      ? NotFound(ApiResponse<object>.Fail("없는 질의입니다.", "NOT_FOUND"))
      : Ok(ApiResponse<DbLogicQuery>.Ok(saved));
  }

  /// <summary>질의를 지운다.</summary>
  [HttpDelete("{dslCd}/queries/{dslId:long}")]
  public async Task<ActionResult<ApiResponse<bool>>> DeleteQuery(
      string dslCd, long dslId, CancellationToken ct)
    => await service.DeleteQueryAsync(dslId, ct)
       ? Ok(ApiResponse<bool>.Ok(true))
       : NotFound(ApiResponse<object>.Fail("없는 질의입니다.", "NOT_FOUND"));
}
