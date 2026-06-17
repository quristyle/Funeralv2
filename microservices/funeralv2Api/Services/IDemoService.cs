using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

public interface IDemoService
{
    Task<PagedResultDto<DemoTableDto>> GetDemoTableListAsync(int page, int pageSize);
}
