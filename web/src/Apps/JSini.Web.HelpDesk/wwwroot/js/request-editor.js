// 요청 글 편집기(`DxHtmlEditor`)에 **클립보드 그림**을 붙일 수 있게 하는 자리.
//
// [무엇을 막으려고 있는가]
//
// 편집기에 그림을 붙여넣으면 그것이 본문에 `data:image/png;base64,…` 로
// 박힌다. 화면에는 잘 보이므로 다 된 것처럼 보이지만 실제로는 셋이 깨진다.
//
//   1. 화면 캡처 한 장이 흔히 1~3MB 다. base64 는 거기에 1/3 을 더한다.
//      그 글자가 회로(SignalR)로 오가는데 수신 한도가 4MB 이고,
//      넘으면 오류가 아니라 **회로가 그냥 끊긴다** — 쓰던 글이 통째로 날아간다.
//   2. 서버가 그 data URI 를 받아 배포 장비 디스크에 떨군다
//      (`FileUtil.SaveImageToFile`). 결정 D5-B 가 그만두기로 한 방식이고,
//      컨테이너 안에서는 그 경로가 재배포 때 사라진다.
//   3. 본문 자체가 수 MB 짜리 글자가 되어 목록·검색·메일이 전부 무거워진다.
//
// 그래서 **붙여넣는 그 순간** 파일로 보내고, 본문에는 주소만 남긴다.
//
// [흐름]
//
//   붙여넣기 → (여기) 편집기에 들어가기 전에 가로챈다
//            → 숨겨 둔 <InputFile> 에 밀어 넣고 change 를 쏜다
//            → 화면(Razor)이 그 바이트를 받아 헬프데스크로 올린다
//            → 받은 주소로 여기 `insertImage` 가 <img> 를 꽂는다
//
// 바이트를 회로에 글자로 실어 보내지 않고 `InputFile` 로 보내는 것이 요점이다.
// 그쪽은 Blazor 가 조각내어 흘려보내므로 위 1번 한도에 걸리지 않는다.
//
// [도구줄의 「그림」 단추도 이 길로 온다]
//
// 그 단추는 편집기 제 판을 띄워 고른 파일을 base64 로 본문에 꽂는다. 그래서
// 위 셋을 고스란히 밟는데, **휴대폰에서 특히 그렇다** — 사진 한 장이 화면
// 캡처보다 훨씬 크기 때문이다. 재어 본 값: 4.3MB 짜리 사진을 그 판으로 고르면
// 본문이 **5,762,175 글자**가 된다. 위 1번 한도(4MB)의 한 배 반이다.
//
// 한 가지가 더 있다. DevExpress 는 그림을 꽂을 자리를 `getSelection().index` 로
// **비었는지 보지 않고** 읽는다(`applyPicture` — 같은 파일의 다른 명령들은
// 본다). 휴대폰은 글칸을 짚지 않은 채 도구줄을 누르는 것이 보통이라 그 자리가
// 빌 수 있고, 그러면 브라우저 쪽 예외라 회로가 끊긴다. 재현해 보지는 못했다 —
// 고치는 길이 어차피 같아서 여기 적어만 둔다.
//
// 그래서 그 단추의 누름을 가로채 같은 파일 칸을 연다. 화면(Razor)이 도구줄의
// 그 단추에 이름표(`hd-pick-image`)를 붙여 주고, 여기서는 그 이름으로 찾는다.
//
//   **여는 일을 회로로 넘기지 않는다.** 파일 고르개는 사람이 누른 그 손짓
//   안에서 열어야 브라우저가 열어 준다. 서버로 갔다 오면 그 손짓이 이미 끝난
//   뒤라 휴대폰에서 조용히 막힌다.

/// 편집기마다 「붙여넣기 직전의 글자 자리」를 기억해 둔다.
///
/// 올리는 동안 사람이 딴 곳을 누르면 커서가 옮겨 간다. 그때 그 자리에 그림을
/// 꽂으면 엉뚱한 문단 한가운데에 들어간다. 그래서 **가로챈 순간의 자리**를
/// 들고 있다가 그 자리에 꽂는다.
const carets = new WeakMap();

/// 편집기의 글자 치는 칸. DevExpress 안쪽 이름을 짚지 않는다 —
/// 판올림마다 바뀔 자리이고, 우리가 원하는 것은 `contenteditable` 하나다.
///
/// **찾는 시점을 미룬다.** 편집기가 그 칸을 다시 만드는 일이 있어서,
/// 걸 때 잡아 둔 참조를 들고 있으면 어느 순간 화면에 없는 것을 가리킨다.
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

    // 파일 목록이 비어 있어도 items 에는 들어 있는 브라우저가 있다
    // (캡처 도구에서 바로 복사한 경우가 그렇다).
    if (!data.items) return [];

    return Array.from(data.items)
        .filter(i => i.kind === 'file' && i.type && i.type.startsWith('image/'))
        .map(i => i.getAsFile())
        .filter(Boolean);
}

/// 붙여넣기·끌어놓기를 가로채 숨긴 파일 칸으로 넘긴다.
function hand(event, data, box, inputId, point) {
    const area = areaIn(box);

    // 편집기 껍데기 안에는 글 쓰는 칸 말고도 입력칸이 있다(링크 판 따위).
    // 거기에 붙여넣는 것까지 가로채면 안 된다.
    if (!area || !area.contains(event.target)) return;

    const images = imagesOf(data);

    // **그림이 없으면 손대지 않는다.** 글자 붙여넣기는 편집기가 훨씬 잘한다
    // (서식 정리·목록·표). 여기서 가로채면 그게 전부 사라진다.
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

/// 도구줄 「그림」 단추의 누름을 가로채 숨긴 파일 칸을 연다.
///
/// [**문서에 건다** — 껍데기가 아니다]
///
/// 좁은 화면에서는 도구줄이 접혀 그 단추가 넘침 메뉴로 옮겨 가고, 그 메뉴는
/// 편집기 바깥에 그려진다. 껍데기에만 걸면 **휴대폰에서 바로 그 경우를 놓친다** —
/// 고치려던 자리가 그 자리다.
///
/// 잡는 단계로 받는 것도 같은 까닭이다. Blazor 는 누름을 문서에서 거품 단계로
/// 받으므로, 여기서 멈추면 편집기의 판이 아예 열리지 않는다.
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

        // 지금 글자 자리가 편집기 안이면 그것을 쓴다. 아니면 따라다니며
        // 적어 둔 마지막 자리가 남아 있고, 그것도 없으면 `insertImage` 가
        // 글 끝에 붙인다.
        const area = areaIn(box);
        const live = area ? caretAt(area, null) : null;
        if (area && live) carets.set(area, live);

        // 같은 사진을 두 번 고를 수 있어야 한다. 값이 남아 있으면 두 번째
        // 고르기에서 change 가 오지 않는다.
        input.value = '';
        input.click();
    };

    document.addEventListener('click', onClick, true);
}

/// 글자 자리를 **미리** 적어 둔다.
///
/// 도구줄 단추를 누르면 편집기가 초점을 잃고, 휴대폰은 자판이 내려가면서
/// 고른 자리까지 지운다. 그 뒤에 물으면 늦다 — 눌리기 전부터 따라다닌다.
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

/// 편집기에 붙여넣기·끌어놓기 처리기를 건다. 이미 걸려 있으면 그냥 참.
///
/// 화면이 다시 그려질 때마다 불러도 된다 — 걸렸다는 표시를 껍데기에 남긴다.
/// 편집기가 통째로 새로 만들어지면 그 표시도 함께 사라지므로 다시 걸린다.
///
/// [**글 쓰는 칸이 아니라 껍데기에 건다** — 여기가 갈리는 지점이다]
///
/// 한 요소에 걸린 처리기들은 **그 요소가 과녁일 때 등록 순서대로** 불린다.
/// 잡는 단계로 걸어도 그 순서는 바뀌지 않는다 — 편집기가 먼저 걸어 두었으므로
/// 같은 칸에 걸면 우리 차례는 **언제나 두 번째**이고, 그때는 이미 편집기가
/// 그림을 base64 로 박은 뒤다. 한 칸 바깥(껍데기)에서 잡는 단계로 받으면
/// 과녁에 닿기 전이라 반드시 먼저 불린다.
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
        // 자리를 잃었으면 글 끝에 붙인다. 아무 데도 안 넣는 것보다 낫다 —
        // 올라간 파일만 남고 사람은 그림이 사라진 줄 안다.
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

    // 편집기에게 「바뀌었다」고 알린다. 우리가 DOM 을 직접 건드렸으므로
    // 이것이 없으면 편집기의 실행취소 이력과 어긋난 채로 남는다.
    area.dispatchEvent(new Event('input', { bubbles: true }));

    return true;
}

/// **지금 화면에 있는 글**을 그대로 읽는다.
///
/// `DxHtmlEditor` 는 값을 파라미터로 돌려주지 않는다 — `MarkupChanged` 로
/// 알려 줄 뿐이고 그 알림은 친 다음 잠깐 뒤에 온다(`InputDelay`, 기본 500ms).
/// 그래서 마지막으로 친 글이 아직 서버로 안 간 채 「등록」이 돌 수 있다.
/// 게다가 여기서는 **우리가 DOM 에 직접 꽂은 `<img>`** 도 있어서, 편집기의
/// 모형을 거치지 않고 보이는 것을 그대로 읽는 편이 확실하다.
///
/// 관리자 모듈의 `mail-editor.js` 와 같은 일을 한다. 합치지 않는 것은
/// 「두 모듈이 쓰면 복제, 세 번째부터 승격」(web/CLAUDE.md) 때문이다.
export function readMarkup(editorSelector) {
    const area = areaOf(editorSelector);
    if (!area) return null;

    // 빈 편집기는 브라우저마다 `<p><br></p>` 같은 뼈대를 남긴다. 그것을 본문으로
    // 넘기면 서버의 「내용이 필요합니다」를 지나쳐 **빈 글**이 등록된다.
    // 그림만 있고 글자가 없는 것은 빈 글이 아니므로 그때는 남긴다.
    const empty = area.innerText.trim() === '' && area.querySelectorAll('img').length === 0;

    return empty ? '' : area.innerHTML;
}
