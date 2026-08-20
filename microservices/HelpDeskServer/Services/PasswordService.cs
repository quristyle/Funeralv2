using Microsoft.AspNetCore.Identity;

namespace HelpDeskServer.Services
{
    /// <summary>
    /// 비밀번호 해시를 포함하는 엔티티를 위한 인터페이스
    /// </summary>
    public interface IPasswordEnabled
  {
      /// <summary>
      /// 비밀번호 해시
      /// </summary>
        string PasswordHash { get; set; }
    }

    /// <summary>
    /// 비밀번호 해싱 및 검증을 처리하는 서비스
    /// </summary>
    public class PasswordService
    {
        /// <summary>
        /// 지정된 사용자의 비밀번호를 해시합니다.
        /// </summary>
        public string HashPassword<TUser>(TUser user, string password) where TUser : class, IPasswordEnabled
        {
            var hasher = new PasswordHasher<TUser>();
            return hasher.HashPassword(user, password);
        }

        /// <summary>
        /// 제공된 비밀번호가 사용자의 해시된 비밀번호와 일치하는지 확인합니다.
        /// </summary>
        public bool VerifyPassword<TUser>(TUser user, string providedPassword) where TUser : class, IPasswordEnabled
        {
            var hasher = new PasswordHasher<TUser>();
            return hasher.VerifyHashedPassword(user, user.PasswordHash, providedPassword) == PasswordVerificationResult.Success;
        }
    }
}