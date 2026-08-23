/**
 * [준수사항 3] ant-design-vue 모달을 헤더로 끌어 옮길 수 있게 만든다.
 *
 * ant 의 `<Modal>` 은 드래그를 지원하지 않는다. 화면마다 `:modal-render` 를 붙여
 * 직접 구현하는 방법이 흔히 쓰이지만, 이 저장소에는 ant 모달을 쓰는 화면이 30곳이 넘고
 * 앞으로도 늘어난다. **한 곳에서 걸어 두는 편이 지켜지기 쉽다.**
 *
 * 그래서 앱이 뜰 때 한 번 DOM 을 지켜보다가, 모달이 나타나면 그 헤더를 손잡이로 만든다.
 * 화면 코드에는 아무것도 추가하지 않는다.
 *
 * 움직이는 대상은 `.ant-modal`(위치를 잡는 상자)이고, 값은 `transform` 으로만 바꾼다 —
 * ant 가 계산한 `top`·`margin` 을 건드리지 않아야 닫고 다시 열 때 제자리로 돌아온다.
 *
 * vben 모달(`useVbenModal`)은 부품이 이미 드래그를 지원한다(기본값을 켜 두었다).
 * Drawer(서랍)는 가장자리에 붙는 부품이라 대상이 아니다.
 */

/** 손잡이로 쓰지 않을 요소 — 헤더 안의 버튼 등 */
const NO_DRAG = 'button, a, input, select, textarea, [data-no-drag]';

/**
 * 손잡이가 될 수 있는 요소.
 *
 * 보통은 ant 가 그리는 헤더다. 다만 공지 팝업처럼 기본 헤더를 쓰지 않고
 * 제목 줄을 직접 그리는 팝업이 있어, `data-drag-handle` 을 붙이면 그것도 손잡이로 받는다.
 */
const HANDLE = '.ant-modal-header, [data-drag-handle]';

/** 이미 처리한 헤더를 다시 붙이지 않으려는 표시 */
const MARK = 'data-jsini-draggable';

interface Offset {
  x: number;
  y: number;
}

/** 모달 하나에 드래그를 건다. */
function attach(header: HTMLElement) {
  if (header.hasAttribute(MARK)) return;

  const modal = header.closest<HTMLElement>('.ant-modal');
  if (!modal) return;

  header.setAttribute(MARK, '');
  header.style.cursor = 'move';
  header.style.userSelect = 'none';

  const offset: Offset = { x: 0, y: 0 };

  const onPointerDown = (event: PointerEvent) => {
    // 헤더 안의 버튼(닫기 등)을 누른 것이면 드래그로 보지 않는다.
    if ((event.target as HTMLElement).closest(NO_DRAG)) return;
    if (event.button !== 0) return;

    const startX = event.clientX;
    const startY = event.clientY;
    const startOffset = { ...offset };

    // 전체화면 모달은 옮길 곳이 없다.
    const rect = modal.getBoundingClientRect();
    if (rect.width >= window.innerWidth - 1) return;

    const onPointerMove = (moveEvent: PointerEvent) => {
      const nextX = startOffset.x + (moveEvent.clientX - startX);
      const nextY = startOffset.y + (moveEvent.clientY - startY);

      // 창 밖으로 완전히 나가 버리면 다시 잡을 수 없다. 헤더가 보이는 만큼은 남긴다.
      const bounds = modal.getBoundingClientRect();
      const minX = -(bounds.left - startOffset.x) - bounds.width + 80;
      const maxX = window.innerWidth - (bounds.left - startOffset.x) - 80;
      const minY = -(bounds.top - startOffset.y);
      const maxY = window.innerHeight - (bounds.top - startOffset.y) - 40;

      offset.x = Math.min(maxX, Math.max(minX, nextX));
      offset.y = Math.min(maxY, Math.max(minY, nextY));
      modal.style.transform = `translate(${offset.x}px, ${offset.y}px)`;
    };

    const onPointerUp = () => {
      window.removeEventListener('pointermove', onPointerMove);
      window.removeEventListener('pointerup', onPointerUp);
    };

    window.addEventListener('pointermove', onPointerMove);
    window.addEventListener('pointerup', onPointerUp);
  };

  header.addEventListener('pointerdown', onPointerDown);

  // 같은 모달이 닫혔다 다시 열리면 원래 자리에서 시작해야 한다.
  // ant 는 열 때 .ant-modal-wrap 의 display 를 바꾸므로 그 시점에 위치를 되돌린다.
  const wrap = modal.closest<HTMLElement>('.ant-modal-wrap');
  if (wrap) {
    const observer = new MutationObserver(() => {
      if (wrap.style.display === 'none') {
        offset.x = 0;
        offset.y = 0;
        modal.style.transform = '';
      }
    });
    observer.observe(wrap, { attributeFilter: ['style'], attributes: true });
  }
}

/** 지금 화면에 있는 모달 전부에 건다. */
function attachAll(root: ParentNode = document) {
  root.querySelectorAll<HTMLElement>(HANDLE).forEach((header) => attach(header));
}

let started = false;

/**
 * 앱 시작 시 한 번 호출한다.
 * 모달은 body 아래에 나중에 붙으므로 DOM 변화를 계속 지켜본다.
 */
export function setupDraggableModal() {
  if (started || typeof window === 'undefined') return;
  started = true;

  attachAll();

  const observer = new MutationObserver((records) => {
    for (const record of records) {
      for (const node of record.addedNodes) {
        if (!(node instanceof HTMLElement)) continue;
        if (node.matches(HANDLE)) {
          attach(node);
        } else {
          attachAll(node);
        }
      }
    }
  });

  observer.observe(document.body, { childList: true, subtree: true });
}
