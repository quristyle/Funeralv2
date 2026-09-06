using System.Security.Cryptography;

namespace AuthServer.Services;

/// <summary>
/// 새로 만든 계정에 처음 넣어 줄 비밀번호를 정한다.
/// </summary>
/// <remarks>
/// <para>
/// 한동안 계정 등록이 <c>"1234"</c> 를 <b>평문 그대로</b> 넣었다.
/// <see cref="PasswordHasher.Verify"/> 가 해시 형식이 아닌 저장값을 평문으로 보고
/// 비교하는(의도된 하위호환) 덕분에 로그인은 됐지만, 그래서 <b>한 번도 로그인하지
/// 않은 계정은 DB 에 비밀번호가 그대로 보이는 채로 남았다.</b> 지금은 여기서 정한
/// 값을 <see cref="PasswordHasher.Hash"/> 로 해시해 저장한다.
/// </para>
///
/// <para>
/// [기본값을 무엇으로 하는가]
/// </para>
///
/// <para>
/// 기본은 <b>계정마다 다른 무작위 값</b>이다. 고정 기본값은 그 자체가 열쇠라
/// 「아무나 새 계정으로 로그인할 수 있다」와 사실상 같고, 실제로 운영 DB 에
/// <c>1234</c> 계정이 스물세 개 쌓여 있었다.
/// </para>
///
/// <para>
/// 그래도 고정값을 쓰려면 설정 한 줄로 되돌릴 수 있다. 값이 있으면 그것을 쓴다.
/// </para>
///
/// <code>
/// "Auth": { "DefaultPassword": "1234" }
/// </code>
///
/// <para>
/// 발급한 값은 <b>등록 응답에 한 번만</b> 실려 나가고(<c>AccountDto.InitialPassword</c>)
/// 어디에도 다시 남기지 않는다. 로그에도 적지 않는다 — 로그는 사람이 많이 보고
/// 오래 남는다.
/// </para>
///
/// <para>
/// 발급된 계정은 첫 로그인에서 곧바로 비밀번호를 바꾸게 된다
/// (<see cref="PasswordPolicy.AlreadyExpiredAt"/>).
/// </para>
/// </remarks>
public static class InitialPassword
{
    /// <summary>고정 기본값을 두고 싶을 때 쓰는 설정 키.</summary>
    public const string ConfigKey = "Auth:DefaultPassword";

    /// <summary>무작위로 발급할 때의 길이.</summary>
    private const int RandomLength = 12;

    /// <summary>
    /// 무작위 발급에 쓰는 글자.
    ///
    /// <b>헷갈리는 글자를 뺐다</b>(<c>0 O o · 1 l I</c>). 이 값은 화면에 떠서
    /// 사람이 옮겨 적거나 불러 주는 것이라, 못 알아보는 글자가 섞이면
    /// 「비밀번호가 틀렸다」로 되돌아온다.
    /// </summary>
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// 설정에 고정값이 있으면 그것을, 없으면 무작위 값을 준다.
    /// </summary>
    public static string Issue(IConfiguration config)
    {
        var configured = config[ConfigKey];

        return string.IsNullOrWhiteSpace(configured)
            ? RandomNumberGenerator.GetString(Alphabet, RandomLength)
            : configured;
    }
}
