using DevExpress.Blazor;
using DevExpress.Blazor.Office;

namespace JSini.Web.HelpDesk.Components.Shared;

/// <summary>DxHtmlEditor 도구줄의 이름표를 한국어로 바꿔 단다. DevExpress 는 한국어 리소스를 들고 있지 않아 부품 바깥에서 CustomizeToolbar 로 칸마다 Text·Tooltip 을 적는다.
/// 둘 다 적는다 — 넓은 도구줄은 Tooltip 을, 접힌 드롭다운은 Text 를 보인다. 없는 칸은 이름으로 찾아 null 이면 건너뛴다. 문화권은 건드리지 않는다.</summary>
internal static class HtmlEditorKorean
{
    /// <summary>묶음 이름표. 접힌 도구줄의 드롭다운 머리에 선다.</summary>
    private static readonly (string Name, string Text)[] Groups =
    [
        (HtmlEditorToolbarGroupNames.UndoRedo, "되돌리기"),
        (HtmlEditorToolbarGroupNames.Font, "글자"),
        (HtmlEditorToolbarGroupNames.Paragraph, "문단"),
        (HtmlEditorToolbarGroupNames.InsertElement, "넣기"),
        (HtmlEditorToolbarGroupNames.Table, "표"),
        (HtmlEditorToolbarGroupNames.Variable, "변수"),
    ];

    /// <summary>칸 하나의 이름표와 설명. 묶음은 적지 않고 도구줄 전체에서 이름으로 찾는다 — 묶음을 짐작하면 인용문·코드·표가 엉뚱한 묶음에서 찾아져 영어로 남는다.</summary>
    private static readonly (string Name, string Text, string Tooltip)[] Items =
    [
        (HtmlEditorToolbarItemNames.Undo, "실행 취소", "마지막으로 한 일을 되돌립니다."),
        (HtmlEditorToolbarItemNames.Redo, "다시 실행", "되돌린 일을 다시 합니다."),

        (HtmlEditorToolbarItemNames.FontName, "글꼴", "글꼴을 바꿉니다."),
        (HtmlEditorToolbarItemNames.FontSize, "글자 크기", "글자 크기를 바꿉니다."),
        (HtmlEditorToolbarItemNames.FontBold, "굵게", "고른 글자를 굵게 합니다."),
        (HtmlEditorToolbarItemNames.FontItalic, "기울임", "고른 글자를 기울입니다."),
        (HtmlEditorToolbarItemNames.FontUnderline, "밑줄", "고른 글자에 밑줄을 긋습니다."),
        (HtmlEditorToolbarItemNames.FontStrikethrough, "취소선", "고른 글자에 가로줄을 긋습니다."),
        (HtmlEditorToolbarItemNames.FontSubscript, "아래 첨자", "글줄 아래에 작은 글자를 답니다."),
        (HtmlEditorToolbarItemNames.FontSuperscript, "위 첨자", "글줄 위에 작은 글자를 답니다."),
        (HtmlEditorToolbarItemNames.FontColor, "글자 색", "글자 색을 바꿉니다."),
        (HtmlEditorToolbarItemNames.HighlightText, "형광펜", "고른 글자에 형광펜을 칠합니다."),
        (HtmlEditorToolbarItemNames.ClearFormatting, "서식 지우기", "고른 글자의 서식을 모두 지웁니다."),

        (HtmlEditorToolbarItemNames.ParagraphStyles, "문단 모양", "문단 모양을 바꿉니다."),
        (HtmlEditorToolbarItemNames.ParagraphAlignmentLeft, "왼쪽 맞춤", "문단을 왼쪽에 맞춥니다."),
        (HtmlEditorToolbarItemNames.ParagraphAlignmentCenter, "가운데 맞춤", "문단을 가운데에 맞춥니다."),
        (HtmlEditorToolbarItemNames.ParagraphAlignmentRight, "오른쪽 맞춤", "문단을 오른쪽에 맞춥니다."),
        (HtmlEditorToolbarItemNames.ParagraphAlignmentJustify, "양쪽 맞춤", "문단을 양쪽 끝에 맞춥니다."),
        (HtmlEditorToolbarItemNames.BulletList, "글머리 기호", "글머리 기호 목록을 시작합니다."),
        (HtmlEditorToolbarItemNames.NumberedList, "번호 매기기", "번호 목록을 시작합니다."),
        (HtmlEditorToolbarItemNames.IncreaseIndent, "들여쓰기", "문단을 한 칸 들여씁니다."),
        (HtmlEditorToolbarItemNames.DecreaseIndent, "내어쓰기", "문단을 한 칸 내어씁니다."),
        (HtmlEditorToolbarItemNames.InsertBlockquote, "인용문", "인용문 덩이를 넣습니다."),
        (HtmlEditorToolbarItemNames.InsertCodeBlock, "코드", "코드 덩이를 넣습니다."),

        (HtmlEditorToolbarItemNames.ShowHyperlinkDialog, "링크", "웹 쪽으로 가는 링크를 겁니다."),
        (HtmlEditorToolbarItemNames.ShowInsertPictureDialog, "그림", "그림을 넣습니다."),
        (HtmlEditorToolbarItemNames.ShowInsertTableDialog, "표", "표를 넣습니다."),

        (HtmlEditorToolbarItemNames.InsertTableRowAbove, "위에 줄 넣기", "고른 줄 위에 줄을 더합니다."),
        (HtmlEditorToolbarItemNames.InsertTableRowBelow, "아래에 줄 넣기", "고른 줄 아래에 줄을 더합니다."),
        (HtmlEditorToolbarItemNames.InsertTableColumnToTheLeft, "왼쪽에 칸 넣기", "고른 칸 왼쪽에 칸을 더합니다."),
        (HtmlEditorToolbarItemNames.InsertTableColumnToTheRight, "오른쪽에 칸 넣기", "고른 칸 오른쪽에 칸을 더합니다."),
        (HtmlEditorToolbarItemNames.InsertTableHeaderRow, "머리글 줄 넣기", "표 맨 위에 머리글 줄을 더합니다."),
        (HtmlEditorToolbarItemNames.DeleteTableRow, "줄 지우기", "고른 줄을 지웁니다."),
        (HtmlEditorToolbarItemNames.DeleteTableColumn, "칸 지우기", "고른 칸을 지웁니다."),
        (HtmlEditorToolbarItemNames.DeleteTable, "표 지우기", "표를 통째로 지웁니다."),

        (HtmlEditorToolbarItemNames.InsertVariableField, "변수", "변수 자리를 넣습니다."),
    ];

    /// <summary>문단 모양 안에 담기는 것들. 이것만 이름이 아니라 글자로 찾는다 — 드롭다운 안쪽에는 DevExpress 이름 상수가 없다.</summary>
    private static readonly Dictionary<string, string> ParagraphStyles = new(StringComparer.Ordinal)
    {
        ["Normal text"] = "본문",
        ["Heading 1"] = "제목 1",
        ["Heading 2"] = "제목 2",
        ["Heading 3"] = "제목 3",
        ["Heading 4"] = "제목 4",
        ["Heading 5"] = "제목 5",
        ["Heading 6"] = "제목 6",
    };

    /// <summary>도구줄 하나에 한국어 이름표를 입힌다.</summary>
    public static void Apply(IToolbar toolbar)
    {
        foreach (var (name, text) in Groups)
        {
            if (toolbar.Groups[name] is { } group)
            {
                group.Text = text;
            }
        }

        // 묶음을 가리지 않고 모든 칸을 훑는다 — 이름은 도구줄 안에서 하나뿐이다.
        var labels = Items.ToDictionary(x => x.Name, x => (x.Text, x.Tooltip), StringComparer.Ordinal);

        for (var g = 0; g < toolbar.Groups.Count; g++)
        {
            var items = toolbar.Groups[g].Items;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (item.Name is { } name && labels.TryGetValue(name, out var label))
                {
                    item.Text = label.Text;
                    item.Tooltip = label.Tooltip;
                }
            }
        }

        TranslateParagraphStyles(toolbar);
    }

    /// <summary>문단 모양 드롭다운 안쪽. 칸이 콤보로 설 때와 드롭다운으로 설 때 둘 다 본다.</summary>
    private static void TranslateParagraphStyles(IToolbar toolbar)
    {
        switch (Find(toolbar, HtmlEditorToolbarItemNames.ParagraphStyles))
        {
            case IBarComboBox combo:
                for (var i = 0; i < combo.Items.Count; i++)
                {
                    if (ParagraphStyles.TryGetValue(combo.Items[i].Text ?? string.Empty, out var ko))
                    {
                        combo.Items[i].Text = ko;
                    }
                }

                break;

            case IBarDropDown drop:
                for (var i = 0; i < drop.Items.Count; i++)
                {
                    if (ParagraphStyles.TryGetValue(drop.Items[i].Text ?? string.Empty, out var ko))
                    {
                        drop.Items[i].Text = ko;
                    }
                }

                break;
        }
    }

    /// <summary>도구줄 전체에서 이름으로 칸 하나를 찾는다. 없으면 <c>null</c>.</summary>
    private static IBarItem? Find(IToolbar toolbar, string name)
    {
        for (var g = 0; g < toolbar.Groups.Count; g++)
        {
            if (toolbar.Groups[g].Items[name] is { } item)
            {
                return item;
            }
        }

        return null;
    }
}
