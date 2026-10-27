using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ScheduleNote : IDisposable
{
    [Inject] private WbsClient Api { get; set; } = default!;

    [Inject] private ScheduleNoteAssist Assist { get; set; } = default!;

    /// <summary>치기를 멈추고 이만큼 지나야 AI 에게 묻는다. 글자마다 물으면 공급자 한도(429)에 먼저 닿는다.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1200);

    /// <summary>구분을 안 고르면 쓴다. 비우면 일정표의 어느 구분 거르기에도 안 걸리는 줄이 된다.</summary>
    private const string DefaultKind = "Public";

    private string? _projectCode;
    private string? _kind = DefaultKind;
    private DateOnly? _start;
    private DateOnly? _end;
    private string? _title;
    private string? _summary;
    private string? _body;

    /// <summary>사람이 손으로 고친 칸. AI 가 그 칸은 덮지 않는다.</summary>
    private readonly HashSet<string> _touched = [];

    /// <summary>한 번 저장한 줄. 그 뒤의 저장은 새 줄이 아니라 이 줄을 고친다 — 안 그러면 누를 때마다 일정이 하나씩 는다.</summary>
    private WbsItemDto? _saved;

    private int? SavedId => _saved?.WbsId;

    private CancellationTokenSource? _pending;

    /// <summary>나간 물음의 차례. 늦게 온 옛 답이 새 답을 덮지 않게 한다.</summary>
    private int _ask;

    private bool _thinking;
    private string? _model;
    private string? _failed;

    private string? AiState =>
        _thinking ? "AI 가 정리하는 중…"
        : _failed is not null ? _failed
        : _model is not null ? $"AI · {_model}"
        : null;

    private void OnBodyChanged(string? text)
    {
        _body = text;
        _ = AskLaterAsync(force: false, delay: Settle);
    }

    /// <summary>사람이 고친 칸을 무시하고 본문으로 다시 채운다.</summary>
    private Task RefillAsync()
    {
        _touched.Clear();
        return AskLaterAsync(force: true, delay: TimeSpan.Zero);
    }

    private async Task AskLaterAsync(bool force, TimeSpan delay)
    {
        _pending?.Cancel();
        _pending?.Dispose();

        var cts = _pending = new CancellationTokenSource();
        var ask = ++_ask;
        var body = _body;

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cts.Token);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            _thinking = true;
            _failed = null;
            await InvokeAsync(StateHasChanged);

            var draft = await Assist.DraftAsync(body, cts.Token);

            if (ask != _ask)
            {
                return;
            }

            _model = draft.Model ?? _model;

            if (draft is { Start: null, Title: null, Summary: null })
            {
                _failed = "AI 답을 읽지 못했습니다";
            }
            else
            {
                Apply(draft, force);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            if (ask == _ask)
            {
                _failed = "AI 에 닿지 못했습니다";
            }
        }
        finally
        {
            if (ask == _ask)
            {
                _thinking = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private void Apply(ScheduleDraft draft, bool force)
    {
        if (draft.Start is not null && (force || !_touched.Contains(nameof(_start)))) _start = draft.Start;
        if (draft.End is not null && (force || !_touched.Contains(nameof(_end)))) _end = draft.End;
        if (draft.Title is not null && (force || !_touched.Contains(nameof(_title)))) _title = draft.Title;
        if (draft.Summary is not null && (force || !_touched.Contains(nameof(_summary)))) _summary = draft.Summary;
    }

    /// <summary>사람이 왼쪽 칸을 고쳤다. 고친 칸으로 적어 두어 다음 AI 답이 덮지 않게 한다.</summary>
    private void Touch(string field, Action set)
    {
        set();
        _touched.Add(field);
    }

    private async Task SaveAsync()
    {
        if (!int.TryParse(_projectCode, out var prjRid))
        {
            Say("프로젝트를 고르십시오.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_title))
        {
            Say("제목을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        var start = _start ?? AppTime.Today;
        var end = _end is { } e && e >= start ? e : start;

        // 고칠 때는 저장해 둔 줄 위에 얹는다. 서버의 수정은 칸 전부를 덮으므로 새 객체를 보내면 담당·개발일 같은 칸이 비워진다.
        var item = _saved ?? new WbsItemDto();

        item.PrjRid = prjRid;
        item.ScheduleType = string.IsNullOrWhiteSpace(_kind) ? DefaultKind : _kind;
        item.ProcNm = _title.Trim();
        item.PlanSdt = start;
        item.PlanEdt = end;
        item.Summary = _summary?.Trim() ?? string.Empty;
        item.BodyText = _body ?? string.Empty;

        var isNew = _saved is null;
        WbsItemDto? saved = null;

        if (await RunAsync(
                async () => saved = isNew ? await Api.CreateAsync(item) : await Api.UpdateAsync(item),
                isNew ? "일정에 저장했습니다." : "일정을 고쳤습니다.",
                "일정을 저장하지 못했습니다")
            && saved is not null)
        {
            _saved = saved;
            _start = saved.PlanSdt;
            _end = saved.PlanEdt;
        }
    }

    private void Reset()
    {
        _pending?.Cancel();
        _ask++;

        _saved = null;
        _start = _end = null;
        _title = _summary = _body = null;
        _touched.Clear();
        _thinking = false;
        _failed = null;
    }

    private static DateTime? Day(DateOnly? value) => value?.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? Only(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);

    public void Dispose()
    {
        _pending?.Cancel();
        _pending?.Dispose();
    }
}
