import re

with open('./microservices/AuthServer/Endpoints/SignupEndpoints.cs', 'r') as f:
    content = f.read()

merge_endpoint = '''
        admin.MapPost("/{id}/merge", async (
            string id,
            [FromQuery] string targetId,
            UserContext? user,
            [FromServices] ISignupService signup,
            CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var ok = await signup.MergeAsync(id, targetId, user.UserId, ct);

            return ok
                ? Results.Ok(ApiResponse<object>.Ok(data: null!, message: "병합했습니다."))
                : Results.BadRequest(ApiResponse<object>.Fail(
                    "병합할 수 없습니다. 목록을 다시 읽어 주십시오.", "INVALID"));
        })
        .WithName("MergeSignup");
'''

content = content.replace(
    '        .WithName("RejectSignup");',
    '        .WithName("RejectSignup");' + merge_endpoint
)

with open('./microservices/AuthServer/Endpoints/SignupEndpoints.cs', 'w') as f:
    f.write(content)
