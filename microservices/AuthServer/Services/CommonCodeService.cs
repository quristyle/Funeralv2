using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Mapster;

namespace AuthServer.Services;

public class CommonCodeService : ICommonCodeService
{
    private readonly AppDbContext _context;

    public CommonCodeService(AppDbContext context)
    {
        _context = context;
    }

    #region Group Management
    public async Task<IEnumerable<CommonCodeGroupDto>> GetGroupsAsync()
    {
        return await _context.CommonCodeGroups
            .OrderBy(g => g.GroupCode)
            .ProjectToType<CommonCodeGroupDto>()
            .ToListAsync();
    }

    public async Task<CommonCodeGroupDto> CreateGroupAsync(CommonCodeGroupCreateDto createDto)
    {
        var group = createDto.Adapt<CommonCodeGroup>();
        _context.CommonCodeGroups.Add(group);
        await _context.SaveChangesAsync();
        return group.Adapt<CommonCodeGroupDto>();
    }

    public async Task<bool> UpdateGroupAsync(string id, CommonCodeGroupCreateDto updateDto)
    {
        var group = await _context.CommonCodeGroups.FindAsync(id);
        if (group == null) return false;
        updateDto.Adapt(group);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteGroupAsync(string id)
    {
        var group = await _context.CommonCodeGroups.FindAsync(id);
        if (group == null) return false;
        _context.CommonCodeGroups.Remove(group);
        await _context.SaveChangesAsync();
        return true;
    }
    #endregion

    #region Code Management
    public async Task<IEnumerable<CommonCodeDto>> GetCodesByGroupAsync(string groupCode, bool hierarchical = false)
    {
        var query = _context.CommonCodes
            .Include(c => c.Group)
            .Where(c => c.Group!.GroupCode == groupCode && c.Status == 1);

        var entities = await query
            .OrderBy(c => c.Level)
            .ThenBy(c => c.SortOrder)
            .ToListAsync();

        var dtoList = entities.Adapt<List<CommonCodeDto>>();

        if (!hierarchical)
        {
            return dtoList;
        }

        // 계층 구조 (트리) 생성 로직
        return BuildTree(dtoList, null);
    }

    public async Task<CommonCodeDto> CreateCodeAsync(CommonCodeCreateDto createDto)
    {
        var code = createDto.Adapt<CommonCode>();
        
        // 레벨 및 IsLeaf 설정
        if (!string.IsNullOrEmpty(createDto.ParentId))
        {
            var parent = await _context.CommonCodes.FindAsync(createDto.ParentId);
            if (parent != null)
            {
                code.Level = parent.Level + 1;
                parent.IsLeaf = false;
            }
        }
        else
        {
            code.Level = 1;
        }

        _context.CommonCodes.Add(code);
        await _context.SaveChangesAsync();
        return code.Adapt<CommonCodeDto>();
    }

    public async Task<bool> UpdateCodeAsync(string id, CommonCodeCreateDto updateDto)
    {
        var code = await _context.CommonCodes.FindAsync(id);
        if (code == null) return false;
        updateDto.Adapt(code);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCodeAsync(string id)
    {
        var code = await _context.CommonCodes.FindAsync(id);
        if (code == null) return false;
        _context.CommonCodes.Remove(code);
        await _context.SaveChangesAsync();
        return true;
    }
    #endregion

    private List<CommonCodeDto> BuildTree(List<CommonCodeDto> source, string? parentId)
    {
        return source
            .Where(c => c.ParentId == parentId)
            .Select(c => {
                c.Children = BuildTree(source, c.Id);
                return c;
            })
            .ToList();
    }
}
