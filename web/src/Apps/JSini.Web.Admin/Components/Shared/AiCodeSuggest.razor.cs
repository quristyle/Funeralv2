using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Shared;

public partial class AiCodeSuggest
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>AI 에게 줄 한글 이름. 비면 아무것도 하지 않는다.</summary>
    [Parameter] public string? Word { get; set; }

    /// <summary>고른 코드를 받는 곳.</summary>
    [Parameter] public EventCallback<string> OnPick { get; set; }

    /// <summary>등록 중일 때만 켠다. 꺼져 있으면 단추도 없다.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>축약형(<c>USE_YN</c>) 대신 풀어 쓴 영문을 받고 싶을 때.</summary>
    [Parameter] public bool Natural { get; set; }

    private string? _code;
    private string? _failed;
    private bool _asking;

    /// <summary>지금 보이는 추천(또는 실패)이 어느 이름의 것인가.</summary>
    private string? _asked;

    /// <summary>물어본 차례. 마지막 것의 답만 받는다.</summary>
    private int _turn;

    protected override void OnParametersSet()
    {
        if (!Enabled)
        {
            Clear();
            return;
        }

        // 이름이 바뀌었으면 옛 이름의 추천을 걷는다. **묻지는 않는다** — 누를 때 묻는다.
        if (_asked is not null && (Word?.Trim() ?? "") != _asked)
        {
            Clear();
        }
    }

    private async Task AskClickedAsync()
    {
        var word = Word?.Trim();

        if (string.IsNullOrEmpty(word))
        {
            _code = null;
            _failed = "이름을 먼저 적으세요.";

            // 빈 이름에 대한 안내다. 이름을 적기 시작하면 위 OnParametersSet 이 걷는다.
            _asked = "";
            return;
        }

        _asked = word;
        await AskAsync(word);
    }

    private async Task AskAsync(string word)
    {
        var turn = ++_turn;

        _asking = true;
        _code = null;
        _failed = null;

        try
        {
            var suggested = await Api.SuggestCodeAsync(word, Natural);

            if (turn != _turn)
            {
                return;
            }

            _code = suggested?.Trim();

            // 빈 답도 실패다. 「AI 추천」 이라고 써 놓고 아무것도 없으면
            // 눌러야 하는지 기다려야 하는지 알 수 없다.
            if (string.IsNullOrEmpty(_code))
            {
                _failed = "받지 못했습니다.";
            }
        }
        catch (ApiException ex)
        {
            if (turn != _turn)
            {
                return;
            }

            // 대개 「키가 없다」·「하루 한도를 다 썼다」다. 서버가 준 말을 그대로
            // 보여 준다 — 여기서 뭉뚱그리면 설정을 고칠 수 있는 사람이 못 고친다.
            _failed = ex.Message;
        }
        catch (Exception)
        {
            if (turn != _turn)
            {
                return;
            }

            // AI 가 안 되는 것으로 등록이 막히면 안 된다. 조용히 접는다.
            _failed = "받지 못했습니다.";
        }
        finally
        {
            if (turn == _turn)
            {
                _asking = false;
                StateHasChanged();
            }
        }
    }

    private async Task PickAsync()
    {
        if (!string.IsNullOrWhiteSpace(_code))
        {
            await OnPick.InvokeAsync(_code);
        }
    }

    private void Clear()
    {
        // 가는 중인 답도 버린다.
        _turn++;
        _asked = null;
        _code = null;
        _failed = null;
        _asking = false;
    }
}
