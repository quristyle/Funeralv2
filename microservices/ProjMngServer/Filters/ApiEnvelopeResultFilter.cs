using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjModel;

namespace ProjMngServer.Filters;

/// <summary>ProjMng 응답을 표준 봉투(ApiResponse)로 감싸는 전역 결과 필터.</summary>
/// 프로시저 결과는 한 건이라 data.result 가 객체이고 행 수는 page.total 로 나간다. 음수 코드는 success:false + EPROC 로 나가며, 서비스 쪽 규약은 그대로다.
public sealed class ApiEnvelopeResultFilter : IAlwaysRunResultFilter {
  public void OnResultExecuting(ResultExecutingContext context) {
    if (context.Result is not ObjectResult objectResult) return;
    if (objectResult.Value is not IResultInfo ri) return; // 403 등 이미 표준 봉투인 것은 그대로

    if (ri.Code is < 0) {
      objectResult.Value = ApiResponse<object>.Fail(
        string.IsNullOrEmpty(ri.Message) ? "요청이 실패했습니다." : ri.Message!,
        code: "EPROC",
        realMessage: $"proc code {ri.Code}");
      objectResult.DeclaredType = typeof(ApiResponse<object>);
      return;
    }

    var rows = new List<object?>();
    if (ri.Rows is not null) {
      foreach (var row in ri.Rows) rows.Add(row);
    }

    var payload = new ProjEnvelopePayload(
      new {
        rows,
        res = ri.Res,
        cols = ri.Cols ?? new Dictionary<string, string>(),
        procCode = ri.Code ?? 0,
      },
      rows.Count);

    objectResult.Value = ApiResponse<ProjEnvelopePayload>.Ok(
      payload,
      string.IsNullOrEmpty(ri.Message) ? "Success" : ri.Message!);
    objectResult.DeclaredType = typeof(ApiResponse<ProjEnvelopePayload>);
  }

  public void OnResultExecuted(ResultExecutedContext context) { }
}

/// <summary>
/// <see cref="ApiResponse{T}"/> 의 페이징 통과 판정(Result·TotalCount 프로퍼티)을 태우기 위한 그릇.
/// 이 모양 덕에 <c>data.result</c> 가 배열이 아니라 **객체**로 나간다.
/// </summary>
public sealed record ProjEnvelopePayload(object Result, int TotalCount);
