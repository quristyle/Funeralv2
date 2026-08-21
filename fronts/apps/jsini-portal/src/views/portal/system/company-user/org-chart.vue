<script lang="ts" setup>
import { ref, watch, onMounted, computed, nextTick } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Button, Tooltip, message, Radio } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import BizSelect from '#/components/BizSelect.vue';
import { getCompanyList } from '#/api/portal/system/company';
import { getDeptList, getDeptUsers, moveDept, moveUserDept } from '#/api/portal/system/dept';

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
}

const orgTreeRoot = ref<OrgNode | null>(null);


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
      const items = (compRes as any)?.items ?? compRes ?? [];
      const matched = items.find((c: any) => c.id === selectedCompanyId.value);
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

    if (node.children.length === 0) {
      if (isVertical) {
        xCounter++;
      } else {
        yCounter++;
      }
    } else {
      node.children.forEach((child) => {
        traverse(child, renderNode);
      });
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
      const compRes = await getCompanyList();
      const items = (compRes as any)?.items ?? compRes ?? [];
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
            <Button class="flex items-center gap-1.5" @click="loadOrgData" :loading="loading">
              <template #icon>
                <IconifyIcon icon="lucide:rotate-cw" class="size-4" />
              </template>
              새로고침
            </Button>
            <Button class="flex items-center gap-1.5" @click="resetZoom">
              <template #icon>
                <IconifyIcon icon="lucide:refresh-cw" class="size-4" />
              </template>
              화면 맞춤
            </Button>
          </div>
        </div>
      </Card>

      <!-- 메인 2D 무한 마인드맵 영역 -->
      <div 
        ref="canvasRef"
        class="flex-1 border rounded-xl overflow-hidden relative select-none cursor-grab"
        :class="{ 'cursor-grabbing': isPanning }"
        @mousedown="onCanvasMouseDown"
        @mousemove="onCanvasMouseMove"
        @mouseup="onCanvasMouseUp"
        @mouseleave="onCanvasMouseUp"
        @wheel="onCanvasWheel"
      >
        <div class="absolute top-4 left-4 z-10 backdrop-blur px-3 py-1.5 rounded-md border text-xs text-gray-500 pointer-events-none">
          💡 노드를 드래그하여 다른 부서에 소속시킬 수 있습니다. (확대: 휠 / 이동: 마우스 드래그)
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
                  <div class="overflow-hidden">
                    <div class="text-xs font-bold text-gray-400">회사</div>
                    <div class="text-sm font-extrabold text-primary truncate" :title="node.name">{{ node.name }}</div>
                  </div>
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
                </div>

                <!-- 사용자 노드 (드래그) -->
                <div
                  v-else-if="node.type === 'USER'"
                  draggable="true"
                  @dragstart="onNodeDragStart($event, DRAG_TYPE_USER, node.id)"
                  class="node-card border border-gray-300 bg-amber-50/90 shadow-sm rounded-lg p-2 flex items-center gap-2 h-[50px] w-[180px] hover:border-amber-500 hover:shadow-md transition-all cursor-move active:opacity-65"
                >
                  <div class="bg-amber-100 p-1.5 rounded text-amber-700 flex items-center justify-center shrink-0">
                    <IconifyIcon icon="lucide:user" class="size-4" />
                  </div>
                  <div class="overflow-hidden flex-1">
                    <div class="text-xs font-bold text-gray-800 truncate" :title="node.name">{{ node.name }}</div>
                    <div class="text-[9px] text-gray-500 truncate" :title="node.email || node.loginId">{{ node.email || node.loginId }}</div>
                  </div>
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
    </div>
  </Page>
</template>

<style scoped>
.node-card {
  box-sizing: border-box;
}
</style>
