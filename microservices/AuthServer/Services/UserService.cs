using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 사용자 정보 관련 비즈니스 로직 처리 서비스
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// UserService 생성자
    /// </summary>
    public UserService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 사용자 정보를 조회하고 DTO로 변환하여 반환
    /// </summary>
    /// <param name="userIdOrKey">사용자 아이디 또는 고유 키</param>
    public async Task<UserInfoDto?> GetUserInfoAsync(string userIdOrKey)
    {
        // 아이디 또는 UserId로 계정 조회
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userIdOrKey || a.Id == userIdOrKey);

        if (account == null) return null;

        // 프론트엔드 요구사항에 맞춰 DTO 구성
        return new UserInfoDto
        {
            Id = account.Id,
            UserId = account.UserId,
            Username = account.UserId,
            RealName = account.UserName,
            //Desc = account.Email ?? "등록된 설명이 없습니다.",
            Roles = new List<string> { "super" } // 기본 관리자 권한 부여
        };
    }
}
