using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;

namespace HelpDeskServer.Endpoints;

public static class WbsDiagramEndpoints
{
    public static void MapWbsDiagramEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/wbs-diagram").WithTags("WbsDiagram");

        // WBS ID로 다이어그램 조회
        group.MapGet("/{wbsRid}", async (int wbsRid, AppDbContext db) =>
        {
            var diagram = await db.WbsDiagrams
                .FirstOrDefaultAsync(d => d.WbsRid == wbsRid);
            
            if (diagram == null)
            {
                return Results.NotFound(new ApiResponse<WbsDiagram>(false, "Diagram not found for this WBS item.", null, null));
            }

            return Results.Ok(new ApiResponse<WbsDiagram>(true, "Success", diagram, null));
        });

        // 다이어그램 저장 (생성 또는 수정)
        group.MapPost("/", async (WbsDiagram diagram, AppDbContext db) =>
        {
            var existing = await db.WbsDiagrams
                .FirstOrDefaultAsync(d => d.WbsRid == diagram.WbsRid);

            if (existing != null)
            {
                existing.DiagramData = diagram.DiagramData;
                db.WbsDiagrams.Update(existing);
                await db.SaveChangesAsync();
                return Results.Ok(new ApiResponse<WbsDiagram>(true, "Updated", existing, null));
            }
            else
            {
                db.WbsDiagrams.Add(diagram);
                await db.SaveChangesAsync();
                return Results.Created($"/api/wbs-diagram/{diagram.WbsRid}", new ApiResponse<WbsDiagram>(true, "Created", diagram, null));
            }
        });
    }
}
