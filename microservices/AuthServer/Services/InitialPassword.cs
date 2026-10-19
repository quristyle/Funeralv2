using System.Security.Cryptography;

namespace AuthServer.Services;

/// <summary>새로 만든 계정에 처음 넣어 줄 비밀번호를 정한다. 저장은 해시로 하고, 발급한 평문은 응답에 한 번만 실어 보낸다.</summary>
public static class InitialPassword
{
    /// <summary>고정 기본값을 두고 싶을 때 쓰는 설정 키.</summary>
    public const string ConfigKey = "Auth:DefaultPassword";

    /// <summary>무작위로 발급할 때의 길이.</summary>
    private const int RandomLength = 12;

    /// <summary>무작위 발급에 쓰는 글자. 헷갈리는 글자(0 O o · 1 l I)는 뺐다 — 사람이 옮겨 적거나 불러 주는 값이라 못 알아보는 글자가 섞이면 「비밀번호가 틀렸다」로 되돌아온다.</summary>
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>설정에 고정값이 있으면 그것을, 없으면 무작위 값을 준다.</summary>
    public static string Issue(IConfiguration config)
    {
        var configured = config[ConfigKey];

        return string.IsNullOrWhiteSpace(configured)
            ? RandomNumberGenerator.GetString(Alphabet, RandomLength)
            : configured;
    }
}
