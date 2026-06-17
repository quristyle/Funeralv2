using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 회사 정보를 관리하는 서비스 클래스
/// </summary>
public class CompanyService : ICompanyService
{
    private readonly AppDbContext _context;

    public CompanyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CompanyDto>> GetAllCompaniesAsync()
    {
        return await _context.Companies
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CompanyDto
            {
                Id = c.Id,
                Name = c.Name,
                BusinessNumber = c.BusinessNumber,
                Representative = c.Representative,
                Status = c.Status,
                Remark = c.Remark,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<CompanyDto?> GetCompanyByIdAsync(string id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null) return null;

        return new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            BusinessNumber = company.BusinessNumber,
            Representative = company.Representative,
            Status = company.Status,
            Remark = company.Remark,
            CreatedAt = company.CreatedAt
        };
    }

    public async Task<CompanyDto> CreateCompanyAsync(CompanyCreateDto createDto)
    {
        var company = new Company
        {
            Name = createDto.Name,
            BusinessNumber = createDto.BusinessNumber,
            Representative = createDto.Representative,
            Status = createDto.Status,
            Remark = createDto.Remark
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        return new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            BusinessNumber = company.BusinessNumber,
            Representative = company.Representative,
            Status = company.Status,
            Remark = company.Remark,
            CreatedAt = company.CreatedAt
        };
    }

    public async Task<bool> UpdateCompanyAsync(string id, CompanyCreateDto updateDto)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null) return false;

        company.Name = updateDto.Name;
        company.BusinessNumber = updateDto.BusinessNumber;
        company.Representative = updateDto.Representative;
        company.Status = updateDto.Status;
        company.Remark = updateDto.Remark;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCompanyAsync(string id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null) return false;

        _context.Companies.Remove(company);
        await _context.SaveChangesAsync();
        return true;
    }
}
