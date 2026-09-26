using JSini.Web.Admin.Api;
using JSini.Web.Admin.Components.Shared;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Company;

/// <summary>회사 속성 수정 탭.</summary>
public partial class CompanyInfoTab
{
    /// <summary>고치는 복사본. 화면이 들고 있다.</summary>
    [Parameter, EditorRequired] public CompanyDto Edit { get; set; } = default!;

    /// <summary>판을 열 때의 값. 「저장 안 함」을 가른다.</summary>
    [Parameter, EditorRequired] public CompanyDto Original { get; set; } = default!;

    [Parameter] public bool IsNew { get; set; }

    [Parameter] public IReadOnlyList<CommonCodeDto> Usages { get; set; } = [];

    /// <summary>저장할 수 있는가. 아니면 폼이 읽기 전용이고 단추가 없다.</summary>
    [Parameter] public bool CanSave { get; set; }

    [Parameter] public bool Saving { get; set; }

    [Parameter] public EventCallback OnSave { get; set; }

    [Parameter] public EventCallback OnRevert { get; set; }

    private bool IsDirty => !CompanyForm.Same(Edit, Original);

    /// <summary>찾아 온 주소를 넣는다. 상세 주소(층·호수)는 사람이 적은 값이라 두고 간다.</summary>
    private void FillAddress(AddrFind.AddrPick picked)
    {
        Edit.ZipCode = picked.ZipCode;
        Edit.Address = picked.Address;
    }
}
