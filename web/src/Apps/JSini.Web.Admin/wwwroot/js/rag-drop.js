// RAG 문서함 — 파일을 끌어다 놓아 올린다.
//
// [왜 JS 가 필요한가]
//
// Blazor 의 `InputFile` 은 `change` 만 듣는다. 끌어다 놓은 파일은 `drop` 이벤트의
// `dataTransfer.files` 에 담겨 오는데, **Blazor 쪽 DragEventArgs 로는 그 FileList
// 를 받을 수 없다.** 그래서 여기서 받아 숨겨 둔 `input` 에 꽂고 `change` 를
// 일으킨다 — 그러면 그다음은 Blazor 가 평소대로 처리한다.
//
// [막아야 하는 기본 동작]
//
// 브라우저는 파일을 창에 떨구면 **그 파일로 창을 바꿔 버린다**(PDF 를 떨구면
// 화면이 PDF 뷰어가 된다). 그래서 dragover 에서도 preventDefault 를 해야 한다 —
// drop 에서만 막으면 늦는다.

const wired = new WeakMap();

export function wire(zone, input) {
    if (!zone || !input || wired.has(zone)) return;

    let depth = 0;   // 자식 위로 지나가도 깜빡이지 않게 센다

    const over = (e) => { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; };

    const enter = (e) => {
        e.preventDefault();
        if (++depth === 1) zone.classList.add('ad-rag__drop--over');
    };

    const leave = () => {
        if (--depth <= 0) { depth = 0; zone.classList.remove('ad-rag__drop--over'); }
    };

    const drop = (e) => {
        e.preventDefault();
        depth = 0;
        zone.classList.remove('ad-rag__drop--over');

        const files = e.dataTransfer?.files;
        if (!files || files.length === 0) return;

        input.files = files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    };

    zone.addEventListener('dragover', over);
    zone.addEventListener('dragenter', enter);
    zone.addEventListener('dragleave', leave);
    zone.addEventListener('drop', drop);

    wired.set(zone, { over, enter, leave, drop });
}

export function unwire(zone) {
    const h = zone && wired.get(zone);
    if (!h) return;
    zone.removeEventListener('dragover', h.over);
    zone.removeEventListener('dragenter', h.enter);
    zone.removeEventListener('dragleave', h.leave);
    zone.removeEventListener('drop', h.drop);
    wired.delete(zone);
}
