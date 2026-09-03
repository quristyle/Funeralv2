import { onUnmounted, watch } from 'vue';

import { useIsMobile } from '@vben-core/composables';

/**
 * 모바일에서 팝업(모달·드로어)을 **브라우저 뒤로가기**로 닫는다 (지시, 2026-09-04).
 *
 * 모바일에는 esc 키가 없다. 안드로이드 사용자의 손은 팝업을 만나면 뒤로가기부터
 * 가는데, 그때 팝업이 아니라 **페이지가 뒤로 가 버리면** 입력하던 것이 통째로 날아간다.
 *
 * 방법: 팝업이 열릴 때 같은 주소로 history 항목을 하나 얹고,
 *   · 뒤로가기(popstate) → 그 항목이 걷히면서 팝업을 닫는다 (페이지는 그대로)
 *   · X 등으로 닫힘      → 얹어 둔 항목을 `history.back()` 으로 걷어 원위치
 * 같은 주소라 vue-router 의 이동은 일어나지 않는다.
 *
 * 여러 팝업이 겹치면 모듈 수준 스택으로 **맨 위 것만** 닫는다 — 뒤로가기 한 번에
 * 팝업 하나씩, 스택이 빌 때까지. (아래 것이 위 것보다 먼저 X 로 닫히는 경우는
 * history 항목이 한 개 어긋날 수 있는데, 그때도 페이지가 이동하지는 않고
 * 뒤로가기 한 번이 빈 걸음이 될 뿐이다.)
 *
 * 데스크톱에서는 아무것도 하지 않는다.
 */

type StackEntry = { close: () => void };

const stack: StackEntry[] = [];
let listening = false;

function onPopstate() {
  // 닫기 전에 스택에서 빼야 한다 — close() 가 열림상태 watcher 를 타고 다시
  // 들어왔을 때 "이미 걷힌 항목" 으로 보여 history.back() 을 중복하지 않는다.
  const top = stack.pop();
  if (stack.length === 0 && listening) {
    window.removeEventListener('popstate', onPopstate);
    listening = false;
  }
  top?.close();
}

export function useBackClose(
  isOpen: () => boolean | undefined,
  close: () => void,
) {
  const { isMobile } = useIsMobile();
  let entry: null | StackEntry = null;

  function disarm(consumeHistory: boolean) {
    if (!entry) return;
    const index = stack.indexOf(entry);
    entry = null;
    if (index === -1) {
      // popstate 가 이미 걷어 갔다(뒤로가기로 닫힘) — history 는 이미 원위치다.
      return;
    }
    stack.splice(index, 1);
    if (stack.length === 0 && listening) {
      window.removeEventListener('popstate', onPopstate);
      listening = false;
    }
    if (consumeHistory) {
      window.history.back();
    }
  }

  // immediate: 팝업 컴포넌트는 첫 open 때 비로소 마운트되는 경우가 많다
  // (connectedComponent 지연 렌더). 그때는 isOpen 이 이미 true 인 채로
  // watcher 가 붙으므로, immediate 가 아니면 첫 열림을 놓친다.
  watch(
    isOpen,
    (open) => {
      if (!isMobile.value) return;

      if (open && !entry) {
        entry = { close };
        stack.push(entry);
        window.history.pushState(
          { ...(window.history.state ?? {}), __vbenPopup: stack.length },
          '',
        );
        if (!listening) {
          window.addEventListener('popstate', onPopstate);
          listening = true;
        }
      } else if (!open) {
        disarm(true);
      }
    },
    { immediate: true },
  );

  onUnmounted(() => disarm(true));
}
