using System.Security.Cryptography;
using System.Text;
using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>한 사람의 「역할 + 메뉴별 권한」을 짧은 지문 하나로 접는다. 포털이 30초마다 이것만 물어 보고, 들고 있는 것과 다를 때만 메뉴·권한표를 다시 받고 토큰을 새로 받는다 — 그래야 관리자가 바꾼 권한이 다시 로그인하지 않아도 먹는다.</summary>
public static class PermissionStamp
{
    public static string Of(IEnumerable<string>? roleIds, IEnumerable<MenuPermissionDto>? permissions)
    {
        var text = new StringBuilder();

        foreach (var role in (roleIds ?? []).Select(r => r.Trim().ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal))
        {
            text.Append(role).Append(',');
        }

        text.Append('|');

        foreach (var p in (permissions ?? []).OrderBy(p => p.Path, StringComparer.Ordinal).ThenBy(p => p.MenuId, StringComparer.Ordinal))
        {
            text.Append(p.Path).Append(':').Append(p.MenuId).Append(':')
                .Append(Bits(p.CanView, p.CanSearch, p.CanCreate, p.CanUpdate, p.CanDelete, p.CanPrint, p.CanExcel,
                    p.CanCust1, p.CanCust2, p.CanCust3, p.CanCust4, p.CanCust5, p.CanCust6, p.CanCust7, p.CanCust8))
                .Append(';');
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(digest.AsSpan(0, 12));
    }

    private static string Bits(params bool[] flags) => new([.. flags.Select(f => f ? '1' : '0')]);
}
