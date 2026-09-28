cat << 'INNER_EOF' > web/src/Apps/JSini.Web.HelpDesk/Components/Pages/RequestManage.razor
@page "/helpdesk/request/manage"
@attribute [RouteKey("helpdesk.request.manage")]

@*
    [요청 처리] — DB 메뉴 `/helpdesk/request/manage`.
    이력: Vue `views/helpdesk/request/manage` → 여기.

    [GET 으로 검색을 부르고 있었다]

    `requests/srch` 를 `GET` 으로 불렀는데 **검색은 POST 다.** GET 자리에는
    `requests/{id:int}` 가 있어서 `srch` 를 숫자로 읽으려다 500 이 났다 —

        Failed to bind parameter "int id" from "srch".

    404 가 아니라 500 이라 「서버가 죽었나」 로 읽히기 쉬웠고, 표는 늘 비어
    있었다.

    [칸을 손으로 적었다]

    담당자가 **매일 여는 화면**이다. 그런 화면은 서버가 준 대로 늘어놓는
    `AutoGrid` 로 두지 않는다 — 상태·긴급·담당자처럼 눈에 먼저 들어와야 하는
    것이 정해져 있고, 정렬 순서도 고정이어야 한다.

    [상세는 새 탭으로 연다 (2026-09-28)]

    「상세」를 누르면 그 요청이 **탭 줄에 제 탭으로** 선다. 이 표를 켜 둔 탭은
    그대로 남으므로 여러 건을 열어 놓고 오갈 수 있다 — 전에는 상세로 옮겨도
    탭 줄이 이 화면을 켜 놓은 채였고, 돌아오려면 뒤로 가기뿐이었다.
    탭을 세우는 쪽은 상세 화면이다(`RequestDetail` 머리말).

    [목록(`/helpdesk/request/list`)과 무엇이 다른가]

    자료는 같고 **보는 사람이 다르다.** 그쪽은 요청자가 자기 것만 보고,
    여기는 처리자가 본다. 시스템관리자는 회사 조건을 고를 수 있고, 그 밖의
    사용자는 신원에 연결된 회사로 고정해 다른 회사 요청을 조회하지 못한다.
*@

@inherits DataPage

<PageTitle>요청 처리 · 헬프데스크</PageTitle>

@* 조건이 여럿이라 한 줄에 다 안 들어간다. `CommSch` 가 넘치는 만큼 아래로
   흘려 담고, 「조회」는 늘 오른쪽 끝에 남는다. *@
<CommSch OnSearch="@ReloadAsync" MobileSummary="@ConditionSummary">
    <Fields>
        @if (Context.IsSystemAdmin)
        {
            <CommSchItem Label="회사">
                <DxComboBox Data="@CompanyFilterOptions" @bind-Value="_companyId" @bind-Value:after="ReloadAsync"
                            TextFieldName="@nameof(BizOption.Label)" ValueFieldName="@nameof(BizOption.Value)"
                            Style="width: 180px"
                            ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto" />
            </CommSchItem>
        }

        <CommSchItem Label="요청자">
            <DxComboBox Data="@RequesterFilterOptions" @bind-Value="_requesterId" @bind-Value:after="ReloadAsync"
                        TextFieldName="@nameof(BizOption.Label)" ValueFieldName="@nameof(BizOption.Value)"
                        Style="width: 120px"
                        ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto" />
        </CommSchItem>

        <CommSchItem Label="상태">
            <DxComboBox Data="@StatusOptions" @bind-Value="_status" @bind-Value:after="ReloadAsync"
                        TextFieldName="Text" ValueFieldName="Value" Style="width: 120px"
                        ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto" />
        </CommSchItem>

        <CommSchItem Label="담당자">
            <DxComboBox Data="@Context.AdminOptions" @bind-Value="_adminId" @bind-Value:after="ReloadAsync"
                        TextFieldName="@nameof(BizOption.Label)" ValueFieldName="@nameof(BizOption.Value)"
                        Style="width: 150px"
                        ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto" />
        </CommSchItem>

        <CommSchItem Label="제목">
            <DxTextBox @bind-Text="_keyword" @bind-Text:after="ReloadAsync" Style="width: 200px"
                       ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto" />
        </CommSchItem>

        <CommSchItem>
            <DxCheckBox @bind-Checked="_onlyOpen" @bind-Checked:after="ReloadAsync">처리 중인 것만</DxCheckBox>
        </CommSchItem>
    </Fields>
</CommSch>

<CommCont>
    <CommGrd TItem="ImprovementRequest" Data="@_rows" PageSize="25"
             ExportName="요청관리"
             Reload="@ReloadAsync"
             OnRowClick="@OnRowClick">
        <Columns>
            <DxGridDataColumn FieldName="@nameof(ImprovementRequest.Id)" Caption="번호" Width="80" />

            <DxGridDataColumn Caption="제목" MinWidth="260">
                <CellDisplayTemplate Context="cell">
                    @{
                        var r = (ImprovementRequest)cell.DataItem;
                    }
                    <div class="jsini-tags">
                        @if (r.IsEmergency == true)
                        {
                            <span class="jsini-badge jsini-badge--off">긴급</span>
                        }
                        <span>@r.Title</span>
                    </div>
                </CellDisplayTemplate>
            </DxGridDataColumn>

            <DxGridDataColumn Caption="고객사" Width="140">
                <CellDisplayTemplate Context="cell">
                    @{
                        var r = (ImprovementRequest)cell.DataItem;
                    }
                    @* 회사는 요청자(고객)를 거쳐 붙는다. 서버가 주는 것은
                       포털 회사 **아이디**뿐이라, 이름은 회사 목록에서 푼다. *@
                    @Context.CompanyName(r.Customer?.CompanyId)
                </CellDisplayTemplate>
            </DxGridDataColumn>

            <DxGridDataColumn Caption="요청자" Width="110">
                <CellDisplayTemplate Context="cell">
                    @(((ImprovementRequest)cell.DataItem).Customer?.UserName ?? "-")
                </CellDisplayTemplate>
            </DxGridDataColumn>

            <DxGridDataColumn Caption="담당자" Width="110">
                <CellDisplayTemplate Context="cell">
                    @{
                        var r = (ImprovementRequest)cell.DataItem;
                    }
                    @if (r.Admin is null)
                    {
                        <span class="jsini-badge jsini-badge--warn">미배정</span>
                    }
                    else
                    {
                        @r.Admin.UserName
                    }
                </CellDisplayTemplate>
            </DxGridDataColumn>

            <DxGridDataColumn Caption="상태" Width="90">
                <CellDisplayTemplate Context="cell">
                    @{
                        var r = (ImprovementRequest)cell.DataItem;
                    }
                    <span class="jsini-badge @StatusClass(r.Status)">@StatusText(r)</span>
                </CellDisplayTemplate>
            </DxGridDataColumn>

            <DxGridDataColumn FieldName="@nameof(ImprovementRequest.CreatedAt)" Caption="접수" Width="150"
                              DisplayFormat="yyyy-MM-dd HH:mm" />

            <DxGridDataColumn Caption="경과" Width="80">
                <CellDisplayTemplate Context="cell">
                    @Elapsed((ImprovementRequest)cell.DataItem)
                </CellDisplayTemplate>
            </DxGridDataColumn>

        </Columns>
    </CommGrd>
</CommCont>
INNER_EOF
