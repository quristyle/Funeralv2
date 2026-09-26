using Microsoft.AspNetCore.Components;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class BirthdayMessages
{
    [Inject] private BirthdayClient Api { get; set; } = default!;

    private int _tabIndex;
    private IReadOnlyList<BirthdayMessage> _messages = [];

    private string PersonCaption => _tabIndex == 1 ? "받는 사람" : "보낸 사람";

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _messages = _tabIndex switch
        {
            1 => await Api.GetSentAsync(),
            2 => await Api.GetTodayMessagesAsync(),
            _ => await Api.GetReceivedAsync(),
        };

        return _messages.Count;
    }, "메시지가 없습니다.", "메시지를 읽지 못했습니다");

    private static string Department(BirthdayMessage m) =>
        m.SenderDepartment ?? m.RecipientDepartment ?? string.Empty;
}
