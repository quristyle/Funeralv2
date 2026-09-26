using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Shared;

public partial class MediaSourceList
{
    [Inject] private FuneralApi Api { get; set; } = default!;
    [Inject] private FileUploadClient Files { get; set; } = default!;

    /// <summary>이 화면이 다루는 자료 종류. 서버가 이 값으로 걸러 준다.</summary>
    [Parameter, EditorRequired] public string SourceType { get; set; } = string.Empty;

    /// <summary>편집 창과 엑셀 파일에 쓸 이름.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    /// <summary>미리보기 그림 칸을 보여 줄지. 영상만 참이다.</summary>
    [Parameter] public bool ShowThumbnail { get; set; }

    /// <summary>
    /// 재처리 단추들. <c>(종류, 이름)</c>.
    ///
    /// 종류는 <c>thumbnail</c> · <c>webm</c> · <c>audio</c> 셋이다.
    /// 파생물이 없는 자료(장식·배경)는 비워 둔다.
    /// </summary>
    [Parameter] public IReadOnlyList<(string Kind, string Label)> Retries { get; set; } = [];

    /// <summary>
    /// 파일 고르기 창의 <c>accept</c>. 화면마다 다르다 — 영상 자리에 mp3 를
    /// 고르면 목록에는 뜨는데 플레이어가 못 튼다.
    /// </summary>
    [Parameter] public string? Accept { get; set; }

    /// <summary>
    /// 한 개 최대 크기(바이트). 영상은 크고 장식 그림은 작다.
    /// 기본 300MB 는 영상 기준이다.
    /// </summary>
    [Parameter] public long MaxBytes { get; set; } = 300L * 1024 * 1024;

    private IReadOnlyList<MediaSource> _all = [];
    private string? _keyword;

    /// <summary>재생 창에 걸린 자료. 닫으면 비운다 — 안 비우면 소리가 계속 난다.</summary>
    private MediaSource? _preview;

    private bool _previewing;

    // ── 올리기 (D5) ─────────────────────────────────────────

    private FilePicker? _picker;
    private PickedFile? _picked;
    private string? _uploadName;
    private bool _uploading;

    private IReadOnlyList<MediaSource> Shown
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return _all;
            }

            var k = _keyword.Trim();
            return [.. _all.Where(m =>
                m.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                || (m.ShortName?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false))];
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetMediaSourcesAsync(SourceType);
        return _all.Count;
    }, "등록된 자료가 없습니다.", "목록을 읽지 못했습니다");

    /// <summary>
    /// 정보를 저장한다. <b>등록은 없다</b> — 파일을 올릴 길이 아직 없어서다.
    ///
    /// 종류를 다시 실어 보낸다. 안 실으면 서버가 빈 값으로 덮어써서 그 자료가
    /// 어느 화면에도 안 나오게 된다.
    /// </summary>
    private Task SaveAsync((MediaSource Item, bool IsNew) e)
    {
        e.Item.SourceType = SourceType;
        return Api.UpdateMediaSourceAsync(e.Item.Id, e.Item);
    }

    private Task DeleteAsync(MediaSource m) => Api.DeleteMediaSourceAsync(m.Id);

    // ── 올리기 (D5) ─────────────────────────────────────────

    /// <summary>
    /// 파일을 고르면 이름 칸을 파일 이름으로 미리 채운다.
    ///
    /// 확장자는 뗀다 — 목록에 보일 이름이지 파일 이름이 아니다.
    /// 사람이 고칠 수 있게 두되 <b>비워 두고 시작하지 않는다.</b> 비워 두면
    /// 이름을 안 적고 올려 목록이 「(이름 없음)」으로 채워진다.
    /// </summary>
    private void OnPickedAsync(IReadOnlyList<PickedFile> files)
    {
        _picked = files.Count > 0 ? files[0] : null;

        if (_picked is not null && string.IsNullOrWhiteSpace(_uploadName))
        {
            _uploadName = Path.GetFileNameWithoutExtension(_picked.Name);
        }
    }

    /// <summary>
    /// 파일을 올리고 자료로 매단다. <b>두 단계다</b> — 머리말 참고.
    /// </summary>
    private async Task UploadAsync()
    {
        if (_picked is null || _uploading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_uploadName))
        {
            Say("목록에 보일 이름을 적어 주십시오.", NoticeTone.Warning);
            return;
        }

        _uploading = true;
        try
        {
            UploadedFile? uploaded = null;

            // ① 파일을 FileServer 로. 실패하면 여기서 끝난다 — 자료를 만들지 않는다.
            var sent = await RunAsync(async () =>
            {
                await using var stream = _picked.OpenRead();
                uploaded = await Files.UploadAsync(stream, _picked.Name, _picked.ContentType);
            }, string.Empty, "파일을 올리지 못했습니다");

            if (!sent || uploaded is null)
            {
                return;
            }

            // ② 자료로 매단다. 여기서 실패하면 **파일은 이미 올라가 있다.**
            //    그 사실을 말해 준다 — 「등록 실패」로만 알리면 같은 파일을
            //    몇 번이고 다시 올려 주인 없는 파일만 쌓인다.
            var linked = await RunAsync(
                () => Api.CreateMediaSourceAsync(new MediaSource
                {
                    Name = _uploadName!.Trim(),
                    SourceType = SourceType,
                    Url = uploaded.BestUrl ?? string.Empty,
                    OriginalFileId = uploaded.FileId ?? uploaded.Id,
                    ThumbnailUrl = uploaded.ThumbnailUrl,
                    ThumbnailFileId = uploaded.ThumbnailFileId,
                    FileSize = uploaded.FileSize ?? _picked.Size,
                }),
                string.Empty,
                "파일은 올라갔지만 목록에 매달지 못했습니다");

            if (!linked)
            {
                return;
            }

            _picked = null;
            _uploadName = null;
            if (_picker is not null)
            {
                await _picker.ClearAsync();
            }

            // **다시 읽는 것이 먼저다.** ReloadAsync 가 시작할 때 안내 줄을
            // 비우므로, 성공 문구를 먼저 적으면 그 자리에서 지워진다.
            await ReloadAsync();
            Say($"{Title} 을(를) 올렸습니다. 변환은 잠시 뒤 끝납니다.");
        }
        finally
        {
            _uploading = false;
        }
    }

    // ── 재생 · 변환 상태 ────────────────────────────────────

    private void Preview(MediaSource m)
    {
        _preview = m;
        _previewing = true;
    }

    /// <summary>
    /// 무엇을 틀 것인가. <b>변환본이 있으면 그것을 먼저 준다</b> —
    /// 원본이 브라우저가 못 여는 형식이라 변환본이 있는 것이다.
    /// </summary>
    private static string PlayUrl(MediaSource m) =>
        m.HasWebm && !string.IsNullOrWhiteSpace(m.WebmUrl) ? m.WebmUrl
        : !string.IsNullOrWhiteSpace(m.AacUrl) ? m.AacUrl
        : m.Url;

    private static string StatusLabel(string? status) => status switch
    {
        "COMPLETED" => "완료",
        "PROCESSING" => "변환 중",
        "READY" => "준비",
        "FAILED" => "실패",
        null or "" => "-",
        _ => status,
    };

    private static string StatusClass(string? status) => status switch
    {
        "COMPLETED" => "jsini-badge--on",
        "FAILED" => "jsini-badge--off",
        _ => string.Empty,
    };

    /// <summary>
    /// 마우스를 올렸을 때 나오는 자세한 사정.
    ///
    /// <para>
    /// 실패한 것에는 <b>실행 명령까지</b> 담는다. ffmpeg 이 무엇을 하다 죽었는지는
    /// 그 줄을 봐야 알 수 있고, 그것을 못 보면 재처리 단추를 눌러 보는 것 말고
    /// 할 수 있는 일이 없다.
    /// </para>
    /// </summary>
    private static string? StatusDetail(MediaSource m)
    {
        if (!string.Equals(m.Status, "FAILED", StringComparison.Ordinal))
        {
            return m.ConversionCompletedAt is { } done ? $"{done:yyyy-MM-dd HH:mm} 완료" : null;
        }

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(m.ErrorMessage))
        {
            lines.Add(m.ErrorMessage);
        }

        if (!string.IsNullOrWhiteSpace(m.ConversionCommand))
        {
            lines.Add($"명령: {m.ConversionCommand}");
        }

        if (m.ConversionStartedAt is { } started)
        {
            lines.Add($"시작: {started:yyyy-MM-dd HH:mm}");
        }

        return lines.Count == 0 ? "변환에 실패했습니다." : string.Join('\n', lines);
    }

    /// <summary>바이트를 사람이 읽는 단위로. 1MB 미만은 KB 로 둔다.</summary>
    private static string FormatSize(long? bytes)
    {
        if (bytes is null or 0)
        {
            return "-";
        }

        var mb = bytes.Value / 1024d / 1024d;
        return mb >= 1 ? $"{mb:0.0} MB" : $"{Math.Round(bytes.Value / 1024d)} KB";
    }

    /// <summary>
    /// 변환 재처리.
    ///
    /// 성공하면 목록을 다시 읽는다 — 변환은 비동기라 지금 당장 바뀌지는 않지만,
    /// 무언가 반응이 있어야 두 번 누르지 않는다.
    /// </summary>
    private async Task RetryAsync(string id, string kind)
    {
        var run = kind switch
        {
            "thumbnail" => Api.RetryThumbnailAsync(id),
            "webm" => Api.RetryWebmAsync(id),
            _ => Api.RetryAudioAsync(id),
        };

        if (await RunAsync(() => run, "재처리를 걸었습니다. 잠시 뒤 반영됩니다.", "재처리를 걸지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
