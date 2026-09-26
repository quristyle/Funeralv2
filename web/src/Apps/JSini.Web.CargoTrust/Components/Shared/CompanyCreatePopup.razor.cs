using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Api;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class CompanyCreatePopup
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    /// <summary>
    /// 거래처가 정해졌을 때(새로 등록했거나, 이미 있던 것을 쓰기로 했을 때).
    /// 안 주면 이미 있던 것은 그 회사 상세로 옮겨 간다.
    /// </summary>
    [Parameter] public EventCallback<CompanyInfo> OnPicked { get; set; }

    private bool _visible;
    private string? _number;
    private bool _checked;
    private CompanyInfo? _existing;
    private CompanyCreateRequest _form = new();

    /// <summary>연다. 검색칸에 번호를 쳐 둔 채 눌렀으면 그 번호를 받아 둔다.</summary>
    public void Open(string? number = null)
    {
        _number = BusinessNumber.Digits(number).Length == 10 ? BusinessNumber.Format(number) : null;
        _checked = false;
        _existing = null;
        _form = new CompanyCreateRequest();
        _visible = true;
        StateHasChanged();
    }

    private void OnNumberChanged(string? value)
    {
        _number = value;

        // 확인한 번호와 저장할 번호가 달라지지 않게 — 고치면 처음부터 다시 묻는다.
        _checked = false;
        _existing = null;
    }

    private async Task CheckAsync()
    {
        if (!BusinessNumber.IsValid(_number))
        {
            Say("사업자등록번호가 올바르지 않습니다. 숫자 10자리를 확인하십시오.", NoticeTone.Warning);
            return;
        }

        CompanyInfo? found = null;
        if (await RunAsync(async () => found = await Api.GetCompanyByNumberAsync(_number!),
                okMessage: string.Empty, failMessage: "사업자번호를 확인하지 못했습니다"))
        {
            _existing = found;
            _checked = true;
            _number = BusinessNumber.Format(_number);
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.CompanyName))
        {
            Say("회사명을 넣으십시오.", NoticeTone.Warning);
            return;
        }

        _form.BusinessNumber = BusinessNumber.Digits(_number);

        CompanyInfo? created = null;
        if (await RunAsync(async () => created = await Api.CreateCompanyAsync(_form),
                $"「{_form.CompanyName}」 을 등록했습니다.", "거래처를 등록하지 못했습니다")
            && created is not null)
        {
            _visible = false;
            await DeliverAsync(created);
        }
    }

    private async Task UseExistingAsync()
    {
        if (_existing is null)
        {
            return;
        }

        _visible = false;
        await DeliverAsync(_existing);
    }

    private async Task DeliverAsync(CompanyInfo company)
    {
        if (OnPicked.HasDelegate)
        {
            await OnPicked.InvokeAsync(company);
        }
        else
        {
            Navigation.NavigateTo($"/cargotrust/companies/{company.CompanyId}");
        }
    }
}
