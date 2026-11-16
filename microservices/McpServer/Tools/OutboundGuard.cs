using System.Net;
using System.Net.Sockets;

namespace McpServer.Tools;

/// <summary>본문 가져오기가 내부망·장비 자신·클라우드 메타데이터로 새지 않게 막는다.
/// 이름 검사만으로는 nip.io 같은 DNS·리다이렉트·IPv6 표기로 뚫리므로 **실제로 붙는 IP** 를 접속 직전에 본다.</summary>
public static class OutboundGuard
{
    /// <summary>모든 새 접속(리다이렉트 포함)에서 돈다. DNS 를 직접 풀고 거른 IP 로만 붙어 확인과 접속 사이에 이름이 바뀌어도 못 샌다.</summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        // 프록시를 타면 ConnectCallback 이 프록시에만 붙어 검사가 무의미해진다.
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        ConnectCallback = async (ctx, ct) =>
        {
            var host = ctx.DnsEndPoint.Host;
            if (RejectHost(host) is { } why) throw new HttpRequestException(why);

            var addrs = IPAddress.TryParse(host, out var lit)
                ? [lit]
                : await Dns.GetHostAddressesAsync(host, ct);

            // 하나라도 막힌 주소면 통째로 거절한다 — 섞어 두고 고르는 쪽을 노리는 수를 막는다.
            if (addrs.Length == 0 || addrs.Any(IsBlocked))
                throw new HttpRequestException($"내부망 주소는 열지 않습니다: {host}");

            Exception? last = null;
            foreach (var ip in addrs)
            {
                var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(ip, ctx.DnsEndPoint.Port), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    socket.Dispose();
                    last = ex;
                }
            }
            throw last ?? new HttpRequestException($"접속하지 못했습니다: {host}");
        },
    };

    /// <summary>주소 모양만으로 걸러지는 것. 점 없는 이름은 도커 서비스 이름(auth·projmng …)이라 막는다.</summary>
    public static string? RejectUrl(Uri u)
    {
        if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)
            return $"http/https 주소가 아닙니다: {u}";
        return RejectHost(u.Host);
    }

    private static string? RejectHost(string rawHost)
    {
        var host = rawHost.Trim('[', ']').TrimEnd('.').ToLowerInvariant();
        if (IPAddress.TryParse(host, out var ip))
            return IsBlocked(ip) ? $"내부망 주소는 열지 않습니다: {host}" : null;

        if (!host.Contains('.')
            || host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal)
            || host.EndsWith(".local", StringComparison.Ordinal)
            || host.EndsWith(".internal", StringComparison.Ordinal)
            || host.EndsWith(".lan", StringComparison.Ordinal)
            || host.EndsWith(".home.arpa", StringComparison.Ordinal))
            return $"내부망 주소는 열지 않습니다: {host}";

        return null;
    }

    public static bool IsBlocked(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 0                                   // 0/8 (0.0.0.0 포함)
                || b[0] == 10                                  // 10/8
                || b[0] == 127                                 // 루프백
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)  // CGNAT 100.64/10
                || (b[0] == 169 && b[1] == 254)                // 링크로컬·메타데이터
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16/12
                || (b[0] == 192 && b[1] == 168)                // 192.168/16
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)     // 192.0.0/24
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19)) // 198.18/15
                || b[0] >= 224;                                // 멀티캐스트·예약·브로드캐스트
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any)
                || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
                return true;

            var b = ip.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return true;            // ULA fc00::/7

            // IPv4 를 품은 표기(호환 ::a.b.c.d · NAT64 64:ff9b::/96 · 6to4 2002::/16)는 품은 IPv4 로 다시 본다.
            if (b.Take(12).All(x => x == 0))
                return IsBlocked(new IPAddress(b[12..16]));
            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b && b[4..12].All(x => x == 0))
                return IsBlocked(new IPAddress(b[12..16]));
            if (b[0] == 0x20 && b[1] == 0x02)
                return IsBlocked(new IPAddress(b[2..6]));
            return false;
        }

        return true;
    }
}
