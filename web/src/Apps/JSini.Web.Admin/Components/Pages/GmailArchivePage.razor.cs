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

    private GmailImportStatusDto? _status;
    private string? _accountId;
    private string? _categoryId;
    private int _maxMessages = 1000;

    private string? _rootHint;
    private string? _archiveHint;

    private bool _accountOpen;
    private AccountEdit _edit = new();

    private CancellationTokenSource? _poll;

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

    private async Task UploadAsync(IReadOnlyList<PickedFile> picked)
    {
        if (picked.Count == 0) return;

        var one = picked[0];
        await RunAsync(async () =>
        {
            await using var stream = File.OpenRead(one.TempPath);
            await Gmail.UploadAsync(stream, one.Name);
            _files = await Gmail.FilesAsync();
            _rootHint = null;
        }, $"{one.Name} 을(를) 올렸습니다.");
    }

    private async Task ArchiveAsync()
    {
        if (_categoryId is not { Length: > 0 } category) return;

        await RunAsync(async () =>
        {
            var result = await Gmail.ArchiveAsync(category, _maxMessages);
            _stats = await Gmail.StatsAsync();

            // 「남은 것이 없다」와 「카테고리가 없다」는 실패가 아니라 알림이다 — 판 머리에 적는다.
            _archiveHint = result?.Note;

            if (result is { Documents: > 0 })
            {
                Say($"메일 {result.Messages:N0}통을 문서 {result.Documents}장으로 올렸습니다. 색인은 뒤에서 만들어집니다.");
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

    private sealed class AccountEdit
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Kind { get; set; } = "api";
    }
}
