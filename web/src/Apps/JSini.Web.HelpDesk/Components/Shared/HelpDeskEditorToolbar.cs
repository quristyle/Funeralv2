using DevExpress.Blazor;
using DevExpress.Blazor.Office;

namespace JSini.Web.HelpDesk.Components.Shared;

/// <summary>헬프데스크 서식 편집기(요청 본문 · 댓글)가 같이 쓰는 도구줄. 기본 묶음을 걷고 아홉 단추 묶음 하나만 세운다.
/// 감추기(GetVisible)가 아니라 걷는다 — 감춘 묶음도 그림 묶음에 실려 나간다. 기본 도구줄(묶음 여섯 · 드롭다운 열다섯)은 그림 묶음이 300KB 를 넘어
/// 화면이 서기까지 브라우저가 1초 가까이 멎었고, 접어 두었다 펴는 댓글 칸에서는 펴는 순간 접힘 계산으로 다시 멎었다. 넘침 단추가 안 생기는 수라 DevExpress 드롭다운(포털 안에서 안 열린다)도 없다.</summary>
internal static class HelpDeskEditorToolbar
{
    /// <summary>도구줄에 세우는 명령 아홉. 폭과 상관없이 이것뿐이고 차례가 곧 화면의 차례다.
    /// 글꼴·색·첨자·표·맞춤은 뺐다 — 이 글들은 서식이 아니라 캡처와 몇 줄이다.</summary>
    private static readonly string[] Items =
    [
        HtmlEditorToolbarItemNames.Undo,
        HtmlEditorToolbarItemNames.Redo,
        HtmlEditorToolbarItemNames.FontBold,
        HtmlEditorToolbarItemNames.FontUnderline,
        HtmlEditorToolbarItemNames.ClearFormatting,
        HtmlEditorToolbarItemNames.BulletList,
        HtmlEditorToolbarItemNames.NumberedList,
        HtmlEditorToolbarItemNames.ShowHyperlinkDialog,
        HtmlEditorToolbarItemNames.ShowInsertPictureDialog,
    ];

    /// <summary>도구줄을 세우고 「그림」 단추에 <paramref name="pictureButtonClass"/> 를 단다 — JS 가 이 이름으로 누름을 가로챈다.
    /// 빠뜨리면 편집기 제 판이 열려 사진이 base64 로 박힌다.
    /// <paramref name="full"/> 이면 기본 도구줄을 그대로 둔다 — 사람이 스스로 고른 때만 쓴다(그만큼 화면이 늦게 선다).</summary>
    public static void Apply(IToolbar toolbar, string pictureButtonClass, bool full = false)
    {
        if (full)
        {
            var picture = toolbar.Groups[HtmlEditorToolbarGroupNames.InsertElement]
                ?.Items[HtmlEditorToolbarItemNames.ShowInsertPictureDialog];

            if (picture is not null)
            {
                picture.CssClass = pictureButtonClass;
            }

            HtmlEditorKorean.Apply(toolbar);
            return;
        }

        toolbar.Groups.Clear();

        var group = toolbar.Groups.AddCustomGroup();

        foreach (var name in Items)
        {
            var item = group.Items.Add(name);

            if (name == HtmlEditorToolbarItemNames.ShowInsertPictureDialog)
            {
                item.CssClass = pictureButtonClass;
            }
        }

        // 한국어 이름표가 CssClass 를 덮지 않는다 — Text·Tooltip 만 적는다.
        HtmlEditorKorean.Apply(toolbar);
    }
}
