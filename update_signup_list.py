import re

with open('./web/src/Apps/JSini.Web.Admin/Components/Pages/SignupList.razor.cs', 'r') as f:
    content = f.read()

content = content.replace(
    '    private string? _reason;',
    '''    private string? _reason;
    private bool _merging;
    private string? _targetAccountId;'''
)

merge_methods = '''

    private Task AskMergeAsync(SignupPendingDto row)
    {
        _target = row;
        _targetAccountId = null;
        _merging = true;
        return Task.CompletedTask;
    }

    private async Task MergeAsync()
    {
        if (_target is null || string.IsNullOrWhiteSpace(_targetAccountId))
        {
            return;
        }

        var name = _target.UserName;

        var ok = await RunAsync(
            () => Api.MergeAsync(_target.Id, _targetAccountId),
            $"{name} 님의 소셜 연결을 결합했습니다.",
            "결합하지 못했습니다");

        _merging = false;

        if (ok)
        {
            await ReloadAsync();
            Say($"{name} 님의 소셜 연결을 결합했습니다.");
        }
    }
'''

content = content.replace('    }\n}', '    }' + merge_methods + '}')

with open('./web/src/Apps/JSini.Web.Admin/Components/Pages/SignupList.razor.cs', 'w') as f:
    f.write(content)

with open('./web/src/Apps/JSini.Web.Admin/Components/Pages/SignupList.razor', 'r') as f:
    content = f.read()

row_actions = '''            <DxButton IconCssClass="jsini-icon-check-circle" Title="가입을 승인한다" RenderStyle="ButtonRenderStyle.Link"
                      Click="@(() => ApproveAsync(row))" />
            <DxButton IconCssClass="jsini-icon-link" Title="기존 계정으로 결합한다" RenderStyle="ButtonRenderStyle.Link"
                      Visible="@(row.SocialProvider != null)"
                      Click="@(() => AskMergeAsync(row))" />
            <DxButton IconCssClass="jsini-icon-close-circle" Title="가입을 거절한다" CssClass="commgrd__icon--warn" RenderStyle="ButtonRenderStyle.Link"
                      Click="@(() => AskRejectAsync(row))" />'''

content = content.replace(
    '''            <DxButton IconCssClass="jsini-icon-check-circle" Title="가입을 승인한다" RenderStyle="ButtonRenderStyle.Link"
                      Click="@(() => ApproveAsync(row))" />
            <DxButton IconCssClass="jsini-icon-close-circle" Title="가입을 거절한다" CssClass="commgrd__icon--warn" RenderStyle="ButtonRenderStyle.Link"
                      Click="@(() => AskRejectAsync(row))" />''',
    row_actions
)

merge_popup = '''
<CommPopup @bind-Visible="_merging" HeaderText="소셜 가입 결합" Width="420px">
    <BodyContentTemplate>
        <p class="jsini-hint">
            @(_target?.UserName ?? "-") (@(_target?.LoginId ?? "-")) 님의 소셜 연결을 기존 계정으로 옮기고 신청은 지웁니다.
        </p>

        <DxTextBox @bind-Text="_targetAccountId" NullText="대상 계정 ID" />
    </BodyContentTemplate>

    <FooterContentTemplate Context="popupContext">
        <DxButton Text="결합" RenderStyle="ButtonRenderStyle.Primary" Click="@MergeAsync" />
        <DxButton Text="닫기" RenderStyle="ButtonRenderStyle.Secondary"
                  Click="@(() => _merging = false)" />
    </FooterContentTemplate>
</CommPopup>
'''

content += merge_popup

with open('./web/src/Apps/JSini.Web.Admin/Components/Pages/SignupList.razor', 'w') as f:
    f.write(content)
