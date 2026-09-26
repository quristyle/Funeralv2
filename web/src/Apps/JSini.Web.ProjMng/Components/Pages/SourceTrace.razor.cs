using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using System.Data;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class SourceTrace
{
    [Inject] private ProjMngClient Client { get; set; } = default!;
    [Inject] private SourceInfoClient Sources { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _sourceName, _extend);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    /// <summary>고른 소스의 이름. 프로젝트와 같은 까닭으로 부품이 올려 준다.</summary>
    private string? _sourceName;

    private string? _projectCode;
    private string? _sourceCode;
    private string? _extend;

    private List<string> _extends = [];
    private string _sourceLang = string.Empty;

    private ProjMngTable _files = ProjMngTable.Empty;

    private string? _openedPath;
    private string? _content;

    /// <summary>
    /// 편집기에 알려 줄 구문 종류.
    ///
    /// 확장자 이름이 곧 monaco 의 언어 이름은 아니다. 옛 화면부터 쓰던 치환
    /// 규칙에 razor·cs 를 더했다 — 모르는 것은 <c>plaintext</c> 로 둔다.
    /// </summary>
    private string EditorLanguage => (_extend ?? _sourceLang).ToLowerInvariant() switch
    {
        "js" => "javascript",
        "ts" => "typescript",
        "jsp" or "html" or "razor" or "cshtml" or "vue" => "html",
        "cs" or "c#" => "csharp",
        "java" => "java",
        "sql" or "pgsql" => "pgsql",
        "xml" or "glue" => "xml",
        "json" => "json",
        "" => "plaintext",
        var other => other,
    };

    /// <summary>
    /// 프로젝트가 바뀌면 아래 것이 전부 무효다. 소스 목록은
    /// <c>SourceSelect</c> 가 다시 읽고 <c>OnChanged</c> 로 새 소스를 올려 준다.
    /// </summary>
    private void OnProjectChanged()
    {
        _extends = [];
        _extend = null;
        Clear();
    }

    private async Task OnSourceChangedAsync(SourceItem? item)
    {
        Clear();

        _sourceLang = item?.Lang ?? string.Empty;
        _extends = [];
        _extend = null;

        if (item is null)
        {
            return;
        }

        await LoadExtendsAsync(item);
        await SearchAsync();
    }

    private void OnExtendChanged(string? value)
    {
        _extend = value;
        Clear();
    }

    private void Clear()
    {
        _files = ProjMngTable.Empty;
        _openedPath = null;
        _content = null;
    }

    /// <summary>
    /// 훑을 확장자 목록.
    ///
    /// 소스 상세(<c>sp_dev_srcinfo_dtl_exec</c>)의 <c>src_extend</c> 가 정본이다.
    /// 다만 그 표에는 확장자가 아닌 행도 섞여 있다 — <c>desc</c>(머리말 추출
    /// 규칙)가 그렇다. 그런 것을 확장자로 내밀면 훑을 파일이 하나도 없다.
    /// </summary>
    private async Task LoadExtendsAsync(SourceItem item)
    {
        // **프로시저를 부르지 않는다.** 옛 길은 `sp_dev_srcinfo_dtl_exec` 였고
        // 지금은 그 표를 REST 로 읽는다(`SourceInfoClient`).
        var details = await Sources.DetailsAsync(int.TryParse(item.Rid, out var rid) ? rid : 0);

        var found = new List<string>();

        foreach (var row in details)
        {
            var grp = row.SrcPatternGrp ?? string.Empty;
            var extend = row.SrcExtend ?? string.Empty;

            // `grp='extend'` 행은 확장자를 값 쪽에 적어 둔다. 그 소스의
            // 기본 확장자라 목록에 함께 넣는다.
            if (string.Equals(grp, "extend", StringComparison.OrdinalIgnoreCase))
            {
                extend = row.UrlPattern ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(extend)
                || string.Equals(extend, "desc", StringComparison.OrdinalIgnoreCase)
                || found.Contains(extend, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            found.Add(extend);
        }

        // 상세에 아무것도 없으면 소스의 주 언어로라도 골라 준다.
        if (found.Count == 0 && !string.IsNullOrWhiteSpace(item.Lang))
        {
            found.Add(item.Lang);
        }

        _extends = found;
        _extend = found.FirstOrDefault();
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(_sourceCode))
        {
            throw new ApiException("추적할 소스를 고르십시오.");
        }

        _openedPath = null;
        _content = null;

        var result = await Client.MdContAsync("md_source_trace", new Dictionary<string, object?>
        {
            ["prj_rid"] = _projectCode ?? string.Empty,
            ["src_rid"] = _sourceCode,
            ["src_lang"] = _extend ?? _sourceLang,
        });

        // 서버가 경로를 못 훑었을 때는 이유를 준다(등록된 경로가 서버에
        // 없다는 안내가 대부분이다). 빈 표만 보여 주면 원인을 알 수 없다.
        if (result.ProcCode < 0)
        {
            throw new ApiException(result.Message ?? "소스를 훑지 못했습니다.");
        }

        _files = ProjMngTable.From(result);
        return _files.Table.Rows.Count;
    }, "그 경로에서 찾은 파일이 없습니다.", "소스를 훑지 못했습니다");

    /// <summary>
    /// 고른 파일의 내용.
    ///
    /// 경로는 <b>서버가 준 값 그대로</b> 돌려보낸다 — 사용자가 친 글자를
    /// 실어 보내면 서버 디스크의 아무 파일이나 읽히는 길이 된다.
    /// </summary>
    /// <summary>고른 파일 줄. <b>표에 돌려줘야 강조가 유지된다.</b></summary>
    private DataRowView? _pickedFile;

    private Task ShowFileAsync(DataRowView? picked)
    {
        _pickedFile = picked;

        // 표를 자료로 주면 셀·선택에 `DataRowView` 가 온다(RuntimeCell 머리말).
        var row = RuntimeCell.Row(picked);

        var path = row is not null && row.Table.Columns.Contains("fullpath")
            ? row["fullpath"]?.ToString()
            : null;

        if (string.IsNullOrWhiteSpace(path))
        {
            _openedPath = null;
            _content = null;
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var result = await Client.MdContAsync("md_source_context", new Dictionary<string, object?>
            {
                ["fullpath"] = path,
            });

            if (result.ProcCode < 0)
            {
                throw new ApiException(result.Message ?? "파일을 읽지 못했습니다.");
            }

            _content = result.Rows?.FirstOrDefault()?.GetValueOrDefault("context")?.ToString();
            _openedPath = path;

            return string.IsNullOrEmpty(_content) ? 0 : 1;
        }, "그 파일이 비어 있습니다.", "파일을 읽지 못했습니다");
    }
}
