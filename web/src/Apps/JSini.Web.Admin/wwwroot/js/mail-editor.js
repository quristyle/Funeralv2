// 메일 본문 편집기에서 **지금 화면에 있는 글**을 그대로 읽는다. `DxHtmlEditor` 는 값을 파라미터로 돌려주지 않는다 — `MarkupChanged` 로 알려 줄
// 뿐이고, 그 알림은 **친 다음 잠깐 뒤에** 온다(`InputDelay`, 기본 500ms).
export function readMarkup(selector) {
    const box = document.querySelector(selector);
    if (!box) return null;

    const area = box.querySelector('[contenteditable="true"]');
    if (!area) return null;

    const html = area.innerHTML;

    // 빈 편집기는 브라우저마다 `<p><br></p>` 같은 뼈대를 남긴다. 그것을 본문으로 넘기면 **서버의 「본문이 필요합니다」를 지나쳐** 빈 메일이 나간다.
    return area.innerText.trim() === '' ? '' : html;
}
