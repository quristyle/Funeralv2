using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjModel;

namespace ProjMngServer.Filters;

/// <summary>
/// ProjMng 응답을 표준 봉투(<see cref="ApiResponse{T}"/>)로 감싸는 전역 결과 필터 (결정 D-A1).
/// </summary>
/// <remarks>
/// <para>
/// 이 서비스는 ProjMngWasm 시절 봉투(<c>{ code, message, res, cols, data }</c>, 숫자 code)를
/// 그대로 내보내던 두 예외 중 하나였다(45번 문서 2절). 이제 다른 여섯 서비스와 같은
/// <c>{ success, code: "S000", message, data: { result, page } }</c> 로 나간다.
/// </para>
/// <para>
/// <b>내보내는 모양</b> — 프로시저 실행 결과는 '한 건'이므로 페이징 통과 경로를 태워
/// <c>result</c> 가 **객체**가 되게 한다(단일 객체를 배열로 감싸는 D-A1 의 병폐를
/// 여기서는 만들지 않는다). 행 수는 <c>page.total</c> 로 나간다.
/// </para>
/// <code>
/// data: {
///   result: { rows: [...], res: {...}, cols: {...}, procCode: 0 },
///   page:   { total: N }
/// }
/// </code>
/// <para>
/// <b>실패(음수 code)</b> 는 <c>success: false</c> + <c>code: "EPROC"</c> 로 나간다.
/// 프로시저의 음수 코드는 <c>realmessage</c> 에 남긴다. HTTP 는 예전처럼 200 이다 —
/// 옛 봉투도 실패를 200 + 음수 code 로 보냈고, 화면 쪽 처리(토스트 후 빈 결과)가
/// 그 전제 위에 있다.
/// </para>
/// <para>
/// <b>서비스·프로시저 쪽은 건드리지 않는다.</b> <see cref="ResultInfo{T}"/> 와 음수 코드
/// 규약은 내부에 그대로 있고, 여기 직렬화 경계에서만 갈아입는다. 그래서 DB 프로시저
/// 규약(45번 문서가 걱정한 그것)은 아무 영향이 없다.
/// </para>
/// <para>
/// <b>HelpDeskServer 에는 이런 필터를 만들지 않는다.</b> 그쪽 봉투는 살아 있는
/// JinReception 이 실사용 중이라, DB 이관이 끝난 뒤에만 손댄다(사용자 지시, 2026-09-04).
/// </para>
/// </remarks>
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
