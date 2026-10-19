// RAG 문서함 — 파일을 끌어다 놓아 올린다. Blazor 의 `InputFile` 은 `change` 만 듣는다. 끌어다 놓은 파일은 `drop` 이벤트의
// `dataTransfer.files` 에 담겨 오는데, **Blazor 쪽 DragEventArgs 로는 그 FileList 를 받을 수 없다.** 그래서 여기서 받아 숨겨 둔 `

const wired = new WeakMap();

// 끌고 오는 것이 **파일인가**. 이 화면에서는 문서 줄도 끌 수 있어서, 안 가르면 줄을 집어 올리는 것만으로 "여기에 놓으면 올라갑니다" 가 번쩍인다.
// `dataTransfer.files` 로는 못 가른다 — 끄는 동안(dragover/dragenter)에는 규격상 그 목록이 비어 있고, 내용은 떨군 뒤에야 읽을 수 있다.
function carriesFiles(e) {
    const t = e.dataTransfer?.types;
    return !!t && Array.prototype.indexOf.call(t, 'Files') !== -1;
}

export function wire(zone, input) {
    if (!zone || !input || wired.has(zone)) return;

    let depth = 0;   // 자식 위로 지나가도 깜빡이지 않게 센다

    const paint = (on) => zone.classList.toggle('ad-rag__drop--over', on);

    const over = (e) => {
        if (!carriesFiles(e)) return;   // 문서 줄을 끄는 중 — 그쪽 일은 Blazor 가 받는다
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';

        // **떨구는 내내 다시 칠한다.** dragenter 를 한 번 놓치면(자식이 빠르게 갈리는 동안 일어난다) 테가 안 뜬 채로 끝까지 간다.
        if (depth === 0) depth = 1;
        paint(true);
    };

    const enter = (e) => {
        if (!carriesFiles(e)) return;
        e.preventDefault();
        if (++depth === 1) paint(true);
    };

    const leave = (e) => {
        if (!carriesFiles(e)) return;
        if (--depth <= 0) { depth = 0; paint(false); }
    };

    const drop = (e) => {
        const files = e.dataTransfer?.files;

        depth = 0;
        paint(false);

        // 문서 줄을 떨군 것이면 여기서 손을 뗀다 — 막으면 Blazor 의 `ondrop` 까지 같이 죽어서 **분류로 옮기는 길이 통째로 막힌다.**
        if (!files || files.length === 0) return;

        e.preventDefault();
        input.files = files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    };

    // 창 밖으로 끌고 나가거나 다른 데 떨구면 `dragleave` 가 안 오는 일이 있다. 그러면 **테가 켜진 채로 남아** 떨구지도 않았는데 기다리는 것처럼 보인다.
    const bail = () => { depth = 0; paint(false); };

    zone.addEventListener('dragover', over);
    zone.addEventListener('dragenter', enter);
    zone.addEventListener('dragleave', leave);
    zone.addEventListener('drop', drop);
    document.addEventListener('dragend', bail);
    document.addEventListener('drop', bail);

    wired.set(zone, { over, enter, leave, drop, bail });
}

export function unwire(zone) {
    const h = zone && wired.get(zone);
    if (!h) return;
    zone.removeEventListener('dragover', h.over);
    zone.removeEventListener('dragenter', h.enter);
    zone.removeEventListener('dragleave', h.leave);
    zone.removeEventListener('drop', h.drop);
    document.removeEventListener('dragend', h.bail);
    document.removeEventListener('drop', h.bail);
    wired.delete(zone);
}
