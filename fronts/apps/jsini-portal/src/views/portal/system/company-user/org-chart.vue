<script lang="ts" setup>
import { ref, watch, onMounted, computed, nextTick } from 'vue';
import { Page } from '@vben/common-ui';
import { Avatar, Card, Button, Input, Modal, Tooltip, message, Radio } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';
import { IconifyIcon } from '@vben/icons';
import BizSelect from '#/components/BizSelect.vue';
import { getCompanyList } from '#/api/portal/system/company';
import { createDept, getDeptList, getDeptUsers, getEligibleDeptUsers, moveDept, moveUserDept } from '#/api/portal/system/dept';

// 회사 선택 상태 (undefined로 초기화해야 BizSelect의 auto-select-first가 정상 동작합니다)
const selectedCompanyId = ref<string | undefined>(undefined);
const selectedCompanyName = ref<string>('');
const loading = ref<boolean>(false);
const layoutDirection = ref<'vertical' | 'horizontal'>('vertical'); // 기본값: 세로 조직도

// SVG 캔버스 변환 상태 (Zoom & Pan)
const canvasRef = ref<HTMLDivElement | null>(null);
const zoom = ref<number>(1.0);
const panX = ref<number>(100);
const panY = ref<number>(150);

// 드래그 상태 (화면 이동용)
const isPanning = ref<boolean>(false);
const startPanX = ref<number>(0);
const startPanY = ref<number>(0);

// 드래그앤드롭 데이터 전송용 키
const DRAG_TYPE_DEPT = 'DEPARTMENT';
const DRAG_TYPE_USER = 'USER';

// ─────────────────────────────────────────────────────────────
// 미소속 사용자 보기
//
// 조직도에는 이미 회사·부서·소속 사람이 나온다. 여기 없는 사람 —
// **어느 회사에도 속하지 않은 계정**을 옆에 띄워, 조직도의 부서 노드로
// 끌어다 놓아 소속시킬 수 있게 한다.
//
// 놓는 처리는 새로 만들지 않는다. 조직도의 부서 노드는 이미 사용자 드롭을 받아
// `moveUserDept(사용자, 부서)` 를 부른다(onNodeDrop). 그래서 이 목록의 행도
// **같은 형식**으로 끌기 값을 실어 주면 그대로 동작한다.
//
// 서버는 회사 인자를 넘기지 않으면 '부서 없음 + 회사 없음' 만 준다.
// ─────────────────────────────────────────────────────────────
const showUnassigned = ref<boolean>(false);
const unassignedUsers = ref<any[]>([]);
const loadingUnassigned = ref<boolean>(false);

async function fetchUnassignedUsers() {
  loadingUnassigned.value = true;
  try {
    const res = await getEligibleDeptUsers();
    unassignedUsers.value = (res as any)?.result ?? res ?? [];
  } catch (error) {
    console.error(error);
    message.error('미소속 사용자 목록을 불러오지 못했습니다.');
  } finally {
    loadingUnassigned.value = false;
  }
}

/**
 * 목록에 쓸 프로필 사진 주소. 원본을 그대로 받으면 목록에서 무거워
 * 포털이 이미 쓰는 규칙대로 썸네일 경로로 바꾼다(layouts/basic.vue 와 동일).
 */
function avatarUrl(record: any): string {
  const raw = record?.avatar ?? '';
  if (!raw) return '';
  return raw.includes('/api/file/download/')
    ? raw.replace('/api/file/download/', '/api/file/thumbnail/')
    : raw;
}

/** 사진이 없을 때 쓸 글자. 이름 첫 글자, 없으면 아이디 첫 글자. */
function avatarText(record: any): string {
  const name = String(record?.userName || record?.loginId || '?');
  return name.slice(0, 1).toUpperCase();
}

/** 미소속 사용자 보기를 켜고 끈다. 켤 때마다 다시 받아 최신 상태를 보여준다. */
async function toggleUnassigned() {
  showUnassigned.value = !showUnassigned.value;
  if (showUnassigned.value) await fetchUnassignedUsers();
}

// ─────────────────────────────────────────────────────────────
// 부서 추가
//
// 회사 노드에서 누르면 **회사 직속 부서**, 부서 노드에서 누르면 **그 부서의 하위 부서**가 된다.
// 조직도를 보면서 바로 만들 수 있어야, 부서를 만들러 다른 화면으로 갔다 오지 않는다.
// ─────────────────────────────────────────────────────────────
const addDeptOpen = ref<boolean>(false);
const addDeptName = ref<string>('');
const addDeptSaving = ref<boolean>(false);
/** 새 부서의 상위. 회사 직속이면 null 이다. */
const addDeptParent = ref<null | { id: string; name: string }>(null);

/** 부서 추가 창을 연다. `parent` 가 없으면 회사 직속이다. */
function openAddDept(parent?: { id: string; name: string }) {
  addDeptParent.value = parent ?? null;
  addDeptName.value = '';
  addDeptOpen.value = true;
}

async function submitAddDept() {
  const name = addDeptName.value.trim();
  if (!name) {
    message.warning('부서명을 입력해 주세요.');
    return;
  }
  if (!selectedCompanyId.value) {
    message.warning('회사를 먼저 선택해 주세요.');
    return;
  }

  addDeptSaving.value = true;
  try {
    await createDept({
      companyId: selectedCompanyId.value,
      name,
      // 회사 직속이면 상위가 없다. 서버는 pid 로 받는다.
      pid: addDeptParent.value?.id,
      sortOrder: 0,
      status: 1,
    } as any);

    message.success(
      addDeptParent.value
        ? `[${addDeptParent.value.name}] 하위에 부서를 만들었습니다.`
        : '회사 직속 부서를 만들었습니다.',
    );
    addDeptOpen.value = false;
    await loadOrgData();
  } catch (error) {
    console.error(error);
    message.error('부서를 만들지 못했습니다.');
  } finally {
    addDeptSaving.value = false;
  }
}

// ─────────────────────────────────────────────────────────────
// 이동 팝업 — 드래그의 터치(모바일) 대체 경로
//
// HTML5 drag&drop 은 모바일 브라우저에서 동작하지 않는다. 그래서 노드마다
// [이동] 버튼을 두고, 대상 부서를 고르는 팝업에서 **드롭이 부르던 것과 같은
// API**(moveDept · moveUserDept)를 부른다. 드래그는 그대로 있다 —
// 데스크톱에서는 둘 다 되고, 버튼 쪽이 접근성도 낫다.
// ─────────────────────────────────────────────────────────────

/** 이동 팝업에서 '회사 직속' 을 뜻하는 선택값. 부서를 옮길 때만 나온다. */
const MOVE_TO_COMPANY = '__COMPANY__';

const moveOpen = ref<boolean>(false);
const moveSaving = ref<boolean>(false);
/** 무엇을 옮기는가. 부서 노드·사용자 노드·미소속 목록의 행이 모두 여기로 온다. */
const moveSource = ref<null | { type: 'DEPT' | 'USER'; id: string; name: string }>(null);
/** 어디로 옮기는가. 아직 안 골랐으면 undefined 다. */
const moveTargetId = ref<string | undefined>(undefined);

/**
 * 이동 팝업에 보여줄 부서 목록. 이미 받아 둔 조직도 트리를 위에서 아래로 편 것이라
 * 서버를 다시 부르지 않는다. 부서를 옮길 때는 **자기 자신과 그 하위 전부**를 뺀다 —
 * 자기 밑으로는 들어갈 수 없다(건너뛰면 재귀도 멈춰 하위가 함께 빠진다).
 */
const moveDeptOptions = computed<{ id: string; name: string; depth: number }[]>(() => {
  const src = moveSource.value;
  const out: { id: string; name: string; depth: number }[] = [];
  function walk(nodes: OrgNode[], depth: number) {
    for (const n of nodes) {
      if (n.type !== 'DEPT') continue;
      if (src?.type === 'DEPT' && n.id === src.id) continue;
      out.push({ id: n.id, name: n.name, depth });
      walk(n.children, depth + 1);
    }
  }
  if (orgTreeRoot.value) walk(orgTreeRoot.value.children, 0);
  return out;
});

/** 이동 팝업을 연다. */
function openMovePicker(type: 'DEPT' | 'USER', id: string, name: string) {
  moveSource.value = { type, id, name };
  moveTargetId.value = undefined;
  moveOpen.value = true;
}

async function submitMove() {
  const src = moveSource.value;
  if (!src) return;
  if (moveTargetId.value === undefined) {
    message.warning('이동할 부서를 선택해 주세요.');
    return;
  }

  moveSaving.value = true;
  try {
    if (src.type === 'DEPT') {
      // 드롭 처리(onNodeDrop · onCompanyDrop)와 같은 호출이다.
      const targetId =
        moveTargetId.value === MOVE_TO_COMPANY ? undefined : moveTargetId.value;
      const success = await moveDept(src.id, targetId);
      if (success) {
        message.success(
          targetId
            ? '부서가 성공적으로 이동되었습니다.'
            : '부서가 회사의 직속 부서로 이동되었습니다.',
        );
      }
    } else {
      const success = await moveUserDept(src.id, moveTargetId.value);
      if (success) {
        message.success('사용자의 부서 소속이 변경되었습니다.');
      }
    }
    moveOpen.value = false;
    await loadOrgData();
    // 미소속 목록에서 옮긴 경우 그 사람은 이제 소속이 있다. 목록을 다시 받는다.
    if (showUnassigned.value) await fetchUnassignedUsers();
  } catch (error) {
    console.error(error);
    message.error('이동 처리에 실패했습니다.');
  } finally {
    moveSaving.value = false;
  }
}

// 캔버스 스타일 transform 적용
const transformStyle = computed(() => {
  return `translate(${panX.value}px, ${panY.value}px) scale(${zoom.value})`;
});

// 조직도 데이터 로드 및 트리 구조 구성
interface OrgNode {
  id: string;
  type: 'COMPANY' | 'DEPT' | 'USER';
  name: string;
  depth: number;
  parentId?: string;
  children: OrgNode[];
  loginId?: string;
  email?: string;
  phone?: string;
  /** 프로필 사진 주소 (사용자 노드에만 있다) */
  avatar?: string;
}

const orgTreeRoot = ref<OrgNode | null>(null);


/**
 * 회사 목록 응답에서 배열을 꺼낸다.
 *
 * 서버는 `{ result: [...], page: {...} }` 로 감싸 보낸다. 예전 코드는 `items` 만 봤는데
 * 그런 필드는 없어서, 감싼 **객체 자체**가 배열인 줄 알고 `.find` 를 불렀다 —
 * "items.find is not a function" 으로 터졌다. 부서가 없는 회사를 열 때만 이 길로 와서
 * 눈에 늦게 띈 것이다(부서가 있으면 회사명을 부서 쪽에서 얻는다).
 */
function toCompanyList(res: any): any[] {
  if (Array.isArray(res)) return res;
  if (Array.isArray(res?.result)) return res.result;
  if (Array.isArray(res?.items)) return res.items;
  if (Array.isArray(res?.data?.result)) return res.data.result;
  return [];
}

async function loadOrgData() {
  if (!selectedCompanyId.value) return;
  loading.value = true;
  try {
    // 1. 부서 트리 가져오기
    const deptsRes = await getDeptList(selectedCompanyId.value);
    const depts = (deptsRes as any)?.result ?? deptsRes ?? [];

    // 회사명 유도
    let companyName = '조직도';
    if (depts.length > 0) {
      companyName = depts[0].companyName || '회사';
    } else {
      const compRes = await getCompanyList();
      const matched = toCompanyList(compRes).find(
        (c: any) => c.id === selectedCompanyId.value,
      );
      companyName = matched?.name || '회사';
    }
    selectedCompanyName.value = companyName;

    // 2. 회사 노드 생성
    const rootNode: OrgNode = {
      id: selectedCompanyId.value,
      type: 'COMPANY',
      name: companyName,
      depth: 0,
      children: [],
    };

    // 3. 재귀적으로 부서 및 사용자 매핑 노드 생성
    rootNode.children = await buildDeptUserNodes(depts, 1);
    orgTreeRoot.value = rootNode;

    // 4. 노드 배치 레이아웃 계산을 위해 트리 구조 평탄화 및 순서 계산
    calculateNodeLayouts();
    nextTick(() => {
      centerRootNode();
    });
  } catch (error) {
    console.error(error);
    message.error('조직도 데이터 로드 실패');
  } finally {
    loading.value = false;
  }
}

async function buildDeptUserNodes(depts: any[], depth: number): Promise<OrgNode[]> {
  const nodes: OrgNode[] = [];
  for (const dept of depts) {
    const deptNode: OrgNode = {
      id: dept.id,
      type: 'DEPT',
      name: dept.name,
      depth: depth,
      parentId: dept.pid,
      children: [],
    };

    // 해당 부서 소속 사용자 목록 조회
    const usersRes = await getDeptUsers(dept.id);
    const users = (usersRes as any)?.result ?? usersRes ?? [];
    const userNodes: OrgNode[] = users.map((u: any) => ({
      id: u.id,
      type: 'USER' as const,
      name: u.userName || u.loginId,
      loginId: u.loginId,
      email: u.email,
      phone: u.phone,
      avatar: u.avatar,
      depth: depth + 1,
      parentId: dept.id,
      children: [],
    }));

    // 하위 부서 재귀 빌드
    const childDepts = dept.children || [];
    const subDeptNodes = await buildDeptUserNodes(childDepts, depth + 1);

    // 자식 노드로 사용자들과 하위 부서 결합
    deptNode.children = [...userNodes, ...subDeptNodes];
    nodes.push(deptNode);
  }
  return nodes;
}

// 가로 마인드맵 레이아웃 연산용 상태
interface RenderNode extends OrgNode {
  x: number;
  y: number;
  parentX?: number;
  parentY?: number;
  /**
   * 세로 조직도에서 부서 아래 세로로 쌓인 사용자일 때, 바로 위에 있는 노드의 식별자.
   *
   * 연결선을 부서에서 각 사람으로 부챗살처럼 긋지 않고 **위에서 아래로 이어 긋기** 위한 값이다.
   * 한 칸에 쌓여 있어 부챗살로 그으면 선이 서로 겹쳐 읽을 수 없다.
   */
  stackPrevId?: string;
}

const renderNodes = ref<RenderNode[]>([]);
const renderLinks = ref<{ sourceX: number; sourceY: number; targetX: number; targetY: number }[]>([]);

function calculateNodeLayouts() {
  if (!orgTreeRoot.value) return;

  const tempNodes: RenderNode[] = [];
  const tempLinks: any[] = [];

  const isVertical = layoutDirection.value === 'vertical';
  const xGap = isVertical ? 200 : 250;
  const yGap = isVertical ? 120 : 70;

  /**
   * 세로 조직도에서 부서 아래 사용자들을 쌓을 간격.
   *
   * 노드 카드 높이가 50px 이라 58 이면 8px 씩 벌어진다. 부서 사이 간격(yGap 120)보다
   * 좁게 두어 "한 부서의 사람 목록" 으로 읽히게 한다.
   */
  const userStackGap = 58;

  let xCounter = 0;
  let yCounter = 0;

  // Pre-order 트리 순회를 돌며 좌표 부여
  function traverse(node: OrgNode, parent?: RenderNode) {
    const renderNode: RenderNode = {
      ...node,
      x: isVertical ? xCounter * xGap : node.depth * xGap,
      y: isVertical ? node.depth * yGap : yCounter * yGap,
    };

    if (parent) {
      renderNode.parentX = parent.x;
      renderNode.parentY = parent.y;
      tempLinks.push({
        sourceX: parent.x,
        sourceY: parent.y,
        targetX: renderNode.x,
        targetY: renderNode.y,
      });
    }

    tempNodes.push(renderNode);

    /*
     * 세로 조직도에서는 부서 아래 **사용자들을 한 칸에 세로로 쌓는다.**
     *
     * 원래는 사용자도 하위 부서와 똑같이 잎 하나가 한 칸(xGap 200px)을 차지했다.
     * 사람이 여덟 명인 부서 하나가 1600px 을 먹어, 부서 몇 개만 있어도 좌우로
     * 한참 끌어야 전체가 보였다.
     *
     * 이제 사용자 묶음 전체가 **한 칸**만 쓴다. 그 칸 안에서 위에서 아래로 쌓인다.
     * 하위 부서는 예전처럼 옆으로 갈라진다 — 부서 계층은 가로로 보는 편이 읽기 쉽다.
     *
     * 묶음에 별도 칸을 주는 이유: 부서의 x 는 자식들의 가운데로 보정되는데(아래
     * adjustParentPosition), 사용자를 부서와 같은 칸에 두면 하위 부서 칸과 겹친다.
     */
    const userChildren = isVertical
      ? node.children.filter((c) => c.type === 'USER')
      : [];
    const otherChildren = isVertical
      ? node.children.filter((c) => c.type !== 'USER')
      : node.children;

    if (userChildren.length > 0) {
      const column = xCounter;
      xCounter += 1; // 사람이 몇 명이든 한 칸이다

      userChildren.forEach((user, index) => {
        const userNode: RenderNode = {
          ...user,
          x: column * xGap,
          y: (node.depth + 1) * yGap + index * userStackGap,
          // 첫 사람은 부서에서, 그다음부터는 바로 위 사람에서 선을 잇는다.
          stackPrevId: index === 0 ? node.id : userChildren[index - 1]!.id,
        };
        // 연결선은 아래에서 부모 좌표를 동기화한 뒤 만든다.
        tempNodes.push(userNode);
      });
    }

    if (otherChildren.length > 0) {
      otherChildren.forEach((child) => {
        traverse(child, renderNode);
      });
    } else if (userChildren.length === 0) {
      // 자식이 없는 잎이다. 자기 칸을 하나 쓴다.
      if (isVertical) {
        xCounter++;
      } else {
        yCounter++;
      }
    }
  }

  traverse(orgTreeRoot.value);

  // 부모 노드 좌표 보정
  function adjustParentPosition(nodeId: string) {
    const node = tempNodes.find(n => n.id === nodeId);
    if (!node || node.children.length === 0) return;

    node.children.forEach(c => adjustParentPosition(c.id));

    const activeChildren = tempNodes.filter(n => node.children.map(c => c.id).includes(n.id));

    if (activeChildren.length > 0) {
      if (isVertical) {
        const minX = Math.min(...activeChildren.map(c => c.x));
        const maxX = Math.max(...activeChildren.map(c => c.x));
        node.x = (minX + maxX) / 2;
      } else {
        const minY = Math.min(...activeChildren.map(c => c.y));
        const maxY = Math.max(...activeChildren.map(c => c.y));
        node.y = (minY + maxY) / 2;
      }
    }
  }

  adjustParentPosition(orgTreeRoot.value.id);

  // 부모 노드의 최종 보정 좌표를 자식 노드의 parentX, parentY 에 동기화
  tempNodes.forEach((n) => {
    // 세로로 쌓인 사용자는 부서가 아니라 **바로 위 노드**에서 선을 받는다.
    // (부서에서 각 사람으로 그으면 한 칸에 겹쳐 읽을 수 없다)
    if (n.stackPrevId) {
      const prev = tempNodes.find((p) => p.id === n.stackPrevId);
      if (prev) {
        n.parentX = prev.x;
        n.parentY = prev.y;
        return;
      }
    }

    if (n.parentId) {
      const parentNode = tempNodes.find((p) => p.id === n.parentId);
      if (parentNode) {
        n.parentX = parentNode.x;
        n.parentY = parentNode.y;
      }
    } else if (orgTreeRoot.value && n.id !== orgTreeRoot.value.id) {
      // parentId가 없으나 root가 아닌 경우 (예: 회사 바로 밑의 부서들)
      const parentNode = tempNodes.find((p) => p.id === orgTreeRoot.value?.id);
      if (parentNode) {
        n.parentX = parentNode.x;
        n.parentY = parentNode.y;
      }
    }
  });

  renderNodes.value = tempNodes;
  renderLinks.value = tempNodes
    .filter((n) => n.parentX !== undefined)
    .map((n) => ({
      sourceX: n.parentX!,
      sourceY: n.parentY!,
      targetX: n.x,
      targetY: n.y,
    }));
}

// 줌앤팬 마우스 드래그 핸들러
function onCanvasMouseDown(e: MouseEvent) {
  const target = e.target as HTMLElement;
  if (target.closest('.node-card') || target.closest('.no-pan')) return;

  isPanning.value = true;
  startPanX.value = e.clientX - panX.value;
  startPanY.value = e.clientY - panY.value;
}

function onCanvasMouseMove(e: MouseEvent) {
  if (!isPanning.value) return;
  panX.value = e.clientX - startPanX.value;
  panY.value = e.clientY - startPanY.value;
}

function onCanvasMouseUp() {
  isPanning.value = false;
}

// ─────────────────────────────────────────────────────────────
// 터치 화면 이동·확대 — 마우스 핸들러의 터치판
//
// 터치 브라우저는 끌기에서 mousemove 를 만들어 주지 않아, 이대로는 모바일에서
// 조직도를 움직일 수조차 없다(움직여야 노드의 [이동] 버튼에 닿는다).
// 한 손가락은 이동, 두 손가락은 확대다. 캔버스에 touch-action: none 을 주어
// 브라우저의 화면 스크롤과 겹치지 않게 한다.
// ─────────────────────────────────────────────────────────────
const pinchDist = ref<number>(0);

function touchDistance(e: TouchEvent): number {
  const [a, b] = [e.touches[0]!, e.touches[1]!];
  return Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
}

function onCanvasTouchStart(e: TouchEvent) {
  const target = e.target as HTMLElement;
  if (target.closest('.node-card') || target.closest('.no-pan')) return;

  if (e.touches.length === 1) {
    isPanning.value = true;
    startPanX.value = e.touches[0]!.clientX - panX.value;
    startPanY.value = e.touches[0]!.clientY - panY.value;
  } else if (e.touches.length === 2) {
    isPanning.value = false;
    pinchDist.value = touchDistance(e);
  }
}

function onCanvasTouchMove(e: TouchEvent) {
  if (e.touches.length === 1 && isPanning.value) {
    e.preventDefault();
    panX.value = e.touches[0]!.clientX - startPanX.value;
    panY.value = e.touches[0]!.clientY - startPanY.value;
  } else if (e.touches.length === 2 && pinchDist.value > 0) {
    e.preventDefault();
    const dist = touchDistance(e);
    let newZoom = zoom.value * (dist / pinchDist.value);
    newZoom = Math.max(0.3, Math.min(2.5, newZoom));
    zoom.value = parseFloat(newZoom.toFixed(2));
    pinchDist.value = dist;
  }
}

function onCanvasTouchEnd() {
  isPanning.value = false;
  pinchDist.value = 0;
}

// 마우스 휠 Zoom 인 / 아웃 핸들러
function onCanvasWheel(e: WheelEvent) {
  e.preventDefault();
  const zoomFactor = 0.08;
  let newZoom = zoom.value + (e.deltaY < 0 ? zoomFactor : -zoomFactor);
  newZoom = Math.max(0.3, Math.min(2.5, newZoom));
  zoom.value = parseFloat(newZoom.toFixed(2));
}

// root 노드를 가로/세로 중앙 정렬하는 함수
function centerRootNode() {
  if (!orgTreeRoot.value || !canvasRef.value) return;
  const isVertical = layoutDirection.value === 'vertical';

  if (isVertical) {
    const canvasWidth = canvasRef.value.clientWidth;
    const rootRenderNode = renderNodes.value.find(n => n.id === orgTreeRoot.value?.id);
    const rootX = rootRenderNode ? rootRenderNode.x : 0;
    panX.value = (canvasWidth / 2) - (rootX + 90) * zoom.value;
    panY.value = 80;
  } else {
    const canvasHeight = canvasRef.value.clientHeight;
    const rootRenderNode = renderNodes.value.find(n => n.id === orgTreeRoot.value?.id);
    const rootY = rootRenderNode ? rootRenderNode.y : 0;
    panX.value = 80;
    panY.value = (canvasHeight / 2) - (rootY + 25) * zoom.value;
  }
}

// 줌 초기화
function resetZoom() {
  zoom.value = 1.0;
  centerRootNode();
}

// 드래그앤드롭 처리 (부서 및 사용자 이동)
function onNodeDragStart(e: DragEvent, type: string, id: string) {
  if (!e.dataTransfer) return;
  e.dataTransfer.setData('text/plain', JSON.stringify({ type, id }));
  e.dataTransfer.effectAllowed = 'move';
}

function onDragOver(e: DragEvent) {
  e.preventDefault();
}

async function onNodeDrop(e: DragEvent, targetDeptId: string) {
  e.preventDefault();
  if (!e.dataTransfer) return;

  try {
    const rawData = e.dataTransfer.getData('text/plain');
    if (!rawData) return;

    const dragData = JSON.parse(rawData);
    const { type, id } = dragData;

    if (id === targetDeptId) return;

    if (type === DRAG_TYPE_DEPT) {
      const success = await moveDept(id, targetDeptId);
      if (success) {
        message.success('부서가 성공적으로 이동되었습니다.');
        await loadOrgData();
      }
    } else if (type === DRAG_TYPE_USER) {
      const success = await moveUserDept(id, targetDeptId);
      if (success) {
        message.success('사용자의 부서 소속이 변경되었습니다.');
        await loadOrgData();
        // 미소속 목록에서 끌어온 경우 그 사람은 이제 소속이 있다. 목록을 다시 받는다.
        if (showUnassigned.value) await fetchUnassignedUsers();
      }
    }
  } catch (error) {
    console.error(error);
    message.error('노드 이동 처리에 실패했습니다.');
  }
}

async function onCompanyDrop(e: DragEvent) {
  e.preventDefault();
  if (!e.dataTransfer) return;

  try {
    const rawData = e.dataTransfer.getData('text/plain');
    if (!rawData) return;

    const dragData = JSON.parse(rawData);
    const { type, id } = dragData;

    // 부서 노드인 경우만 회사의 직속 부서로 이동시킴 (상위 부서 ID를 undefined로 전송)
    if (type === DRAG_TYPE_DEPT) {
      const success = await moveDept(id, undefined);
      if (success) {
        message.success('부서가 회사의 직속 부서로 이동되었습니다.');
        await loadOrgData();
      }
    } else if (type === DRAG_TYPE_USER) {
      message.warning('사용자는 부서로만 이동할 수 있습니다.');
    }
  } catch (error) {
    console.error(error);
    message.error('회사로 노드 이동 처리에 실패했습니다.');
  }
}

// Bezier 곡선 연결선 패스 빌더
function getBezierPath(link: { sourceX: number; sourceY: number; targetX: number; targetY: number }) {
  const { sourceX, sourceY, targetX, targetY } = link;
  const isVertical = layoutDirection.value === 'vertical';

  if (isVertical) {
    // 세로형 곡선 (Top to Bottom)
    // 부모 노드의 하단 중앙(x + 90, y + 50)에서 자식 노드의 상단 중앙(x + 90, y)으로 베지어 매핑
    const controlY = (sourceY + targetY) / 2;
    return `M ${sourceX + 90} ${sourceY + 50} C ${sourceX + 90} ${controlY}, ${targetX + 90} ${controlY}, ${targetX + 90} ${targetY}`;
  } else {
    // 가로형 곡선 (Left to Right)
    // 부모 노드의 우측 중앙(x + 180, y + 25)에서 자식 노드의 좌측 중앙(x, y + 25)으로 베지어 매핑
    const controlX = (sourceX + targetX) / 2;
    return `M ${sourceX + 180} ${sourceY + 25} C ${controlX} ${sourceY + 25}, ${controlX} ${targetY + 25}, ${targetX} ${targetY + 25}`;
  }
}

watch(layoutDirection, () => {
  calculateNodeLayouts();
  nextTick(() => {
    centerRootNode();
  });
});

watch(selectedCompanyId, () => {
  loadOrgData();
});

onMounted(async () => {
  // BizSelect에 의해 최초 선택 시 자동으로 loadOrgData가 watch를 타고 동작하지만,
  // 화면 진입 시 첫 번째 회사가 자동으로 선택되도록 명시적으로 기본 회사 목록을 불러와 설정합니다.
  if (!selectedCompanyId.value) {
    try {
      const items = toCompanyList(await getCompanyList());
      if (items.length > 0 && items[0]?.id) {
        selectedCompanyId.value = items[0].id;
      }
    } catch (error) {
      console.error('기본 회사 목록 로드 실패:', error);
    }
  }
});
</script>

<template>
  <Page auto-content-height>
    <div class="flex flex-col h-full overflow-hidden gap-4">
      <!-- 상단 툴바 카드 -->
      <Card class="no-pan shadow-sm" :body-style="{ padding: '12px 24px' }">
        <div class="flex justify-between items-center flex-wrap gap-3">
          <div class="flex items-center gap-3">
            <span class="text-sm font-semibold text-gray-700">대상 회사:</span>
            <BizSelect
              v-model:value="selectedCompanyId"
              type="company"
              auto-select-first
              placeholder="회사를 선택해 주세요"
              class="w-60 no-pan"
              show-search
              option-filter-prop="label"
            />
          </div>
          <div class="flex items-center gap-2">
            <!-- 가로/세로 방향 토글 스위치 -->
            <Radio.Group v-model:value="layoutDirection" button-style="solid" class="no-pan mr-2">
              <Radio.Button value="vertical">
                <IconifyIcon icon="lucide:git-fork" class="inline-block mr-1 align-text-bottom text-sm" style="transform: rotate(90deg);" />
                세로 조직도
              </Radio.Button>
              <Radio.Button value="horizontal">
                <IconifyIcon icon="lucide:git-fork" class="inline-block mr-1 align-text-bottom text-sm" />
                가로 조직도
              </Radio.Button>
            </Radio.Group>

            <Tooltip title="휠로 화면을 확대/축소하고, 캔버스를 드래그하여 이동할 수 있습니다. 노드를 드래그하여 다른 부서에 소속시킬 수 있습니다.">
              <Button type="text" class="flex items-center justify-center p-2 text-gray-500">
                <template #icon>
                  <IconifyIcon icon="lucide:help-circle" class="size-5" />
                </template>
              </Button>
            </Tooltip>
            <!--
              미소속 사용자 보기. 켜면 오른쪽에 목록이 붙고, 그 행을 조직도의
              부서 노드로 끌어다 놓으면 그 부서 소속이 된다.
            -->
            <!--
              부서가 하나도 없으면 조직도에 회사 노드만 있고 그 카드의 버튼은 찾기 어렵다.
              첫 부서를 만드는 길을 상단에도 둔다.
            -->
            <Button
              class="flex items-center gap-1.5"
              :disabled="!selectedCompanyId"
              @click="openAddDept()"
            >
              <template #icon>
                <IconifyIcon icon="lucide:folder-plus" class="size-4" />
              </template>
              부서 추가
            </Button>
            <Button
              class="flex items-center gap-1.5"
              :type="showUnassigned ? 'primary' : 'default'"
              :loading="loadingUnassigned"
              @click="toggleUnassigned"
            >
              <template #icon>
                <IconifyIcon icon="lucide:user-plus" class="size-4" />
              </template>
              미소속 사용자 보기
            </Button>
            <GridIconButton
              :loading="loading"
              icon="vxe-icon-repeat"
              title="새로고침"
              @click="loadOrgData"
            />
            <Button class="flex items-center gap-1.5" @click="resetZoom">
              <template #icon>
                <IconifyIcon icon="lucide:refresh-cw" class="size-4" />
              </template>
              화면 맞춤
            </Button>
          </div>
        </div>
      </Card>

      <!--
        조직도 + 미소속 사용자 목록.

        목록을 켜면 오른쪽에 붙는다. 캔버스는 남은 폭을 쓴다(min-w-0 이 없으면
        캔버스가 줄어들지 않아 목록이 밖으로 밀려난다).
      -->
      <div class="flex-1 flex gap-4 min-h-0 overflow-hidden">
      <!-- 메인 2D 무한 마인드맵 영역 -->
      <div
        ref="canvasRef"
        class="flex-1 min-w-0 border rounded-xl overflow-hidden relative select-none cursor-grab"
        :class="{ 'cursor-grabbing': isPanning }"
        style="touch-action: none"
        @mousedown="onCanvasMouseDown"
        @mousemove="onCanvasMouseMove"
        @mouseup="onCanvasMouseUp"
        @mouseleave="onCanvasMouseUp"
        @wheel="onCanvasWheel"
        @touchstart="onCanvasTouchStart"
        @touchmove="onCanvasTouchMove"
        @touchend="onCanvasTouchEnd"
        @touchcancel="onCanvasTouchEnd"
      >
        <div class="absolute top-4 left-4 z-10 backdrop-blur px-3 py-1.5 rounded-md border text-xs text-gray-500 pointer-events-none">
          💡 노드를 드래그하거나 노드의 이동 버튼(<IconifyIcon icon="lucide:move" class="inline-block size-3 align-text-bottom" />)으로 다른 부서에 소속시킬 수 있습니다. (확대: 휠·두 손가락 / 이동: 드래그)
        </div>

        <svg class="w-full h-full pointer-events-none" style="overflow: visible;">
          <g :style="{ transform: transformStyle, transition: isPanning ? 'none' : 'transform 0.1s ease-out' }" class="origin-top-left">
            <!-- 1. 연결선 (Cubic Bezier) -->
            <g>
              <path
                v-for="(link, idx) in renderLinks"
                :key="idx"
                :d="getBezierPath(link)"
                fill="none"
                stroke="#d1d5db"
                stroke-width="2"
                stroke-dasharray="1 1"
              />
            </g>

            <!-- 2. 노드 렌더링 -->
            <g v-for="node in renderNodes" :key="node.id" class="pointer-events-auto">
              <foreignObject
                :x="node.x"
                :y="node.y"
                width="190"
                height="65"
                class="overflow-visible"
              >
                <!-- 회사 노드 -->
                <div
                  v-if="node.type === 'COMPANY'"
                  @dragover="onDragOver"
                  @drop.stop="onCompanyDrop($event)"
                  class="node-card border-2 border-primary  shadow-md rounded-lg p-2.5 flex items-center gap-2.5 h-[50px] w-[180px] hover:shadow-lg transition-shadow"
                >
                  <div class="bg-primary/10 p-1.5 rounded text-primary flex items-center justify-center shrink-0">
                    <IconifyIcon icon="lucide:building-2" class="size-5" />
                  </div>
                  <div class="overflow-hidden flex-1">
                    <div class="text-xs font-bold text-gray-400">회사</div>
                    <div class="text-sm font-extrabold text-primary truncate" :title="node.name">{{ node.name }}</div>
                  </div>
                  <!--
                    회사 직속 부서를 만든다. no-pan 이 없으면 누를 때 캔버스가 끌린다.
                  -->
                  <button
                    type="button"
                    class="no-pan shrink-0 rounded p-1 text-primary hover:bg-primary/10"
                    title="회사 직속 부서 추가"
                    @click.stop="openAddDept()"
                  >
                    <IconifyIcon icon="lucide:folder-plus" class="size-4" />
                  </button>
                </div>

                <!-- 부서 노드 (드래그/드롭 타겟) -->
                <div
                  v-else-if="node.type === 'DEPT'"
                  draggable="true"
                  @dragstart="onNodeDragStart($event, DRAG_TYPE_DEPT, node.id)"
                  @dragover="onDragOver"
                  @drop.stop="onNodeDrop($event, node.id)"
                  class="node-card border border-teal-500 shadow rounded-lg p-2 flex items-center gap-2 h-[50px] w-[180px] hover:border-teal-600 hover:shadow-md transition-all cursor-move active:opacity-65"
                >
                  <div class=" p-1.5 rounded text-teal-600 flex items-center justify-center shrink-0">
                    <IconifyIcon icon="lucide:folder-open" class="size-4" />
                  </div>
                  <div class="overflow-hidden flex-1">
                    <div class="text-[10px] font-semibold text-gray-400">부서</div>
                    <div class="text-xs font-bold text-gray-700 truncate" :title="node.name">{{ node.name }}</div>
                  </div>
                  <button
                    type="button"
                    class="no-pan shrink-0 rounded p-1 text-teal-600 hover:bg-teal-50"
                    title="하위 부서 추가"
                    @click.stop="openAddDept({ id: node.id, name: node.name })"
                  >
                    <IconifyIcon icon="lucide:folder-plus" class="size-4" />
                  </button>
                  <!-- 드래그의 터치 대체 경로. 팝업에서 대상 부서를 고른다. -->
                  <button
                    type="button"
                    class="no-pan shrink-0 rounded p-1 text-teal-600 hover:bg-teal-50"
                    title="다른 부서로 이동"
                    @click.stop="openMovePicker('DEPT', node.id, node.name)"
                  >
                    <IconifyIcon icon="lucide:move" class="size-4" />
                  </button>
                </div>

                <!-- 사용자 노드 (드래그) -->
                <div
                  v-else-if="node.type === 'USER'"
                  draggable="true"
                  @dragstart="onNodeDragStart($event, DRAG_TYPE_USER, node.id)"
                  class="node-card border border-gray-300 bg-amber-50/90 shadow-sm rounded-lg p-2 flex items-center gap-2 h-[50px] w-[180px] hover:border-amber-500 hover:shadow-md transition-all cursor-move active:opacity-65"
                >
                  <!-- 사진이 있으면 사진, 없으면 이름 첫 글자 -->
                  <Avatar
                    :size="30"
                    :src="avatarUrl(node) || undefined"
                    class="shrink-0 bg-amber-100 text-amber-700"
                  >
                    {{ avatarText(node) }}
                  </Avatar>
                  <div class="overflow-hidden flex-1">
                    <div class="text-xs font-bold text-gray-800 truncate" :title="node.name">{{ node.name }}</div>
                    <div class="text-[9px] text-gray-500 truncate" :title="node.email || node.loginId">{{ node.email || node.loginId }}</div>
                  </div>
                  <!-- 드래그의 터치 대체 경로. 팝업에서 대상 부서를 고른다. -->
                  <button
                    type="button"
                    class="no-pan shrink-0 rounded p-1 text-amber-700 hover:bg-amber-100"
                    title="다른 부서로 이동"
                    @click.stop="openMovePicker('USER', node.id, node.name)"
                  >
                    <IconifyIcon icon="lucide:move" class="size-4" />
                  </button>
                </div>
              </foreignObject>
            </g>
          </g>
        </svg>

        <!-- 로딩 표시 -->
        <div v-if="loading" class="absolute inset-0 flex items-center justify-center pointer-events-none">
          <div class="flex flex-col items-center gap-2">
            <IconifyIcon icon="lucide:loader-2" class="size-8 text-primary animate-spin" />
            <span class="text-xs font-semibold text-gray-500">조직도 분석 중...</span>
          </div>
        </div>
      </div>

      <!--
        미소속 사용자 목록.

        각 행은 부서 노드가 이미 받아들이는 것과 **같은 형식**으로 끌기 값을 싣는다
        (`{ type: 'USER', id }`). 그래서 조직도 쪽 드롭 처리를 손대지 않고 그대로 쓴다.
      -->
      <div
        v-if="showUnassigned"
        class="no-pan w-64 shrink-0 border rounded-xl flex flex-col overflow-hidden"
      >
        <div class="px-3 py-2 border-b flex items-center justify-between gap-2">
          <div class="flex items-center gap-1.5 min-w-0">
            <IconifyIcon icon="lucide:user-x" class="size-4 text-gray-400 shrink-0" />
            <span class="text-sm font-semibold truncate">미소속 사용자</span>
            <span class="text-xs text-gray-400 shrink-0">
              {{ unassignedUsers.length }}
            </span>
          </div>
          <Button
            type="text"
            size="small"
            :loading="loadingUnassigned"
            @click="fetchUnassignedUsers"
          >
            <template #icon>
              <IconifyIcon icon="lucide:refresh-cw" class="size-3.5" />
            </template>
          </Button>
        </div>

        <div class="px-3 py-2 text-[11px] leading-snug text-gray-400 border-b">
          어느 회사에도 소속되지 않은 사용자입니다.
          <b>부서 노드로 끌어다 놓거나</b> 행의 이동 버튼을 누르면 그 부서 소속이 됩니다.
        </div>

        <div class="flex-1 overflow-y-auto p-2 flex flex-col gap-1.5">
          <div
            v-for="user in unassignedUsers"
            :key="user.id"
            draggable="true"
            class="border rounded-md px-2 py-1.5 cursor-move hover:border-primary hover:shadow-sm transition-all flex items-center gap-2"
            @dragstart="onNodeDragStart($event, DRAG_TYPE_USER, user.id)"
          >
            <Avatar :size="30" :src="avatarUrl(user) || undefined" class="shrink-0">
              {{ avatarText(user) }}
            </Avatar>
            <div class="min-w-0 flex-1">
              <div class="text-sm font-medium truncate" :title="user.userName">
                {{ user.userName || user.loginId }}
              </div>
              <div class="text-[11px] text-gray-400 truncate" :title="user.loginId">
                {{ user.loginId }}
              </div>
              <div class="text-[11px] text-gray-400 truncate" :title="user.email">
                {{ user.email || '-' }}
              </div>
              <div class="text-[11px] text-gray-400 truncate" :title="user.phone">
                {{ user.phone || '-' }}
              </div>
            </div>
            <!-- 드래그의 터치 대체 경로. 팝업에서 대상 부서를 고른다. -->
            <button
              type="button"
              class="shrink-0 rounded p-1 text-gray-400 hover:bg-primary/10 hover:text-primary"
              title="부서로 이동"
              @click.stop="openMovePicker('USER', user.id, user.userName || user.loginId)"
            >
              <IconifyIcon icon="lucide:move" class="size-4" />
            </button>
          </div>

          <div
            v-if="!loadingUnassigned && unassignedUsers.length === 0"
            class="py-8 text-center text-xs text-gray-400"
          >
            미소속 사용자가 없습니다.
          </div>
        </div>
      </div>
      </div>
    </div>

    <!-- 부서 추가 -->
    <Modal
      v-model:open="addDeptOpen"
      :confirm-loading="addDeptSaving"
      :title="
        addDeptParent
          ? `[${addDeptParent.name}] 하위 부서 추가`
          : `[${selectedCompanyName}] 회사 직속 부서 추가`
      "
      ok-text="만들기"
      cancel-text="취소"
      @ok="submitAddDept"
    >
      <div class="py-2">
        <div class="mb-2 text-xs text-gray-500">
          {{
            addDeptParent
              ? `'${addDeptParent.name}' 아래에 새 부서를 만듭니다.`
              : '회사 바로 아래에 새 부서를 만듭니다.'
          }}
        </div>
        <Input
          v-model:value="addDeptName"
          placeholder="부서명"
          @press-enter="submitAddDept"
        />
      </div>
    </Modal>

    <!--
      이동 대상 선택 — 드래그의 터치(모바일) 대체 경로.
      끌어다 놓는 대신 여기서 대상 부서를 고른다. 실행되는 API 는 드롭과 같다.
    -->
    <Modal
      v-model:open="moveOpen"
      :confirm-loading="moveSaving"
      :title="
        moveSource
          ? `[${moveSource.name}] ${moveSource.type === 'DEPT' ? '부서' : '사용자'} 이동`
          : '이동'
      "
      ok-text="이동"
      cancel-text="취소"
      @ok="submitMove"
    >
      <div class="py-2">
        <div class="mb-2 text-xs text-gray-500">
          {{
            moveSource?.type === 'DEPT'
              ? '이 부서(하위 부서·소속 사용자 포함)를 옮길 곳을 선택해 주세요.'
              : '이 사용자를 소속시킬 부서를 선택해 주세요.'
          }}
        </div>
        <div class="max-h-80 overflow-y-auto rounded-md border p-1.5 flex flex-col gap-1">
          <!-- 부서는 회사 직속으로도 옮길 수 있다 (드롭의 onCompanyDrop 과 같다) -->
          <div
            v-if="moveSource?.type === 'DEPT'"
            class="cursor-pointer rounded border px-2 py-1.5 flex items-center gap-2 transition-colors"
            :class="
              moveTargetId === MOVE_TO_COMPANY
                ? 'border-primary bg-primary/5'
                : 'hover:border-primary/40'
            "
            @click="moveTargetId = MOVE_TO_COMPANY"
          >
            <IconifyIcon icon="lucide:building-2" class="size-4 text-primary shrink-0" />
            <span class="text-sm font-medium truncate">
              [회사 직속] {{ selectedCompanyName }}
            </span>
          </div>

          <div
            v-for="opt in moveDeptOptions"
            :key="opt.id"
            class="cursor-pointer rounded border px-2 py-1.5 flex items-center gap-2 transition-colors"
            :class="
              moveTargetId === opt.id ? 'border-primary bg-primary/5' : 'hover:border-primary/40'
            "
            :style="{ paddingLeft: `${8 + opt.depth * 16}px` }"
            @click="moveTargetId = opt.id"
          >
            <IconifyIcon icon="lucide:folder-open" class="size-4 text-teal-600 shrink-0" />
            <span class="text-sm truncate">{{ opt.name }}</span>
          </div>

          <div
            v-if="moveDeptOptions.length === 0 && moveSource?.type !== 'DEPT'"
            class="py-6 text-center text-xs text-gray-400"
          >
            옮길 수 있는 부서가 없습니다.
          </div>
        </div>
      </div>
    </Modal>
  </Page>
</template>

<style scoped>
.node-card {
  box-sizing: border-box;
}
</style>
