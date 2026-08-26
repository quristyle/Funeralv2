import type { App, Directive, DirectiveBinding } from 'vue';

import { effectScope, watchEffect } from 'vue';

import { useMenuPermissionStore } from '#/store/menu-permission';

/**
 * [v-perm 디렉티브]
 *
 * 현재 화면에서 그 동작을 할 권한이 없으면 요소를 감추거나 잠근다.
 * 권한은 JSini 포털 한 곳(`scom.role_menus`)에서만 관리하고,
 * 장례식장·헬프데스크 등 모든 MSA 화면이 이 결과를 따른다.
 *
 * ```html
 * <Button v-perm:create @click="onCreate">등록</Button>
 * <Button v-perm:delete.disable @click="onDelete">삭제</Button>
 * <Button v-perm:excel @click="onExport">엑셀</Button>
 * ```
 *
 * 기본은 숨김이고, `.disable` 을 붙이면 감추는 대신 잠근다.
 * 버튼이 사라지면 화면 구성이 어색해지는 자리에 쓴다.
 *
 * 경로는 요소가 속한 라우트를 자동으로 쓴다. 다른 화면의 권한을 봐야 하면
 * 값으로 경로를 넘긴다: `v-perm:update="'/helpdesk/request/manage'"`.
 *
 * 화면 안에서 조건을 조합해야 할 때는 디렉티브 대신
 * `useMenuPermission()` 훅을 쓰는 편이 읽기 쉽다.
 */

/** v-perm:<이 이름> → 권한 필드 */
const ARG_TO_FIELD: Record<string, string> = {
  create: 'canCreate',
  cust1: 'canCust1',
  cust2: 'canCust2',
  cust3: 'canCust3',
  cust4: 'canCust4',
  cust5: 'canCust5',
  cust6: 'canCust6',
  cust7: 'canCust7',
  cust8: 'canCust8',
  delete: 'canDelete',
  excel: 'canExcel',
  print: 'canPrint',
  search: 'canSearch',
  update: 'canUpdate',
  view: 'canView',
};

/** 숨길 때 원래 display 값을 기억해 둔다. 다시 켤 때 되돌리기 위해서다. */
const ORIGINAL_DISPLAY = new WeakMap<HTMLElement, string>();

/**
 * 요소마다 반응형 감시를 하나씩 붙여 둔다.
 *
 * 권한 목록은 로그인 뒤 비동기로 도착한다. mounted 시점에 한 번만 계산하면
 * 새로고침으로 화면에 바로 들어온 경우 권한이 늦게 와도 버튼이 갱신되지 않는다.
 * 그래서 스토어를 감시해 값이 바뀔 때마다 다시 반영한다.
 */
const SCOPES = new WeakMap<HTMLElement, ReturnType<typeof effectScope>>();

function apply(el: HTMLElement, binding: DirectiveBinding) {
  const field = ARG_TO_FIELD[binding.arg ?? 'view'];
  if (!field) {
    console.warn(`[v-perm] 알 수 없는 권한 이름입니다: ${binding.arg}`);
    return;
  }

  const store = useMenuPermissionStore();

  // 아직 못 받은 동안만 열어 둔다 (깜빡임 방지).
  //
  // **"역할이 하나도 없는 계정은 막지 않는다" 는 규칙은 없앴다.** 그러면 권한을
  // 하나도 주지 않은 계정이 오히려 모든 버튼을 갖게 된다. 받아왔더니 비어 있으면
  // 그대로 '권한 없음' 으로 다룬다.
  if (!store.isLoaded) {
    const original = ORIGINAL_DISPLAY.get(el);
    if (original !== undefined) el.style.display = original;
    el.removeAttribute('disabled');
    return;
  }

  const path =
    typeof binding.value === 'string' && binding.value
      ? binding.value
      : window.location.pathname;

  const allowed = Boolean((store.resolve(path) as any)[field]);

  if (binding.modifiers.disable) {
    // 잠그기: 버튼이 사라지면 어색한 자리에 쓴다.
    el.toggleAttribute('disabled', !allowed);
    el.classList.toggle('cursor-not-allowed', !allowed);
    el.classList.toggle('opacity-50', !allowed);
    if (!allowed) {
      el.setAttribute('title', '이 동작을 할 권한이 없습니다.');
    } else if (el.getAttribute('title') === '이 동작을 할 권한이 없습니다.') {
      el.removeAttribute('title');
    }
    return;
  }

  // 감추기(기본)
  if (allowed) {
    const original = ORIGINAL_DISPLAY.get(el);
    if (original !== undefined) el.style.display = original;
  } else {
    if (!ORIGINAL_DISPLAY.has(el)) {
      ORIGINAL_DISPLAY.set(el, el.style.display);
    }
    el.style.display = 'none';
  }
}

const permDirective: Directive<HTMLElement> = {
  mounted(el, binding) {
    const scope = effectScope();
    SCOPES.set(el, scope);
    // 스토어 값이 바뀌면 자동으로 다시 반영된다.
    scope.run(() => watchEffect(() => apply(el, binding)));
  },
  updated(el, binding) {
    apply(el, binding);
  },
  unmounted(el) {
    SCOPES.get(el)?.stop();
    SCOPES.delete(el);
  },
};

export function setupPermDirective(app: App) {
  app.directive('perm', permDirective);
}

export { permDirective };
