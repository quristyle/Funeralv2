using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

public class DemoService : IDemoService
{
    public Task<PagedResultDto<DemoTableDto>> GetDemoTableListAsync(int page, int pageSize)
    {
        var items = new List<DemoTableDto>();
        for (int i = 1; i <= pageSize; i++)
        {
            items.Add(new DemoTableDto
            {
                Id = ((page - 1) * pageSize + i).ToString(),
                Title = $"샘플 게시글 제목 #{((page - 1) * pageSize + i)}",
                Author = "관리자",
                CreatedAt = DateTime.UtcNow.AddHours(-i),
                Status = i % 2 == 0 ? "published" : "draft"
            });
        }

        return Task.FromResult(new PagedResultDto<DemoTableDto>
        {
            Result = items,
            Page = new PageInfo
            {
                Total = 100 // 전체 데이터 개수 가상값
            }
        });
    }
}
