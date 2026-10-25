using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class GmailArchivePage : DataPage, IAsyncDisposable
{
    /// <summary>올리기 한도. <b>0 은 제한 없음이다</b> — Takeout 은 메일함 하나가 GB 단위이고
    /// 쪼갠 묶음도 수 GB 라, 숫자를 박으면 그 자리가 곧 막히는 자리가 된다. 조각으로 흘러가므로
    /// (고르개 → 셸의 임시 파일 → 게이트웨이 → 적재 폴더) 어느 자리에도 통째로 쌓이지 않는다.</summary>
    private const long MaxUploadBytes = 0;

    private const string UploadHint =
        "mbox 한 개. 크기 제한은 없지만 GB 단위는 서버의 적재 폴더에 직접 두는 쪽이 훨씬 빠르다.";

    /// <summary>적재가 도는 동안 되묻는 간격. 더 잦게 물으면 왕복만 늘고, 더 뜸하면 멎은 것처럼 보인다.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private static readonly SchOption[] Kinds =
    [
        new("api", "Gmail API (Workspace)"),
        new("imap", "IMAP (개인 Gmail)"),
    ];

    [Inject] private GmailArchiveClient Gmail { get; set; } = default!;

    [Inject] private RagLibraryClient Rag { get; set; } = default!;

    private IReadOnlyList<GmailAccountDto> _accounts = [];
    private IReadOnlyList<GmailMboxFileDto> _files = [];
    private IReadOnlyList<GmailStatDto> _stats = [];
    private IReadOnlyList<RagCategoryDto> _categories = [];

    /// <summary>고른 카테고리의 분류들. 나무지만 고르개에는 한 줄씩 「상위 › 하위」로 편다 —
    /// 깊이를 안 보이면 이름만 같은 분류가 여럿일 때 어느 것인지 알 수 없다.</summary>
    private IReadOnlyList<FolderOption> _folders = [];

    private GmailImportStatusDto? _status;
    private string? _accountId;
    private string? _categoryId;
    private long? _folderId;
    private int _maxMessages = 1000;

    private string? _rootHint;
    private string? _archiveHint;

    private bool _accountOpen;
    private AccountEdit _edit = new();

    private CancellationTokenSource? _poll;

    /// <summary>올리는 동안의 진행. 끝나면 <c>null</c> 로 돌린다.</summary>
    private UploadProgress? _up;

    /// <summary>올리는 중인가. 그동안 적재·문서 만들기 단추를 잠근다 — 같은 파일을 두 번 밀어 넣지 않게.</summary>
    private bool Uploading => _up is not null;

    /// <summary>화면을 다시 그리는 간격. 바이트가 올 때마다 그리면 500MB 에서 수천 번 그린다.</summary>
    private static readonly TimeSpan RepaintInterval = TimeSpan.FromMilliseconds(400);

    private DateTime _lastPaint = DateTime.MinValue;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            _accounts = await Gmail.AccountsAsync();
            _accountId ??= _accounts.FirstOrDefault()?.Id;

            _stats = await Gmail.StatsAsync();
            _status = await Gmail.ImportStatusAsync();

            // 카테고리를 못 읽어도 적재는 되어야 한다 — 고르개만 빈다.
            try
            {
                _categories = await Rag.CategoriesAsync();
            }
            catch (ApiException)
            {
                _categories = [];
            }

            // 적재 폴더가 안 잡혀 있으면 파일 목록 자체가 거절된다. 그때는 그 까닭을
            // 판 머리에 적어 둔다 — 빈 표로 두면 「파일을 아직 안 올렸나」로 읽힌다.
            try
            {
                _files = await Gmail.FilesAsync();
                _rootHint = null;
            }
            catch (ApiException ex)
            {
                _files = [];
                _rootHint = ex.Message;
            }

            StartPollIfRunning();
            return _files.Count;
        });
    }

    /// <summary>카테고리를 바꾸면 분류를 다시 읽는다. <b>고른 분류는 반드시 버린다</b> —
    /// 남겨 두면 다른 카테고리의 분류 번호가 그대로 올라가고, 서버가 거절하므로
    /// 「분류를 골랐는데 안 된다」가 된다.</summary>
    private async Task OnCategoryChangedAsync(string? categoryId)
    {
        _categoryId = categoryId;
        _folderId = null;
        _folders = [];
        _archiveHint = null;

        if (string.IsNullOrWhiteSpace(categoryId)) return;

        try
        {
            _folders = FolderOption.Flatten(await Rag.FoldersAsync(categoryId));
        }
        catch (ApiException ex)
        {
            // 분류를 못 읽어도 카테고리 바로 아래로는 올릴 수 있다.
            _folders = [];
            Say($"분류를 읽지 못했습니다 — {ex.Message}", NoticeTone.Warning);
        }
    }

    /// <summary>브라우저가 보낸 바이트가 포털에 쌓이는 동안. <b>DevExpress 는 이 구간이 끝나면
    /// 「Uploaded」로 바꾸는데 그때 일이 끝난 것이 아니다</b> — 뒤에 적재 폴더로 보내는 구간이 남는다.</summary>
    private Task OnReceivingAsync((long Received, long Total) p)
    {
        _up = (_up ?? new UploadProgress()).With(UploadPhase.Receiving, p.Received, p.Total);
        return RepaintAsync();
    }

    /// <summary>올리기. <b>어떤 예외도 밖으로 내보내지 않는다</b> — 여기서 던지면 회로가 끊겨
    /// 화면에 「연결이 끊겼습니다」 띠가 뜬다. RunAsync 는 ApiException 만 잡는데 이 길은
    /// HttpRequestException(413 따위)·IOException 으로도 실패한다.</summary>
    private async Task UploadAsync(IReadOnlyList<PickedFile> picked)
    {
        if (picked.Count == 0) return;

        var one = picked[0];
        _up = (_up ?? new UploadProgress()).With(UploadPhase.Sending, 0, one.Size);

        try
        {
            await using var stream = File.OpenRead(one.TempPath);

            await Gmail.UploadAsync(stream, one.Name, sent =>
            {
                _up = _up?.With(UploadPhase.Sending, sent, one.Size);
                _ = RepaintAsync();
            });

            _files = await Gmail.FilesAsync();
            _rootHint = null;
            Say($"{one.Name} 을(를) 올렸습니다.");
        }
        catch (Exception ex)
        {
            Say($"{one.Name} 을(를) 올리지 못했습니다 — {Reason(ex)}", NoticeTone.Error);
        }
        finally
        {
            _up = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>사람이 읽을 까닭. 상한에 걸린 것과 길이 끊긴 것은 할 일이 다르다.</summary>
    private static string Reason(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.RequestEntityTooLarge } =>
            "서버가 크기를 막았습니다. 게이트웨이와 AI 서버가 최신 판인지 확인해 주세요.",
        HttpRequestException { StatusCode: { } code } => $"서버가 {(int)code} 로 답했습니다.",
        HttpRequestException => "서버에 닿지 못했습니다.",
        IOException => "파일을 읽다 끊겼습니다.",
        _ => ex.Message,
    };

    /// <summary>진행을 그린다. 시간으로 조이지 않으면 조각마다 그려 회로가 막힌다.</summary>
    private async Task RepaintAsync()
    {
        var now = DateTime.UtcNow;
        var done = _up is null || _up.Sent >= _up.Total;
        if (!done && now - _lastPaint < RepaintInterval) return;

        _lastPaint = now;

        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // 보내는 쪽에서 불려 오는 길이라(HttpClient 의 읽기 스레드) 화면이 이미 사라졌을 수 있다.
            // 그림 한 번을 못 그린 것뿐이고, 올리는 일은 그대로 간다.
        }
    }

    private async Task ImportAsync(string file)
    {
        if (_accountId is not { Length: > 0 } account) return;

        await RunAsync(async () =>
        {
            await Gmail.StartImportAsync(account, file);
            _status = await Gmail.ImportStatusAsync();
            StartPollIfRunning();
        }, $"{file} 적재를 시작했습니다.");
    }



    private async Task ArchiveAsync()
    {
        if (_categoryId is not { Length: > 0 } category) return;

        await RunAsync(async () =>
        {
            var result = await Gmail.ArchiveAsync(category, _folderId, _maxMessages);
            _stats = await Gmail.StatsAsync();

            // 「남은 것이 없다」와 「카테고리가 없다」는 실패가 아니라 알림이다 — 판 머리에 적는다.
            _archiveHint = result?.Note;

            if (result is { Documents: > 0 })
            {
                var where = _folders.FirstOrDefault(f => f.Id == _folderId)?.Label ?? "카테고리 바로 아래";
                Say($"메일 {result.Messages:N0}통을 문서 {result.Documents}장으로 「{where}」에 올렸습니다. 색인은 뒤에서 만들어집니다.");
            }
            else
            {
                Say(result?.Note ?? "올릴 메일이 없습니다.", NoticeTone.Warning);
            }
        });
    }

    // ── 적재 되묻기 ────────────────────────────────────────

    private void StartPollIfRunning()
    {
        if (_status?.Running != true) return;
        if (_poll is not null) return;

        _poll = new CancellationTokenSource();
        _ = PollAsync(_poll.Token);
    }

    /// <summary>적재가 끝날 때까지 상태만 되묻는다. 끝나면 파일·통계를 한 번 다시 읽는다 —
    /// 적재가 도는 동안 그것까지 매번 읽으면 긴 적재에서 DB 를 쓸데없이 긁는다.</summary>
    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollInterval, ct);

                var now = await Gmail.ImportStatusAsync();
                _status = now;
                await InvokeAsync(StateHasChanged);

                if (now?.Running == true) continue;

                _stats = await Gmail.StatsAsync();
                await InvokeAsync(StateHasChanged);
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // 화면을 떠났다.
        }
        catch (Exception)
        {
            // 되묻기가 실패해도 화면이 죽으면 안 된다 — 적재 자체는 서버에서 계속 돈다.
        }
        finally
        {
            _poll?.Dispose();
            _poll = null;
        }
    }

    // ── 메일함 등록 ────────────────────────────────────────

    private Task OpenAccountAsync()
    {
        _edit = new AccountEdit();
        _accountOpen = true;
        return Task.CompletedTask;
    }

    private async Task SaveAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(_edit.Id) || string.IsNullOrWhiteSpace(_edit.Email))
        {
            Say("열쇠와 메일 주소를 적어 주세요.", NoticeTone.Warning);
            return;
        }

        await RunAsync(async () =>
        {
            // 주인을 안 보낸다 — 서버가 지금 사람으로 채운다. 남의 메일함을 만들 길을 두지 않는다.
            await Gmail.SaveAccountAsync(new GmailAccountSave(
                _edit.Id.Trim(), _edit.Email.Trim(), null, _edit.Kind, true));

            _accounts = await Gmail.AccountsAsync();
            _accountId = _edit.Id.Trim();
            _accountOpen = false;
        }, "메일함을 등록했습니다.");
    }

    private static string Period(GmailStatDto s)
    {
        if (s.Oldest is not { } from || s.Newest is not { } to) return "-";
        return $"{from.Kst("yyyy-MM-dd")} ~ {to.Kst("yyyy-MM-dd")}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_poll is { } cts)
        {
            await cts.CancelAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>고르개 한 줄. 나무를 깊이 순서로 펴면서 「상위 › 하위」 경로를 미리 지어 둔다.</summary>
    private sealed record FolderOption(long Id, string Label)
    {
        public static IReadOnlyList<FolderOption> Flatten(IReadOnlyList<RagFolderDto> folders)
        {
            // 뿌리는 ParentId 가 null 이다. 분류 번호가 양수라 0 을 뿌리 자리로 쓴다 —
            // null 을 사전 열쇠로 쓸 수 없다.
            var byParent = folders
                .GroupBy(f => f.ParentId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortNo).ThenBy(x => x.Name).ToList());

            var result = new List<FolderOption>();
            Walk(0, string.Empty);
            return result;

            void Walk(long parent, string prefix)
            {
                if (!byParent.TryGetValue(parent, out var rows)) return;

                foreach (var f in rows)
                {
                    var label = prefix.Length == 0 ? f.Name : $"{prefix} › {f.Name}";
                    result.Add(new FolderOption(f.Id, label));
                    Walk(f.Id, label);
                }
            }
        }
    }

    private enum UploadPhase
    {
        /// <summary>브라우저 → 포털(회로). 느린 쪽이다.</summary>
        Receiving,

        /// <summary>포털 → 게이트웨이 → 적재 폴더. 서버끼리라 빠르지만 공짜는 아니다.</summary>
        Sending,
    }

    /// <summary>올리는 동안의 진행. 속도는 <b>단계가 바뀐 뒤부터</b> 센다 —
    /// 처음부터 재면 느린 1단계가 2단계의 남은 시간을 몇 배로 부풀린다.</summary>
    private sealed class UploadProgress
    {
        public UploadPhase Phase { get; private set; }

        public long Sent { get; private set; }

        public long Total { get; private set; }

        private DateTime _since = DateTime.UtcNow;

        private long _base;

        public UploadProgress With(UploadPhase phase, long sent, long total)
        {
            if (phase != Phase)
            {
                Phase = phase;
                _since = DateTime.UtcNow;
                _base = sent;
            }

            Sent = sent;
            Total = total;
            return this;
        }

        public string PhaseLabel => Phase == UploadPhase.Receiving ? "브라우저 → 포털" : "포털 → 적재 폴더";

        public int Percent => Total > 0 ? (int)Math.Clamp(Sent * 100 / Total, 0, 100) : 0;

        /// <summary>초당 바이트. 아직 잴 것이 모자라면 <c>null</c>.</summary>
        public double? BytesPerSecond
        {
            get
            {
                var seconds = (DateTime.UtcNow - _since).TotalSeconds;
                if (seconds < 1) return null;

                var moved = Sent - _base;
                return moved <= 0 ? null : moved / seconds;
            }
        }

        public string SizeLabel => $"{Human(Sent)} / {Human(Total)}";

        public string? SpeedLabel => BytesPerSecond is { } bps ? $"{Human((long)bps)}/s" : null;

        /// <summary>남은 시간. 속도를 아직 모르면 <c>null</c> — 「계산 중」이 숫자보다 정직하다.</summary>
        public string? EtaLabel
        {
            get
            {
                if (BytesPerSecond is not { } bps || bps <= 0) return null;

                var left = TimeSpan.FromSeconds((Total - Sent) / bps);
                if (left.TotalSeconds < 1) return "곧 끝납니다";
                if (left.TotalMinutes < 1) return $"{left.Seconds}초 남음";
                if (left.TotalHours < 1) return $"{left.Minutes}분 {left.Seconds}초 남음";
                return $"{(int)left.TotalHours}시간 {left.Minutes}분 남음";
            }
        }

        private static string Human(long bytes) => bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:0.#} GB",
            >= 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
            >= 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes} B",
        };
    }

    private sealed class AccountEdit
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Kind { get; set; } = "api";
    }
}
