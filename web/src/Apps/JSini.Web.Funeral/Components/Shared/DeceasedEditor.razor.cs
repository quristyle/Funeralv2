using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Shared;

public partial class DeceasedEditor
{
    [Inject] private FuneralApi Api { get; set; } = default!;
    [Inject] private FileUploadClient Upload { get; set; } = default!;
    [Inject] private CommonCodeClient Codes { get; set; } = default!;

    private sealed record Option(string Code, string Name);

    private static readonly Option[] Genders =
    [
        new("MALE", "남"),
        new("FEMALE", "여"),
    ];

    [Parameter] public bool Visible { get; set; }

    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>고칠 고인. <c>null</c> 이면 새로 등록한다.</summary>
    [Parameter] public string? DeceasedId { get; set; }

    /// <summary>등록할 때 고를 수 있는 빈 호실.</summary>
    [Parameter] public IReadOnlyList<Room> Rooms { get; set; } = [];

    /// <summary>저장했다. 목록을 다시 읽는 것은 부모가 한다.</summary>
    [Parameter] public EventCallback OnSaved { get; set; }

    private DeceasedDetail? _form;
    private string? _loadedFor;

    /// <summary>시설 유형 고르개. 호실과 같은 공통코드를 쓴다(원본도 그랬다).</summary>
    private IReadOnlyList<CommonCode> _roomTypes = [];

    private bool IsNew => string.IsNullOrEmpty(DeceasedId);

    /// <summary>영정사진 올리는 중인가. 두 번 고르는 것을 막는다.</summary>
    private bool _uploading;

    /// <summary>
    /// 지금 걸려 있는 영정사진 주소.
    ///
    /// <para>
    /// 아이디가 있으면 그것으로 만든다 — DB 에 든 <c>MemorialPhotoUrl</c> 은
    /// <c>/api/file/...</c> 라 포털에서 바로 못 쓴다(<c>FileDownload</c> 가 옮긴다).
    /// </para>
    /// </summary>
    private string? PortraitUrl =>
        _form?.MemorialPhotoFileId is { Length: > 0 } id
            ? FileDownload.UrlFor(id)
            : FileDownload.RelayUrl(_form?.MemorialPhotoUrl);

    // 계약자·담당은 없을 수 있다. 폼이 늘 쓸 수 있게 빈 것을 채워 둔다 —
    // null 인 채로 두면 칸마다 null 검사를 적게 되고, 한 곳은 반드시 빠진다.
    private DeceasedContractor Contractor => _form!.Contractor ??= new DeceasedContractor();
    private DeceasedManager Manager => _form!.Manager ??= new DeceasedManager();

    protected override async Task OnParametersSetAsync()
    {
        if (!Visible)
        {
            _loadedFor = null;
            return;
        }

        // 같은 대상을 다시 열면 그대로 둔다 — 다시 읽으면 쓰던 값이 날아간다.
        var key = DeceasedId ?? "(new)";
        if (string.Equals(_loadedFor, key, StringComparison.Ordinal))
        {
            return;
        }

        _loadedFor = key;
        await LoadAsync2();
    }

    private Task LoadAsync2() => LoadAsync(async () =>
    {
        // 시설 유형 고르개가 쓴다. 못 읽어도 폼은 뜬다 — 그때는 코드값이
        // 그대로 보이고, 고르지 못할 뿐이다.
        if (_roomTypes.Count == 0)
        {
            try
            {
                _roomTypes = await Codes.GetAsync("ROOM_TYPE");
            }
            catch (ApiException)
            {
                _roomTypes = [];
            }
        }

        if (IsNew)
        {
            _form = new DeceasedDetail();
            return 1;
        }

        _form = await Api.GetDeceasedDetailAsync(DeceasedId!) ?? new DeceasedDetail { Id = DeceasedId! };
        return 1;
    }, string.Empty, "고인 자료를 읽지 못했습니다");

    private void AddMourner() => _form!.Mourners.Add(new DeceasedMourner
    {
        SortOrder = _form.Mourners.Count,

        // 첫 상주는 대표로 둔다. 대표가 하나도 없으면 현황판의 「상주」 칸이
        // 빈다 — 등록하고 나서야 알게 되는 종류의 빠짐이다.
        IsChief = _form.Mourners.Count == 0,
    });

    /// <summary>
    /// 대표 상주는 한 명이다. 새로 고르면 앞의 것을 푼다 —
    /// 둘이 되면 현황판이 어느 쪽을 보여 줄지 서버가 임의로 정하게 된다.
    /// </summary>
    private void SetChief(DeceasedMourner mourner, bool value)
    {
        if (value)
        {
            foreach (var other in _form!.Mourners)
            {
                other.IsChief = false;
            }
        }

        mourner.IsChief = value;
    }

    private async Task SaveAsync()
    {
        if (_form is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_form.Name))
        {
            Say("고인명을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        if (_form.Mourners.Any(m => string.IsNullOrWhiteSpace(m.Name)))
        {
            Say("이름이 비어 있는 상주가 있습니다.", NoticeTone.Warning);
            return;
        }

        // 상주 순서를 화면에 보이는 대로 매긴다. 안 매기면 서버가 넣은 순서를
        // 쓰는데, 그것이 화면 순서와 달라 다시 열었을 때 뒤바뀌어 보인다.
        for (var i = 0; i < _form.Mourners.Count; i++)
        {
            _form.Mourners[i].SortOrder = i;
        }

        var saved = await RunAsync(
            () => Api.SaveDeceasedDetailAsync(IsNew ? null : DeceasedId, _form),
            IsNew ? "등록했습니다." : "저장했습니다.",
            IsNew ? "등록하지 못했습니다" : "저장하지 못했습니다");

        if (saved)
        {
            await OnVisibleChanged(false);
            await OnSaved.InvokeAsync();
        }
    }

    // ── 시설 사용 ───────────────────────────────────────────
    //
    // 안치실·염습실처럼 빈소와 따로 쓰는 자리를 적는다. 이 값이 정산으로 간다.

    private void AddFacility()
    {
        // 단가 기본값은 원본과 같다. 시설마다 다르면 그 자리에서 고친다.
        _form?.Facilities.Add(new DeceasedFacility { UnitPrice = 50000 });
    }

    /// <summary>
    /// 시작·종료를 넣으면 <b>사용 시간과 금액이 저절로 채워진다.</b>
    /// 손으로도 고칠 수 있다 — 실제로 쓴 시간이 시계와 다른 경우가 있다.
    /// </summary>
    private static void SetFacilityTime(DeceasedFacility facility, DateTime? start, DateTime? end)
    {
        if (start is not null)
        {
            facility.StartTime = start;
        }

        if (end is not null)
        {
            facility.EndTime = end;
        }

        if (facility.StartTime is { } from && facility.EndTime is { } to && to > from)
        {
            facility.UseHours = Math.Round((to - from).TotalHours, 1);
            facility.TotalPrice = Math.Round(facility.UnitPrice * (decimal)facility.UseHours);
        }
    }

    private static void SetFacilityHours(DeceasedFacility facility, double hours)
    {
        facility.UseHours = hours;
        facility.TotalPrice = Math.Round(facility.UnitPrice * (decimal)hours);
    }

    private static void SetFacilityPrice(DeceasedFacility facility, decimal price)
    {
        facility.UnitPrice = price;
        facility.TotalPrice = Math.Round(price * (decimal)facility.UseHours);
    }

    // ── 영정사진 ────────────────────────────────────────────
    //
    // 한 장이라 파일 그룹이 아니라 **파일 하나**로 올린다(`file/upload`).
    // 고르는 즉시 올라가고, 폼은 그 아이디와 주소만 들고 있다가 저장한다.
    // 저장을 안 누르면 파일만 남고 고인에는 안 붙는다 — 사진 그룹과 같은 사정이다.

    private async Task UploadPortraitAsync(InputFileChangeEventArgs e)
    {
        if (_form is null) return;

        var file = e.File;

        // 휴대폰으로 찍은 원본이 10MB 를 넘는 일이 흔하다. 영정사진은 한 장이라
        // 넉넉히 잡아도 부담이 없다.
        const long maxBytes = 20L * 1024 * 1024;

        if (file.Size > maxBytes)
        {
            Say("사진이 20MB 를 넘습니다.", NoticeTone.Warning);
            return;
        }

        _uploading = true;

        try
        {
            // 사진 한 장이라 통째로 들고 있어도 된다.
            using var buffer = new MemoryStream();
            await using (var source = file.OpenReadStream(maxBytes))
            {
                await source.CopyToAsync(buffer);
            }

            buffer.Position = 0;

            // 업무 구분은 Vue 가 쓰던 값 그대로다. FileServer 가 저장 폴더
            // 이름으로 쓰므로 바꾸면 옛 사진과 새 사진이 갈라진다.
            var uploaded = await Upload.UploadAsync(
                buffer, file.Name, file.ContentType, bizType: "DECEASED");

            if (uploaded is null)
            {
                Say("사진을 올리지 못했습니다.", NoticeTone.Error);
                return;
            }

            _form.MemorialPhotoFileId = uploaded.FileId ?? uploaded.Id;
            _form.MemorialPhotoUrl = uploaded.BestUrl;

            Say("사진을 올렸습니다. 저장을 눌러야 고인에 붙습니다.");
        }
        catch (Exception ex)
        {
            Say($"사진을 올리지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _uploading = false;
        }
    }

    /// <summary>
    /// 영정사진을 뺀다. <b>파일은 지우지 않는다</b> — 연결만 끊는다.
    /// 지우면 옛 주소를 들고 있는 화면(플레이어 캐시)이 깨진다.
    /// </summary>
    private void ClearPortrait()
    {
        if (_form is null) return;

        _form.MemorialPhotoFileId = null;
        _form.MemorialPhotoUrl = null;
    }

    private async Task OnVisibleChanged(bool visible)
    {
        Visible = visible;

        if (!visible)
        {
            _form = null;
            _loadedFor = null;
        }

        await VisibleChanged.InvokeAsync(visible);
    }
}
