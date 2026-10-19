using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class AiTargetStatusList
{
    [Inject] private AiTargetStatusClient Api { get; set; } = default!;

    /// <summary>확인된 지 이만큼 지나면 시각을 흐린 색으로 적는다. 실행기 주기(3분)의 두 배를 넘도록 소식이 없으면 실행기가 멈춘 것일 때가 많다 — 값이 틀린 것보다 멈춘 것이 알아채기 어려워 색으로 말한다.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    private IReadOnlyList<AiTargetStatusDto> _rows = [];

    /// <summary>표에서 고른 한 건. 아래 「자세히」가 이것을 그린다.</summary>
    private AiTargetStatusDto? _picked;

    /// <summary>「지금 확인」의 답을 기다리는 동안만 도는 타이머.</summary>
    private CancellationTokenSource? _poll;

    private string Hint
    {
        get
        {
            if (_rows.Count == 0)
            {
                return string.Empty;
            }

            var clean = _rows.Count(t => t.Probed && t.IsRepo && t.IsClean);
            var bad = _rows.Count(t => t.Tone == "err");

            return bad > 0
                ? $"{_rows.Count}건 · 깨끗함 {clean} · 볼 것 {bad}"
                : $"{_rows.Count}건 · 깨끗함 {clean}";
        }
    }

    /// <summary>머리말 한 줄. 스냅샷이라는 사실을 늘 말하며, 한 번도 확인된 적 없는 대상이 있으면 그쪽을 먼저 말한다 — 대개 실행기가 안 떠 있다는 뜻이고, 모르면 빈 칸을 「문제 없음」으로 읽는다.</summary>
    private string SnapshotNotice
    {
        get
        {
            // 한 건도 없으면 앞엣것이 거짓이라(0 > 0) 일반 안내로 떨어진다.
            var never = _rows.Count(t => !t.Probed);

            return never > 0 && never == _rows.Count
                ? "아직 아무 대상도 확인되지 않았습니다. 대상을 들여다보는 것은 그 장비의 실행기라, "
                + "실행기가 떠 있지 않으면 이 화면은 비어 있습니다."
                : "실행기가 주기적으로 들여다본 결과입니다 — 지금 이 순간의 값이 아니라 "
                + "「확인」 칸의 시각 기준입니다. 앞섬·뒤처짐은 fetch 없이 세므로 "
                + "마지막으로 당겨 온 것 기준입니다.";
        }
    }

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        Show(await Api.ListAsync());
        return _rows.Count;
    }, "등록된 대상이 없습니다.", "대상 상태를 읽지 못했습니다");

    /// <summary>받은 목록을 화면에 앉힌다. 고른 건은 새 목록의 같은 건으로 갈아 끼운다 — 그대로 두면 아래 「자세히」가 다시 읽기 전의 값을 계속 그린다.</summary>
    private void Show(IReadOnlyList<AiTargetStatusDto> rows)
    {
        _rows = rows;

        if (_picked is { } old)
        {
            _picked = rows.FirstOrDefault(t => t.TargetKey == old.TargetKey);
        }

        Follow();
    }

    /// <summary>한 건을 다시 봐 달라고 한다.</summary>
    private async Task ProbeAsync(AiTargetStatusDto t)
    {
        var ok = await RunAsync(
            () => Api.ProbeAsync(t.TargetKey),
            $"「{t.TargetNm}」 을(를) 확인해 달라고 알렸습니다. 곧 값이 바뀝니다.",
            "확인 요청을 보내지 못했습니다");

        if (ok)
        {
            await SearchAsync();
        }
    }

    /// <summary>전부 다시 봐 달라고 한다. 건마다 따로 부른다 — 묶어 보내는 경로를 서버에 두면 한 건짜리와 여러 건짜리 규칙이 둘이 된다.</summary>
    private async Task ProbeAllAsync()
    {
        var targets = _rows.Where(t => !t.ProbePending).ToList();

        if (targets.Count == 0)
        {
            Say("이미 전부 확인을 기다리는 중입니다.");
            return;
        }

        var ok = await RunAsync(
            async () =>
            {
                foreach (var t in targets)
                {
                    await Api.ProbeAsync(t.TargetKey);
                }
            },
            $"{targets.Count}건을 확인해 달라고 알렸습니다. 곧 값이 바뀝니다.",
            "확인 요청을 보내지 못했습니다");

        if (ok)
        {
            await SearchAsync();
        }
    }

    /// <summary>답을 기다리는 것이 있으면 따라간다. 없으면 타이머를 끈다 — 끄지 않으면 열어 두고 잊은 화면에서 회로가 계속 일한다.</summary>
    private void Follow()
    {
        if (!_rows.Any(t => t.ProbePending))
        {
            StopPoll();
            return;
        }

        if (_poll is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _poll = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    // 실행기의 확인 바퀴가 15초다. 5초면 늦지 않게 잡는다.
                    await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);

                    var rows = await Api.ListAsync(ct: cts.Token);

                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    // Show 는 여기서 다시 Follow 로 들어와 같은 타이머를 보고 돌아온다 — 끝났으면 거기서 꺼진다.
                    Show(rows);

                    await InvokeAsync(StateHasChanged);

                    if (!_rows.Any(t => t.ProbePending))
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 화면을 떠났다. 정상이다.
            }
            catch
            {
                // 한 번 실패했다고 화면을 깨지 않는다. 다음 바퀴에 다시 본다.
            }
        }, cts.Token);
    }

    private void StopPoll()
    {
        var cts = _poll;
        _poll = null;

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // 이미 치웠다.
        }
    }

    /// <summary>화면을 떠날 때 반드시 끈다. 안 끄면 회로마다 타이머가 쌓인다.</summary>
    public void Dispose() => StopPoll();

    // ── 표기 ────────────────────────────────────────────────
    // 서버가 주는 시각은 UTC 다. 견주기는 UTC 끼리 하고, 적을 때만 한국 시각으로 옮긴다(docs/utc-time.md).

    private static string When(DateTime? at) => at.Kst("yyyy-MM-dd HH:mm");

    /// <summary>「3분 전」. <b>이 화면에서 제일 중요한 글자다.</b></summary>
    private static string Ago(DateTime? at)
    {
        if (at is not { } v)
        {
            return "없음";
        }

        var d = AppTime.UtcNow - v;

        // 시계가 몇 초 어긋나면 음수가 된다. 「-3초 전」보다 「방금」이 낫다.
        return d < TimeSpan.FromMinutes(1) ? "방금"
            : d < TimeSpan.FromHours(1) ? $"{(int)d.TotalMinutes}분 전"
            : d < TimeSpan.FromDays(1) ? $"{(int)d.TotalHours}시간 전"
            : $"{(int)d.TotalDays}일 전";
    }

    /// <summary>확인된 지 오래됐나. 시각을 흐리게 그릴지를 정한다.</summary>
    private static bool Stale(AiTargetStatusDto t) =>
        t.ProbedAt is not { } at || AppTime.UtcNow - at > StaleAfter;

    /// <summary>등록된 기준 가지와 다른 데 서 있나. 확인되지 않았거나 기준이 비어 있으면 아무 말도 하지 않는다 — 모르는 것을 경고로 그리면 경고가 늘 켜진다.</summary>
    private static bool OffDefault(AiTargetStatusDto t) =>
        t.IsRepo
        && t.Branch is { Length: > 0 }
        && t.DefaultRef is { Length: > 0 }
        && !string.Equals(t.Branch, t.DefaultRef, StringComparison.Ordinal);

    private static string IsolationText(AiTargetStatusDto t) => t.IsolationMode switch
    {
        "worktree" => "worktree",
        "copy" => "복사본",
        "inplace" => "원본 직접",
        _ => t.IsolationMode ?? "-",
    };
}
