import { useMenuPermissionStore } from '#/store/menu-permission';

/**
 * [렌더 함수용 권한 확인]
 *
 * 화면 템플릿에서는 `v-perm` 디렉티브나 `useMenuPermission()` 훅을 쓰면 된다.
 * 하지만 vxe-table 의 액션 컬럼처럼 `h()` 로 직접 그리는 자리에서는
 * 디렉티브를 붙일 수 없어서 이 함수를 쓴다.
 *
 * ```ts
 * slots: {
 *   default: ({ row }) =>
 *     h('div', {}, [
 *       can('update') && h(Button, { onClick: () => onEdit(row) }, ...),
 *       can('delete') && h(Popconfirm, { ... }),
 *     ].filter(Boolean)),
 * }
 * ```
 *
 * 렌더 함수 안에서 부르면 스토어를 읽으므로, 권한이 나중에 도착해도
 * 다시 그려질 때 반영된다.
 *
 * 권한은 JSini 포털 한 곳(`scom.role_menus`)에서만 관리하고
 * 장례식장·헬프데스크 등 모든 MSA 화면이 이 결과를 따른다.
 *
 * @param action 확인할 동작
 * @param path   다른 화면의 권한을 봐야 할 때만 지정. 비우면 현재 주소를 쓴다.
 */
export function can(action: PermissionAction, path?: string): boolean {
  const store = useMenuPermissionStore();

  // 아직 못 받은 동안만 열어 둔다 (깜빡임 방지).
  //
  // **"역할이 하나도 없는 계정은 막지 않는다" 는 규칙은 없앴다.** 그러면 권한을
  // 하나도 주지 않은 계정이 오히려 모든 동작을 갖게 된다. 받아왔더니 비어 있으면
  // 그대로 '권한 없음' 이다.
  if (!store.isLoaded) return true;

  const target = path ?? window.location.pathname;
  return Boolean((store.resolve(target) as any)[ACTION_TO_FIELD[action]]);
}

export type PermissionAction =
  | 'create'
  | 'cust1'
  | 'cust2'
  | 'cust3'
  | 'cust4'
  | 'cust5'
  | 'cust6'
  | 'cust7'
  | 'cust8'
  | 'delete'
  | 'excel'
  | 'print'
  | 'search'
  | 'update'
  | 'view';

const ACTION_TO_FIELD: Record<PermissionAction, string> = {
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
