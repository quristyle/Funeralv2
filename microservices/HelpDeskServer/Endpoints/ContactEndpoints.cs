using HelpDeskServer.Dtos;
using HelpDeskServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// Contact 관련 엔드포인트
/// </summary>
public static class ContactEndpoints {
  /// <summary>
  /// Contact 엔드포인트 매핑
  /// </summary>
  /// <param name="routes"></param>
  public static void MapContactEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/contact").WithTags("Contact");

    // 문의 메시지 전송
    group.MapPost("/", async ([FromBody] ContactUsDto contactUsDto, [FromServices] ContactService contactService) => {
      try {
        await contactService.SendContactEmailAsync(contactUsDto);
        return Results.Ok(new { message = "메시지가 성공적으로 전송되었습니다." });
      }
      catch (InvalidOperationException ex) {
        return Results.Problem(ex.Message, statusCode: 500);
      }
      catch (Exception ex) {
        return Results.Problem($"메시지 전송 중 오류가 발생했습니다: {ex.Message}", statusCode: 500);
      }
    })
    .WithName("SendContactMessage")
    .Produces(200)
    .Produces(500);
  }
}
