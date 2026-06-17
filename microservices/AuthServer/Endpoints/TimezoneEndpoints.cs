using Microsoft.AspNetCore.Mvc;
using AuthServer.Entities;
using AuthServer.Services;
using AuthServer.DTOs;

namespace AuthServer.Endpoints;

public static class TimezoneEndpoints
{
    public static void MapTimezoneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/timezone");

        group.MapGet("/getTimezone", async (UserContext? user, [FromServices] ITimezoneService timezoneService) =>
        {
            if (user is null) return Results.Unauthorized();
            var timezone = await timezoneService.GetCurrentTimezoneAsync(user.UserId);
            return Results.Ok(ApiResponse<string>.Success(timezone));
        })
        .WithName("GetTimezone")
        .WithOpenApi();

        group.MapGet("/getTimezoneOptions", async ([FromServices] ITimezoneService timezoneService) =>
        {
            var options = await timezoneService.GetTimezoneOptionsAsync();
            return Results.Ok(ApiResponse<List<TimezoneOptionDto>>.Success(options));
        })
        .WithName("GetTimezoneOptions")
        .WithOpenApi();
    }
}
