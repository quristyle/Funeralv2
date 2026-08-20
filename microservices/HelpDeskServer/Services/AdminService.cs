using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HelpDeskServer.Data;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>
/// 관리자 관련 비즈니스 로직을 처리하는 서비스의 인터페이스입니다.
/// </summary>
public interface IAdminService {
  /// <summary>
  /// 이메일 수신을 설정한 관리자의 이메일 주소 목록을 비동기적으로 가져옵니다.
  /// </summary>
  /// <returns>관리자 이메일 주소 목록입니다.</returns>
  Task<List<string>> GetAdminEmailsForNotificationAsync();
  Task<List<string>> GetCustomerEmailsForNotificationAsync();
  Task<List<string>> GetCustomerEmailsForNotificationAsync(int customerId);




}

/// <summary>
/// 관리자 관련 비즈니스 로직을 처리하는 서비스입니다.
/// </summary>
public class AdminService : IAdminService {
  private readonly AppDbContext _db;

  /// <summary>
  /// AdminService의 새 인스턴스를 초기화합니다.
  /// </summary>
  /// <param name="db">데이터베이스 컨텍스트입니다.</param>
  public AdminService(AppDbContext db) {
    _db = db;
  }

  /// <summary>
  /// 이메일 수신을 설정한 관리자의 이메일 주소 목록.
  /// </summary>
  /// <returns></returns>
  public async Task<List<string>> GetAdminEmailsForNotificationAsync() {
    return await _db.Admins
        .Where(a => _db.UserProperties.Any(up =>
            up.UserId == a.Id &&
            up.UserType == "admin" &&
            up.Key == "receiveEmail" &&
            up.Value == "true"))
        .Select(a => a.Email)
        .ToListAsync();
  }


  /// <summary>
  /// 고객 이메일 수신을 설정한 고객의 이메일 주소 목록을 가져옵니다.
  /// </summary>
  /// <returns></returns>
  public async Task<List<string>> GetCustomerEmailsForNotificationAsync() {
    return await _db.Customers
        .Where(c => _db.UserProperties.Any(up =>
            up.UserId == c.Id &&
            up.UserType == "customer" &&
            up.Key == "receiveEmail" &&
            up.Value == "true"))
        .Select(c => c.Email)
        .ToListAsync();
  }

  /// <summary>
  /// 특정 고객이 이메일 수신을 설정한 고객의 이메일 주소 목록을 가져옵니다.
  /// </summary>
  /// <param name="customerId"></param>
  /// <returns></returns>
  public async Task<List<string>> GetCustomerEmailsForNotificationAsync(int customerId) {
    return await _db.Customers
        .Where(c => _db.UserProperties.Any(up =>
            up.UserId == c.Id &&
            c.Id == customerId &&
            up.UserType == "customer" &&
            up.Key == "receiveEmail" &&
            up.Value == "true"))
        .Select(c => c.Email)
        .ToListAsync();
  }
}
