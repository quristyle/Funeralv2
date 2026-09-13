// 메일 본문 편집기에서 **지금 화면에 있는 글**을 그대로 읽는다.
//
// [왜 필요한가]
//
// `DxHtmlEditor` 는 값을 파라미터로 돌려주지 않는다 — `MarkupChanged` 로
// 알려 줄 뿐이고, 그 알림은 **친 다음 잠깐 뒤에** 온다(`InputDelay`, 기본 500ms).
// 그래서 마지막으로 친 글이 아직 서버로 안 간 채 「보내기」가 돌 수 있고,
// 그때 나가는 메일은 **본문이 비어 있다.**
//
// 부품에는 「지금 값을 내놔라」가 없다(26.1.4 에서 public 메서드가 없다).
// 그래서 화면에 있는 것을 직접 읽는다.
//
// **DevExpress 내부(`DevExpress.ui.dxHtmlEditor.getInstance`)를 건드리지 않는다.**
// 그쪽이 정본에 가깝지만 판올림마다 깨질 자리이고, 여기서 원하는 것은
// 「사람이 보고 있는 글」이라 편집 영역의 innerHTML 이면 충분하다.
export function readMarkup(selector) {
    const box = document.querySelector(selector);
    if (!box) return null;

    const area = box.querySelector('[contenteditable="true"]');
    if (!area) return null;

    const html = area.innerHTML;

    // 빈 편집기는 브라우저마다 `<p><br></p>` 같은 뼈대를 남긴다. 그것을
    // 본문으로 넘기면 **서버의 「본문이 필요합니다」를 지나쳐** 빈 메일이 나간다.
    return area.innerText.trim() === '' ? '' : html;
}
