using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace SiteServer.Services;

/// <summary>
/// 문의 폼의 자체 캡차. 외부 스크립트를 부르지 않는다(D-S4) — 그림은 여기서 PNG 로 만들고
/// 정답은 어디에도 저장하지 않는다. 토큰에 <c>HMAC(열쇠, 발급정보+정답)</c> 만 실어,
/// 맞는 답을 가진 사람만 같은 서명을 다시 만들 수 있다. 한 토큰은 맞든 틀리든 한 번만 검사한다.
/// </summary>
public interface ICaptchaService
{
    CaptchaChallenge Issue();
    CaptchaVerdict Verify(string? token, string? answer);
}

public sealed record CaptchaChallenge(string Token, byte[] Png);

public enum CaptchaVerdict
{
    Passed,
    Missing,
    Malformed,
    Expired,
    TooFast,
    Reused,
    Wrong,
}

public sealed class CaptchaService : ICaptchaService
{
    // 헷갈리는 글자(0/O, 1/I, 2/Z, 5/S, 8/B, G)는 뺐다.
    private const string Alphabet = "ACDEFHJKLMNPRTUVWXY34679";
    private const int Length = 5;
    private const int Width = 180;
    private const int Height = 60;

    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    // 폼을 받자마자 보내는 것은 사람이 아니다. 그림을 읽고 다섯 글자를 치는 데 2초는 걸린다.
    private static readonly TimeSpan MinSolve = TimeSpan.FromSeconds(2);

    private readonly byte[] _key;
    private readonly IMemoryCache _used;
    private readonly TimeProvider _clock;
    private readonly ILogger<CaptchaService> _logger;

    public CaptchaService(IConfiguration config, IMemoryCache cache, ILogger<CaptchaService> logger)
        : this(config, cache, logger, TimeProvider.System) { }

    public CaptchaService(IConfiguration config, IMemoryCache cache, ILogger<CaptchaService> logger, TimeProvider clock)
    {
        _used = cache;
        _clock = clock;
        _logger = logger;

        // 열쇠를 주지 않으면 기동마다 새로 만든다. 재기동 순간 풀던 캡차만 틀린 것이 되고 새 그림이 나온다.
        var configured = config["Captcha:Key"];
        _key = string.IsNullOrWhiteSpace(configured)
            ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(configured));
    }

    public CaptchaChallenge Issue()
    {
        var answer = string.Create(Length, 0, static (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });

        var payload = new byte[24];
        RandomNumberGenerator.Fill(payload.AsSpan(0, 16));
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(16), _clock.GetUtcNow().ToUnixTimeMilliseconds());

        var token = $"{Base64Url(payload)}.{Base64Url(Sign(payload, answer))}";
        return new CaptchaChallenge(token, CaptchaImage.Render(answer));
    }

    public CaptchaVerdict Verify(string? token, string? answer)
    {
        var verdict = VerifyCore(token, answer);
        if (verdict != CaptchaVerdict.Passed)
        {
            _logger.LogInformation("문의 캡차 불통과: {Verdict}", verdict);
        }
        return verdict;
    }

    private CaptchaVerdict VerifyCore(string? token, string? answer)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(answer))
        {
            return CaptchaVerdict.Missing;
        }

        if (token.Length > 128) return CaptchaVerdict.Malformed;
        var dot = token.IndexOf('.');
        if (dot <= 0) return CaptchaVerdict.Malformed;

        var payload = FromBase64Url(token[..dot]);
        var signature = FromBase64Url(token[(dot + 1)..]);
        if (payload is not { Length: 24 } || signature is not { Length: 16 })
        {
            return CaptchaVerdict.Malformed;
        }

        var now = _clock.GetUtcNow();
        var issued = DateTimeOffset.FromUnixTimeMilliseconds(BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(16)));
        var age = now - issued;
        if (age > MaxAge || age < TimeSpan.FromMinutes(-1)) return CaptchaVerdict.Expired;

        // 답을 맞히기 전에 먼저 써 버린다 — 같은 토큰으로 답을 바꿔 가며 두드리지 못하게.
        var id = "captcha:" + Convert.ToHexString(payload, 0, 16);
        if (_used.TryGetValue(id, out _)) return CaptchaVerdict.Reused;
        _used.Set(id, true, issued + MaxAge + TimeSpan.FromMinutes(1));

        if (age < MinSolve) return CaptchaVerdict.TooFast;

        var normalized = Normalize(answer);
        if (normalized.Length != Length) return CaptchaVerdict.Wrong;

        return CryptographicOperations.FixedTimeEquals(Sign(payload, normalized), signature)
            ? CaptchaVerdict.Passed
            : CaptchaVerdict.Wrong;
    }

    /// <summary>사람이 친 답을 고른다. 대소문자와 사이 공백을 가리지 않는다.</summary>
    public static string Normalize(string answer)
    {
        var sb = new StringBuilder(Length);
        foreach (var ch in answer)
        {
            if (char.IsWhiteSpace(ch) || ch == '-') continue;
            sb.Append(char.ToUpperInvariant(ch));
            if (sb.Length > 16) break;
        }
        return sb.ToString();
    }

    private byte[] Sign(byte[] payload, string answer)
    {
        var data = new byte[payload.Length + answer.Length];
        payload.CopyTo(data, 0);
        Encoding.ASCII.GetBytes(answer, data.AsSpan(payload.Length));
        return HMACSHA256.HashData(_key, data)[..16];
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        try
        {
            return Convert.FromBase64String(s);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// 글자를 그림으로 그린다. 글자마다 크기·기울기·높이를 흔들고 화면 전체를 물결로 비튼 뒤
    /// 글자색 곡선과 점을 덮는다. System.Drawing 은 리눅스 컨테이너에 없어 획(선분)으로 된 글꼴을 직접 그린다.
    /// </summary>
    internal static class CaptchaImage
    {
        // 4×6 칸 위의 꺾은선들. 점자(비트맵)로 그리면 기울인 순간 사선 획이 모서리로만 이어져 끊어진다.
        private static readonly Dictionary<char, float[][]> Font = new()
        {
            ['A'] = [[0, 6, 2, 0, 4, 6], [0.8f, 3.6f, 3.2f, 3.6f]],
            ['C'] = [[4, 1, 3, 0, 1, 0, 0, 1, 0, 5, 1, 6, 3, 6, 4, 5]],
            ['D'] = [[0, 0, 0, 6, 2.5f, 6, 4, 4.5f, 4, 1.5f, 2.5f, 0, 0, 0]],
            ['E'] = [[4, 0, 0, 0, 0, 6, 4, 6], [0, 3, 3, 3]],
            ['F'] = [[4, 0, 0, 0, 0, 6], [0, 3, 3, 3]],
            ['H'] = [[0, 0, 0, 6], [4, 0, 4, 6], [0, 3, 4, 3]],
            ['J'] = [[1.5f, 0, 4, 0], [3, 0, 3, 5, 2, 6, 1, 6, 0, 5]],
            ['K'] = [[0, 0, 0, 6], [4, 0, 0, 3.5f], [1.3f, 2.7f, 4, 6]],
            ['L'] = [[0, 0, 0, 6, 4, 6]],
            ['M'] = [[0, 6, 0, 0, 2, 3.5f, 4, 0, 4, 6]],
            ['N'] = [[0, 6, 0, 0, 4, 6, 4, 0]],
            ['P'] = [[0, 6, 0, 0, 3, 0, 4, 1, 4, 2, 3, 3, 0, 3]],
            ['R'] = [[0, 6, 0, 0, 3, 0, 4, 1, 4, 2, 3, 3, 0, 3], [2, 3, 4, 6]],
            ['T'] = [[0, 0, 4, 0], [2, 0, 2, 6]],
            ['U'] = [[0, 0, 0, 5, 1, 6, 3, 6, 4, 5, 4, 0]],
            ['V'] = [[0, 0, 2, 6, 4, 0]],
            ['W'] = [[0, 0, 1, 6, 2, 2.5f, 3, 6, 4, 0]],
            ['X'] = [[0, 0, 4, 6], [4, 0, 0, 6]],
            ['Y'] = [[0, 0, 2, 3, 4, 0], [2, 3, 2, 6]],
            ['3'] = [[0, 0, 4, 0, 2, 2.5f, 3, 2.5f, 4, 3.5f, 4, 5, 3, 6, 1, 6, 0, 5]],
            ['4'] = [[3, 6, 3, 0, 0, 4, 4, 4]],
            ['6'] = [[3.5f, 0, 2, 0, 0, 2.5f, 0, 5, 1, 6, 3, 6, 4, 5, 4, 4, 3, 3, 1, 3, 0, 4]],
            ['7'] = [[0, 0, 4, 0, 1.5f, 6]],
            ['9'] = [[4, 3, 1, 3, 0, 2, 0, 1, 1, 0, 3, 0, 4, 1, 4, 3.5f, 2, 6]],
        };

        // 획의 반폭(글꼴 칸 단위). 칸 하나가 5~6px 이라 획이 4~5px 로 선다.
        private const double Stroke = 0.42;

        private readonly record struct Glyph(float[][] Lines, double Cx, double Cy, double Cos, double Sin, double Scale, byte R, byte G, byte B);

        public static byte[] Render(string text)
        {
            var rnd = new Random(RandomNumberGenerator.GetInt32(int.MaxValue));
            var pixels = new byte[Width * Height * 3];

            for (var i = 0; i < pixels.Length; i += 3)
            {
                var shade = (byte)(236 + rnd.Next(16));
                pixels[i] = shade;
                pixels[i + 1] = shade;
                pixels[i + 2] = (byte)Math.Min(255, shade + 4);
            }

            var glyphs = new Glyph[text.Length];
            var step = (Width - 20.0) / text.Length;
            for (var i = 0; i < text.Length; i++)
            {
                var angle = (rnd.NextDouble() - 0.5) * 0.6;
                glyphs[i] = new Glyph(
                    Font[text[i]],
                    10 + step * (i + 0.5) + (rnd.NextDouble() - 0.5) * 5,
                    Height / 2.0 + (rnd.NextDouble() - 0.5) * 8,
                    Math.Cos(angle), Math.Sin(angle),
                    5.2 + rnd.NextDouble() * 1.0,
                    (byte)(20 + rnd.Next(70)), (byte)(20 + rnd.Next(60)), (byte)(40 + rnd.Next(80)));
            }

            var ampX = 1.5 + rnd.NextDouble() * 1.5;
            var periodX = 35 + rnd.NextDouble() * 20;
            var phaseX = rnd.NextDouble() * Math.PI * 2;
            var ampY = 1.0 + rnd.NextDouble() * 1.5;
            var periodY = 50 + rnd.NextDouble() * 40;
            var phaseY = rnd.NextDouble() * Math.PI * 2;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    // 2×2 로 나눠 찍어 가장자리를 부드럽게 한다.
                    int hits = 0, gi = -1;
                    for (var sy = 0; sy < 2; sy++)
                    {
                        for (var sx = 0; sx < 2; sx++)
                        {
                            var px = x + 0.25 + sx * 0.5;
                            var py = y + 0.25 + sy * 0.5;
                            var wx = px + ampX * Math.Sin(py / periodX * Math.PI * 2 + phaseX);
                            var wy = py + ampY * Math.Sin(px / periodY * Math.PI * 2 + phaseY);
                            var g = InkAt(glyphs, wx, wy);
                            if (g >= 0) { hits++; gi = g; }
                        }
                    }

                    if (hits > 0)
                    {
                        Blend(pixels, x, y, glyphs[gi].R, glyphs[gi].G, glyphs[gi].B, hits / 4.0);
                    }
                }
            }

            // 글자를 가로지르는 가는 곡선 둘. 글자와 같은 계열의 색이라 색만으로 걷어 낼 수 없다.
            for (var n = 0; n < 2; n++)
            {
                var y0 = 18 + rnd.NextDouble() * 24;
                var amp = 5 + rnd.NextDouble() * 8;
                var period = 70 + rnd.NextDouble() * 80;
                var phase = rnd.NextDouble() * Math.PI * 2;
                var c = glyphs[rnd.Next(glyphs.Length)];
                for (var x = 0; x < Width; x++)
                {
                    var yy = (int)Math.Round(y0 + amp * Math.Sin(x / period * Math.PI * 2 + phase));
                    if (yy >= 0 && yy < Height) Blend(pixels, x, yy, c.R, c.G, c.B, 0.9);
                }
            }

            for (var n = 0; n < Width * Height / 25; n++)
            {
                var c = (byte)rnd.Next(40, 200);
                Blend(pixels, rnd.Next(Width), rnd.Next(Height), c, c, c, 0.7);
            }

            return EncodePng(pixels);
        }

        private static int InkAt(Glyph[] glyphs, double x, double y)
        {
            for (var i = 0; i < glyphs.Length; i++)
            {
                var g = glyphs[i];
                var dx = x - g.Cx;
                var dy = y - g.Cy;
                if (Math.Abs(dx) > 30 || Math.Abs(dy) > 30) continue;

                // 글자 가운데(2,3)를 원점으로 되돌려 글꼴 칸 좌표로 바꾼다.
                var u = (dx * g.Cos + dy * g.Sin) / g.Scale + 2;
                var v = (-dx * g.Sin + dy * g.Cos) / g.Scale + 3;
                if (u < -1 || v < -1 || u > 5 || v > 7) continue;

                foreach (var line in g.Lines)
                {
                    for (var k = 0; k + 3 < line.Length; k += 2)
                    {
                        if (DistanceToSegment(u, v, line[k], line[k + 1], line[k + 2], line[k + 3]) <= Stroke)
                        {
                            return i;
                        }
                    }
                }
            }
            return -1;
        }

        private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
        {
            var vx = bx - ax;
            var vy = by - ay;
            var len2 = vx * vx + vy * vy;
            var t = len2 == 0 ? 0 : Math.Clamp(((px - ax) * vx + (py - ay) * vy) / len2, 0, 1);
            var cx = ax + t * vx - px;
            var cy = ay + t * vy - py;
            return Math.Sqrt(cx * cx + cy * cy);
        }

        private static void Blend(byte[] pixels, int x, int y, byte r, byte g, byte b, double a)
        {
            var i = (y * Width + x) * 3;
            pixels[i] = (byte)(pixels[i] * (1 - a) + r * a);
            pixels[i + 1] = (byte)(pixels[i + 1] * (1 - a) + g * a);
            pixels[i + 2] = (byte)(pixels[i + 2] * (1 - a) + b * a);
        }

        private static byte[] EncodePng(byte[] rgb)
        {
            using var raw = new MemoryStream();
            using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (var y = 0; y < Height; y++)
                {
                    z.WriteByte(0);
                    z.Write(rgb, y * Width * 3, Width * 3);
                }
            }

            using var png = new MemoryStream();
            png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

            var ihdr = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(ihdr, Width);
            BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), Height);
            ihdr[8] = 8;  // 8비트
            ihdr[9] = 2;  // RGB
            WriteChunk(png, "IHDR", ihdr);
            WriteChunk(png, "IDAT", raw.ToArray());
            WriteChunk(png, "IEND", []);
            return png.ToArray();
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            Span<byte> len = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
            s.Write(len);

            var typed = new byte[4 + data.Length];
            Encoding.ASCII.GetBytes(type, typed);
            data.CopyTo(typed, 4);
            s.Write(typed);

            Span<byte> crc = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typed));
            s.Write(crc);
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        private static uint Crc32(byte[] data)
        {
            var c = 0xFFFFFFFFu;
            foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
    }
}
