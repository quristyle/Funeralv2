using System.ComponentModel.DataAnnotations;

namespace JSini.Web.Shell.Components.Pages;

/// <summary>로그인 폼에 담기는 값.</summary>
public sealed class LoginInput
{
    [Required(ErrorMessage = "아이디를 입력하세요.")]
    public string Username { get; set; } = "quristyle";//string.Empty;

    [Required(ErrorMessage = "비밀번호를 입력하세요.")]
    public string Password { get; set; } = "1";//string.Empty;
}
