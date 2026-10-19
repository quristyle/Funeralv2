// 요청 글 편집기(DxHtmlEditor)에 클립보드 그림을 붙이는 자리. 그림을 본문에 data URI 로 박으면 회로가 4MB 수신 한도에서 끊기고 배포 디스크에 떨어진다.
// 그래서 붙여넣기·끌어놓기·「그림」 단추를 가로채 숨긴 파일 칸(InputFile)으로 보내고, 본문에는 받은 주소만 꽂는다.
// 파일 고르개는 사람이 누른 손짓 안에서 열어야 하므로 그 단추의 누름은 회로로 넘기지 않고 여기서 연다.

/// 편집기마다 붙여넣기 직전의 글자 자리를 기억해 두고 가로챈 그 자리에 그림을 꽂는다 — 올리는 동안 커서가 옮겨 가도 엉뚱한 곳에 들어가지 않는다.
const carets = new WeakMap();

/// 편집기의 글 치는 칸(contenteditable). 찾는 것은 미룬다 — 편집기가 그 칸을 다시 만들 수 있다.
function areaIn(box) {
    return box ? box.querySelector('[contenteditable="true"]') : null;
}

function areaOf(selector) {
    return areaIn(document.querySelector(selector));
}

/// 클립보드(또는 끌어놓기)에 든 그림 파일들.
function imagesOf(data) {
    if (!data) return [];

    const files = data.files ? Array.from(data.files) : [];
    const picked = files.filter(f => f && f.type && f.type.startsWith('image/'));
    if (picked.length > 0) return picked;

    // 파일 목록이 비어 있어도 items 에는 든 브라우저가 있다(캡처 도구에서 복사한 경우).
    if (!data.items) return [];

    return Array.from(data.items)
        .filter(i => i.kind === 'file' && i.type && i.type.startsWith('image/'))
        .map(i => i.getAsFile())
        .filter(Boolean);
}

/// 붙여넣기·끌어놓기를 가로채 숨긴 파일 칸으로 넘긴다.
function hand(event, data, box, inputId, point) {
    const area = areaIn(box);

    // 편집기 껍데기 안의 다른 입력칸(링크 판 등)에 붙여넣는 것은 가로채지 않는다.
    if (!area || !area.contains(event.target)) return;

    const images = imagesOf(data);

    // 그림이 없으면 손대지 않는다 — 글자 붙여넣기는 편집기가 서식까지 잘 처리한다.
    if (images.length === 0) return;

    const input = document.getElementById(inputId);
    if (!input) return;

    event.preventDefault();
    event.stopImmediatePropagation();

    carets.set(area, caretAt(area, point));

    const transfer = new DataTransfer();
    for (const image of images) {
        transfer.items.add(image);
    }

    input.files = transfer.files;
    input.dispatchEvent(new Event('change', { bubbles: true }));
}

/// 도구줄 「그림」 단추의 누름을 가로채 숨긴 파일 칸을 연다. 문서에 건다 — 좁은 화면에서는 단추가 편집기 바깥 넘침 메뉴로 나가 껍데기에서는 놓친다.
function watchPictureButton(box, inputId, pictureSelector) {
    const onClick = event => {
        // 편집기가 사라졌다. 손을 뗀다 — 화면을 옮길 때마다 하나씩 쌓인다.
        if (!box.isConnected) {
            document.removeEventListener('click', onClick, true);
            return;
        }

        const target = event.target instanceof Element ? event.target : null;
        if (!target || !target.closest(pictureSelector)) return;

        const input = document.getElementById(inputId);
        if (!input) return;

        event.preventDefault();
        event.stopImmediatePropagation();

        // 글자 자리가 편집기 안이면 그것을 쓰고, 아니면 적어 둔 마지막 자리, 그것도 없으면 insertImage 가 글 끝에 붙인다.
        const area = areaIn(box);
        const live = area ? caretAt(area, null) : null;
        if (area && live) carets.set(area, live);

        // 같은 사진을 두 번 고를 수 있도록 값을 비운다 — 남아 있으면 두 번째 고르기에서 change 가 오지 않는다.
        input.value = '';
        input.click();
    };

    document.addEventListener('click', onClick, true);
}

/// 글자 자리를 미리 적어 둔다 — 도구줄 단추를 누르면 편집기가 초점을 잃고 휴대폰은 고른 자리까지 지워서, 눌리기 전부터 따라다닌다.
function watchCaret(box) {
    const onSelectionChange = () => {
        if (!box.isConnected) {
            document.removeEventListener('selectionchange', onSelectionChange);
            return;
        }

        const area = areaIn(box);
        if (!area) return;

        const range = caretAt(area, null);
        if (range) carets.set(area, range);
    };

    document.addEventListener('selectionchange', onSelectionChange);
}

/// 꽂을 자리. 끌어놓기는 놓은 지점이고, 붙여넣기는 지금 커서다.
function caretAt(area, point) {
    if (point && document.caretRangeFromPoint) {
        const range = document.caretRangeFromPoint(point.x, point.y);
        if (range && area.contains(range.startContainer)) {
            return range;
        }
    }

    const selection = window.getSelection();
    if (selection && selection.rangeCount > 0) {
        const range = selection.getRangeAt(0);
        if (area.contains(range.startContainer)) {
            return range.cloneRange();
        }
    }

    return null;
}

/// 편집기에 붙여넣기·끌어놓기 처리기를 건다. 이미 걸려 있으면 그냥 참. 화면이 다시 그려질 때마다 불러도 된다.
/// 글칸이 아니라 껍데기에 잡는 단계로 건다 — 글칸에 걸면 편집기가 먼저 그림을 base64 로 박은 뒤라 늦다.
export function bind(editorSelector, inputId, pictureSelector) {
    const box = document.querySelector(editorSelector);

    if (!box || !areaIn(box) || !document.getElementById(inputId)) return false;
    if (box.dataset.hdPasteBound === '1') return true;

    box.dataset.hdPasteBound = '1';

    box.addEventListener('paste', e => hand(e, e.clipboardData, box, inputId, null), true);
    box.addEventListener('drop', e => hand(e, e.dataTransfer, box, inputId, { x: e.clientX, y: e.clientY }), true);

    watchCaret(box);

    if (pictureSelector) {
        watchPictureButton(box, inputId, pictureSelector);
    }

    return true;
}

/// 올려 둔 그림을 아까 그 자리에 꽂는다.
export function insertImage(editorSelector, url, alt) {
    const area = areaOf(editorSelector);
    if (!area) return false;

    area.focus();

    const selection = window.getSelection();
    const saved = carets.get(area);

    if (saved && area.contains(saved.startContainer)) {
        selection.removeAllRanges();
        selection.addRange(saved);
    }

    let range;
    if (selection.rangeCount > 0 && area.contains(selection.getRangeAt(0).startContainer)) {
        range = selection.getRangeAt(0);
    } else {
        // 자리를 잃었으면 글 끝에 붙인다 — 아무 데도 안 넣는 것보다 낫다. 올라간 파일만 남고 사람은 그림이 사라진 줄 안다.
        range = document.createRange();
        range.selectNodeContents(area);
        range.collapse(false);
    }

    range.deleteContents();

    const image = document.createElement('img');
    image.setAttribute('src', url);
    image.setAttribute('alt', alt || '');
    range.insertNode(image);

    // 커서를 그림 뒤로 옮겨 둔다. 안 그러면 이어 치는 글이 그림 앞에 쌓인다.
    range.setStartAfter(image);
    range.collapse(true);
    selection.removeAllRanges();
    selection.addRange(range);

    carets.set(area, range.cloneRange());

    // 편집기에 「바뀌었다」고 알린다 — DOM 을 직접 건드렸으므로 알리지 않으면 실행취소 이력과 어긋난다.
    area.dispatchEvent(new Event('input', { bubbles: true }));

    return true;
}

/// 지금 화면의 글을 그대로 읽는다. MarkupChanged 는 친 뒤 InputDelay(기본 500ms) 뒤에 와서 마지막 글이 빠질 수 있다.
/// 우리가 DOM 에 직접 꽂은 img 도 보이는 그대로 읽어야 하므로 편집기 모형을 거치지 않는다. 관리자 모듈의 mail-editor.js 와 같은 일이지만 합치지 않는다(web/CLAUDE.md 의 복제 규칙).
export function readMarkup(editorSelector) {
    const area = areaOf(editorSelector);
    if (!area) return null;

    // 빈 편집기가 남기는 <p><br></p> 같은 뼈대는 빈 글로 보고 넘기지 않는다 — 그림만 있는 글은 빈 글이 아니다.
    const empty = area.innerText.trim() === '' && area.querySelectorAll('img').length === 0;

    return empty ? '' : area.innerHTML;
}

/// 본문 맨 위에 HTML 조각을 꽂는다(AI 요약). _content 에 이어 붙이지 않는다 — Markup 을 밀어 넣으면 편집기가 글을 통째로 다시 그려 커서와 실행취소가 날아간다.
/// DOM 을 직접 만지고 편집기에 바뀌었다고 알린다. 그림은 고르던 자리에, 요약은 언제나 맨 위에 꽂는다.
export function prependHtml(editorSelector, html) {
    const area = areaOf(editorSelector);
    if (!area || !html) return false;

    const holder = document.createElement('div');
    holder.innerHTML = html;

    // 조각이 여러 마디라 통째로 옮긴다 — innerHTML 앞에 글자를 이어 붙이면 편집기가 든 노드가 바뀌어 커서가 사라진다.
    const fragment = document.createDocumentFragment();
    while (holder.firstChild) fragment.appendChild(holder.firstChild);

    area.insertBefore(fragment, area.firstChild);

    area.dispatchEvent(new Event('input', { bubbles: true }));

    return true;
}
