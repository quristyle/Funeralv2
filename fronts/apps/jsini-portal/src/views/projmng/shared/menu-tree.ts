/**
 * 프로젝트관리 자체 메뉴 트리 구성 — 이식 전 `MenuInfo.BuildMenuTree`.
 *
 * 주의: 이건 **포털 메뉴가 아니다.** 프로젝트관리가 관리 대상 프로젝트마다
 * 들고 있는 화면 목록(`projmng` 스키마의 메뉴 테이블)이다.
 * 포털 자신의 메뉴·권한은 AuthServer(`scom.system_menus`)가 관장하고,
 * 이 화면은 그 관리 대상 데이터를 편집하는 업무 화면일 뿐이다.
 *
 * 프로시저는 평평한 목록을 주고 `owner_id` 가 `'ROOT'` 인 것이 최상위다.
 */
import type { ProjMngRow } from '#/api/projmng';

export interface MenuNode extends ProjMngRow {
  children: MenuNode[];
  /** 부모 노드. 드래그로 옮길 때 원래 자리에서 떼어내려면 필요하다 */
  parent?: MenuNode;
  /**
   * 값이 바뀐 항목 표시. 그리드가 아니라 폼·트리로 편집하는 화면이라
   * `quri_ischange` 대신 이 표시를 쓰고, 저장할 때 표시된 것만 보낸다.
   */
  __dirty?: boolean;
}

/** 평평한 목록을 트리로 만든다. 원본과 같은 규칙(`owner_id === 'ROOT'` 이 뿌리)이다. */
export function buildMenuTree(rows: ProjMngRow[]): MenuNode[] {
  const map = new Map<string, MenuNode>();
  const roots: MenuNode[] = [];

  const nodes = rows.map((row) => {
    const node = { ...row, children: [] } as MenuNode;
    map.set(String(row.mnu_id ?? ''), node);
    return node;
  });

  nodes.forEach((node) => {
    const ownerId = String(node.owner_id ?? '');
    const parent = ownerId === 'ROOT' ? undefined : map.get(ownerId);

    if (parent) {
      node.parent = parent;
      parent.children.push(node);
    } else {
      // 부모를 못 찾은 항목도 뿌리로 올린다. 데이터가 깨져 있어도 화면에서 사라지지 않게.
      roots.push(node);
    }
  });

  return roots;
}

/** 트리를 다시 평평하게 만든다. 체크 상태 대조 등에 쓴다. */
export function flattenMenuTree(nodes: MenuNode[]): MenuNode[] {
  return nodes.flatMap((node) => [node, ...flattenMenuTree(node.children)]);
}

/** ant-design-vue 의 Tree 가 먹는 형태로 바꾼다. */
export interface AntTreeNode {
  key: string;
  title: string;
  children: AntTreeNode[];
  raw: MenuNode;
}

export function toAntTree(nodes: MenuNode[]): AntTreeNode[] {
  return nodes.map((node) => ({
    key: String(node.mnu_id ?? ''),
    title: String(node.mnu_nm ?? '(이름 없음)'),
    children: toAntTree(node.children),
    raw: node,
  }));
}

/** 트리에서 키로 노드를 찾는다. */
export function findMenuNode(
  nodes: MenuNode[],
  mnuId: string,
): MenuNode | undefined {
  for (const node of nodes) {
    if (String(node.mnu_id ?? '') === mnuId) return node;
    const found = findMenuNode(node.children, mnuId);
    if (found) return found;
  }
  return undefined;
}
