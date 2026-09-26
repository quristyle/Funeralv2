using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class DeceasedStatus
{
    [Inject] private FuneralApi Api { get; set; } = default!;
    [Inject] private FileUploadClient Files { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        _status is null ? SchSummary.Any : StatusText(_status));

    /// <summary>출상 완료 상태. 서버가 쓰는 값이다.</summary>
    private const string Departed = "DEPARTED";

    /// <summary>거를 수 있는 상태. 화면 문구는 <see cref="StatusText" /> 가 만든다.</summary>
    private static readonly string[] Statuses = ["FUNERAL_IN_PROGRESS", Departed];

    private string? _keyword;
    private string? _status;
    private IReadOnlyList<Deceased> _rows = [];

    // ── 영정 사진 (D5) ──────────────────────────────────────

    private bool _photoOpen;
    private bool _photoSaving;

    /// <summary>지금 창을 연 고인. 제목에 이름을 띄운다.</summary>
    private Deceased? _photoOf;

    /// <summary>
    /// 고인 상세. <b>목록 자료로 저장하면 안 되기 때문에</b> 읽는다 —
    /// 상세에만 있는 칸(상주·계약자·시설)이 통째로 빈 값이 된다.
    /// </summary>
    private DeceasedDetail? _detail;

    private FilePicker? _photoPicker;
    private PickedFile? _photoPicked;

    /// <summary>지금 걸린 사진. 서버와 같은 순서로 고른다 — 보정본이 먼저다.</summary>
    private string? CurrentPhoto =>
        _detail is null ? null
        : string.IsNullOrWhiteSpace(_detail.MemorialEditedPhotoUrl)
            ? _detail.MemorialPhotoUrl
            : _detail.MemorialEditedPhotoUrl;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 이 API 는 조건을 사전으로 받는다 (백엔드가 검색 칸을 계속 늘려 와서
        // 매개변수를 하나씩 붙이는 방식을 그만뒀다). 값이 비면 넣지 않는다 —
        // 빈 문자열을 보내면 서버가 "빈 이름" 으로 걸러 0건이 된다.
        var query = new Dictionary<string, string?>();
        // 서버가 받는 이름은 `name` 이다. `keyword` 로 보내면 조용히 무시되고
        // 전체가 나온다 — 오류가 아니라서 화면만 봐서는 알 수 없다.
        if (!string.IsNullOrWhiteSpace(_keyword)) query["name"] = _keyword;
        if (!string.IsNullOrWhiteSpace(_status)) query["status"] = _status;

        _rows = await Api.GetDeceasedListAsync(query);
        return _rows.Count;
    }, "조건에 맞는 고인 자료가 없습니다.", "고인 목록을 읽지 못했습니다");

    private async Task DepartAsync(Deceased d)
    {
        if (await RunAsync(() => Api.DepartDeceasedAsync(d.Id),
                $"{d.Name} 출상 처리했습니다.", "출상 처리하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    private async Task CancelAsync(Deceased d)
    {
        if (await RunAsync(() => Api.CancelDeceasedDepartureAsync(d.Id),
                $"{d.Name} 출상을 취소했습니다.", "출상을 취소하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    // ── 영정 사진 (D5) ──────────────────────────────────────

    /// <summary>창을 열면서 상세를 읽는다. 지금 무엇이 걸려 있는지 보여 줘야 한다.</summary>
    private async Task StartPhotoAsync(Deceased d)
    {
        _photoOf = d;
        _detail = null;
        _photoPicked = null;
        _photoOpen = true;

        await LoadOneAsync(
            () => Api.GetDeceasedDetailAsync(d.Id),
            detail => _detail = detail,
            "고인 자료를 찾지 못했습니다.",
            "고인 자료를 읽지 못했습니다");
    }

    /// <summary>
    /// 사진을 올려 고인에게 매단다. <b>두 단계다</b> — 머리말 참고.
    /// </summary>
    private async Task SavePhotoAsync()
    {
        if (_photoPicked is null || _detail is null || _photoOf is null || _photoSaving)
        {
            return;
        }

        _photoSaving = true;
        try
        {
            UploadedFile? uploaded = null;

            var sent = await RunAsync(async () =>
            {
                await using var stream = _photoPicked.OpenRead();
                uploaded = await Files.UploadAsync(stream, _photoPicked.Name, _photoPicked.ContentType);
            }, string.Empty, "사진을 올리지 못했습니다");

            if (!sent || uploaded is null)
            {
                return;
            }

            // 읽어 둔 상세 **위에 얹어** 저장한다. 새 객체를 만들어 보내면
            // 상주·계약자·시설이 빈 값으로 덮인다.
            _detail.MemorialPhotoFileId = uploaded.FileId ?? uploaded.Id;
            _detail.MemorialPhotoUrl = uploaded.BestUrl;

            // 보정본을 비운다. 안 비우면 서버가 그쪽을 먼저 골라, 새로 올렸는데
            // 빈소 화면에는 옛 사진이 계속 나온다.
            _detail.MemorialEditedPhotoFileId = null;
            _detail.MemorialEditedPhotoUrl = null;

            var saved = await RunAsync(
                () => Api.SaveDeceasedDetailAsync(_photoOf.Id, _detail),
                string.Empty,
                "사진은 올라갔지만 고인 자료에 매달지 못했습니다");

            if (!saved)
            {
                return;
            }

            _photoOpen = false;
            if (_photoPicker is not null)
            {
                await _photoPicker.ClearAsync();
            }

            Say($"{_photoOf.Name} 영정 사진을 올렸습니다.");
        }
        finally
        {
            _photoSaving = false;
        }
    }

    private static string StatusText(string status) => status switch
    {
        Departed => "출상",
        "FUNERAL_IN_PROGRESS" => "진행 중",
        _ => status,
    };
}
