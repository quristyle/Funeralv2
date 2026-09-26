using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class I18nList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_category),
        _keyword,
        SchSummary.On(_onlyMissing, "한쪽만 있는 것"));

    /// <summary>
    /// 한 키의 두 언어. 서버는 언어별로 한 줄씩 주지만 <b>고칠 때는 짝</b>이다.
    ///
    /// <para>
    /// 한쪽만 고치면 언어를 바꿨을 때 옛 문구가 남는다. 그래서 화면에서 묶는다.
    /// </para>
    ///
    /// <para>
    /// <b>record 가 아니라 고칠 수 있는 class 다.</b> 이것이 표의 편집 모델이라
    /// DevExpress 가 매개변수 없는 생성자로 만들고 칸마다 값을 넣는다 —
    /// 위치 매개변수 record 는 둘 다 안 된다.
    /// </para>
    /// </summary>
    private sealed class Pair
    {
        public string Key { get; set; } = string.Empty;
        public string? Category { get; set; }

        /// <summary>편집 폼이 묶는 값. 표에 보여 주는 것도 이 값이다.</summary>
        public string? KoValue { get; set; }

        /// <inheritdoc cref="KoValue"/>
        public string? EnValue { get; set; }
    }

    private IReadOnlyList<I18nResourceDto> _all = [];
    private IReadOnlyList<Pair> _pairs = [];
    private IReadOnlyList<string> _categories = [];

    private string? _keyword;
    private string? _category;
    private bool _onlyMissing;

    /// <summary>편집 창이 등록으로 열렸는가. 키를 잠글지 정한다.</summary>
    private bool _isNew;

    private IReadOnlyList<Pair> Shown
    {
        get
        {
            IEnumerable<Pair> rows = _pairs;

            if (!string.IsNullOrWhiteSpace(_category))
            {
                rows = rows.Where(p => string.Equals(p.Category, _category, StringComparison.OrdinalIgnoreCase));
            }

            if (_onlyMissing)
            {
                rows = rows.Where(p => string.IsNullOrWhiteSpace(p.KoValue) || string.IsNullOrWhiteSpace(p.EnValue));
            }

            if (!string.IsNullOrWhiteSpace(_keyword))
            {
                var k = _keyword.Trim();
                rows = rows.Where(p =>
                    p.Key.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || (p.KoValue?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (p.EnValue?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return [.. rows];
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 언어별이 아니라 전체를 한 번에 받는다. 키로 묶어야 하기 때문이다.
        _all = await Api.GetAllI18nAsync();

        _pairs =
        [
            .. _all
                .GroupBy(r => r.Key, StringComparer.Ordinal)
                .Select(g => new Pair
                {
                    Key = g.Key,
                    Category = g.Select(r => r.Category).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                    KoValue = Locale(g, "ko")?.Value,
                    EnValue = Locale(g, "en")?.Value,
                })
                .OrderBy(p => p.Key, StringComparer.Ordinal)
        ];

        _categories =
        [
            .. _all
                .Select(r => r.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)!
        ];

        return _pairs.Count;
    }, "등록된 자원이 없습니다.", "다국어 자원을 읽지 못했습니다");

    private Task ResetAsync()
    {
        _category = null;
        _keyword = null;
        _onlyMissing = false;

        return ReloadAsync();
    }

    /// <summary>고르고 있던 분류를 새 자원에 물려 준다. 이어서 여러 건 넣는 일이 흔하다.</summary>
    private void FillNew(Pair row) => row.Category = _category;

    /// <summary>
    /// 편집 창이 열릴 때. <b>수정으로 열 때도 불린다</b> — 키를 잠그는 것이
    /// 그 구분에 걸려 있어서, 등록 때만 불리는 <c>OnNew</c> 로는 앞선 등록의
    /// 상태가 그대로 남는다.
    /// </summary>
    private void OnEditOpen(Pair row, bool isNew) => _isNew = isNew;

    /// <summary>
    /// 두 언어를 함께 저장한다.
    ///
    /// <para>
    /// 언어마다 줄이 따로라 최대 두 번 부른다 — 없던 언어는 등록, 있던 언어는
    /// 수정, 비운 언어는 삭제다. <b>「비웠다」와 「원래 없었다」를 가르는 것</b>이
    /// 여기서 중요하다.
    /// </para>
    ///
    /// <para>
    /// 서버 줄(식별자)은 편집 모델에 없다. <b>일부러 넣지 않았다</b> — 편집
    /// 모델은 DevExpress 가 만든 사본이라 무엇이 옮겨 오는지에 기대면 조용히
    /// 어긋나고, 그때 나는 사고가 「수정한 것이 새 줄로 하나 더 생긴다」다.
    /// 대신 지금 읽어 둔 목록에서 <b>키로 찾는다.</b>
    /// </para>
    /// </summary>
    private async Task SaveAsync((Pair Item, bool IsNew) e)
    {
        var key = e.Item.Key?.Trim();

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ApiException("키를 넣으십시오.");
        }

        if (string.IsNullOrWhiteSpace(e.Item.KoValue) && string.IsNullOrWhiteSpace(e.Item.EnValue))
        {
            throw new ApiException("적어도 한 언어는 값을 넣으십시오.");
        }

        if (e.IsNew && _pairs.Any(p => string.Equals(p.Key, key, StringComparison.Ordinal)))
        {
            throw new ApiException($"「{key}」 는 이미 있습니다. 그 줄을 고치십시오.");
        }

        // 수정인데 원본을 못 찾으면 **멈춘다.** 그대로 두면 등록으로 흘러가
        // 같은 키가 두 벌 생기고, 어느 쪽이 보이는지는 순서가 정한다.
        var rows = Rows(key);

        if (!e.IsNew && rows.Count == 0)
        {
            throw new ApiException("고칠 자원을 찾지 못했습니다. 다시 읽고 시도하십시오.");
        }

        await SaveOneAsync(key, "ko", e.Item.KoValue, e.Item.Category, Locale(rows, "ko"));
        await SaveOneAsync(key, "en", e.Item.EnValue, e.Item.Category, Locale(rows, "en"));
    }

    private Task SaveOneAsync(string key, string locale, string? value, string? category, I18nResourceDto? existing)
    {
        var body = new I18nResourceDto
        {
            Key = key,
            Locale = locale,
            Value = value?.Trim() ?? string.Empty,
            Category = category,
        };

        if (existing is null)
        {
            // 원래 없었고 지금도 비었으면 만들 이유가 없다.
            return string.IsNullOrWhiteSpace(value)
                ? Task.CompletedTask
                : Api.CreateI18nAsync(body);
        }

        // 있던 것을 비웠으면 지운다. 빈 값으로 남기면 그 언어에서 빈 문구가
        // 보이는데, 키가 보이는 것보다 알아채기 어렵다.
        return string.IsNullOrWhiteSpace(value)
            ? Api.DeleteI18nAsync(existing.Id)
            : Api.UpdateI18nAsync(existing.Id, body);
    }

    /// <summary>
    /// 삭제 확인 문구. <b>지운 뒤에 무슨 일이 나는지</b>까지 적는다 — 번역이
    /// 사라진 자리에는 빈칸이 아니라 키가 그대로 보인다. 그 사실을 모르면
    /// 사이드바에 <c>system.menu.title</c> 이 뜬 것을 별개의 사고로 신고한다.
    /// </summary>
    private string DeleteMessage(Pair row)
    {
        var langs = string.Join(" · ", Rows(row.Key).Select(r => Language(r.Locale)));
        var scope = string.IsNullOrEmpty(langs) ? string.Empty : $"\n{langs} 가 사라집니다.";

        return $"키 「{row.Key}」 의 번역을 지웁니다.{scope}"
             + "\n그 키를 쓰던 자리에는 키가 그대로 보입니다. 되돌릴 수 없습니다.";
    }

    private async Task DeleteAsync(Pair row)
    {
        foreach (var existing in Rows(row.Key))
        {
            await Api.DeleteI18nAsync(existing.Id);
        }
    }

    /// <summary>그 키의 서버 줄들. 언어마다 한 줄이다.</summary>
    private List<I18nResourceDto> Rows(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? []
            : [.. _all.Where(r => string.Equals(r.Key, key, StringComparison.Ordinal))];

    /// <summary>언어 코드가 <c>ko-KR</c> 처럼 지역까지 붙어 오므로 앞만 본다.</summary>
    private static I18nResourceDto? Locale(IEnumerable<I18nResourceDto> rows, string prefix) =>
        rows.FirstOrDefault(r => r.Locale.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string Language(string locale) =>
        locale.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? "한국어"
        : locale.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "영어"
        : locale;
}
