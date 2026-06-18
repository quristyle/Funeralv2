using AuthServer.DTOs;

namespace AuthServer.Services;

public interface ICommonCodeService
{
    // 그룹 관리
    Task<IEnumerable<CommonCodeGroupDto>> GetGroupsAsync();
    Task<CommonCodeGroupDto> CreateGroupAsync(CommonCodeGroupCreateDto createDto);
    Task<bool> UpdateGroupAsync(string id, CommonCodeGroupCreateDto updateDto);
    Task<bool> DeleteGroupAsync(string id);

    // 코드 관리
    Task<IEnumerable<CommonCodeDto>> GetCodesByGroupAsync(string groupCode, bool hierarchical = false);
    Task<CommonCodeDto> CreateCodeAsync(CommonCodeCreateDto createDto);
    Task<bool> UpdateCodeAsync(string id, CommonCodeCreateDto updateDto);
    Task<bool> DeleteCodeAsync(string id);
}
