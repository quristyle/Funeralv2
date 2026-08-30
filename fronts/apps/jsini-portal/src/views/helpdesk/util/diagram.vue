<script lang="ts" setup>
import type { StencilGroup } from './modules/stencils';

import type { Project, WbsTreeNode } from '#/api/helpdesk';

import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Collapse,
  CollapsePanel,
  Empty,
  message,
  Select,
  Space,
  Spin,
  Switch,
  Tooltip,
  TreeSelect,
} from 'ant-design-vue';

import {
  getProjects,
  getWbsDiagram,
  getWbsTree,
  saveWbsDiagram,
} from '#/api/helpdesk';

/**
 * [다이어그램]
 *
 * 원본(JinReception utils/DiagramEditor.vue, `/diagram`).
 * WBS 항목마다 구성도를 그려 저장한다.
 *
 * 그래프 엔진은 원본과 같은 @maxgraph/core 를 그대로 쓴다. 저장된 다이어그램이
 * maxgraph XML 이라 엔진을 바꾸면 기존 자료를 읽을 수 없기 때문이다.
 * 화면 껍데기(도구 모음·도형 팔레트·트리 선택)만 Ant Design Vue 로 다시 만들었다.
 *
 * 도형 라이브러리(stencils)는 원본 정의 파일을 그대로 가져왔다.
 */

const route = useRoute();

const loading = ref(false);
const saving = ref(false);

const projects = ref<Project[]>([]);
const selectedProjectId = ref<number | undefined>();
const wbsNodes = ref<WbsTreeNode[]>([]);
const selectedWbsRid = ref<number | undefined>();

const container = ref<HTMLDivElement | null>(null);
const minimapContainer = ref<HTMLDivElement | null>(null);

/** maxgraph 인스턴스들. 동적 로딩이라 타입은 느슨하게 둔다. */
let graph: any = null;
let undoManager: any = null;
let outline: any = null;

const showPalette = ref(true);
const showMinimap = ref(false);
const autoSaveEnabled = ref(true);
/** 불러오는 중에는 자동 저장을 막는다(원본의 isLoadingDiagram). */
let isLoadingDiagram = false;

const gridStyle = ref<'dot' | 'line' | 'none'>('dot');
const GRID_OPTIONS = [
  { label: '점 격자', value: 'dot' },
  { label: '선 격자', value: 'line' },
  { label: '없음', value: 'none' },
];

const stencilGroups = ref<StencilGroup[]>([]);

/** WBS 트리를 TreeSelect 가 쓰는 형태로 바꾼다. */
function toTreeData(nodes: WbsTreeNode[]): any[] {
  return nodes.map((n) => ({
    children: n.children?.length ? toTreeData(n.children) : undefined,
    label: n.data.wbsName,
    value: n.data.wbsRid,
  }));
}

const wbsTreeData = computed(() => toTreeData(wbsNodes.value));

/** 격자 배경 */
const canvasStyle = computed(() => {
  if (gridStyle.value === 'none') return {};
  if (gridStyle.value === 'line') {
    return {
      backgroundImage:
        'linear-gradient(to right, rgb(128 128 128 / 18%) 1px, transparent 1px), linear-gradient(to bottom, rgb(128 128 128 / 18%) 1px, transparent 1px)',
      backgroundSize: '16px 16px',
    };
  }
  return {
    backgroundImage:
      'radial-gradient(circle, rgb(128 128 128 / 25%) 1px, transparent 1px)',
    backgroundSize: '16px 16px',
  };
});

/** maxgraph 초기화. 번들이 커서 화면에 들어올 때 동적으로 불러온다. */
async function initGraph() {
  if (graph || !container.value) return;

  const {
    Graph,
    InternalEvent,
    RubberBandHandler,
    UndoManager,
    Outline,
  } = await import('@maxgraph/core');
  const { registerAllStencils, STENCIL_GROUPS } = await import(
    './modules/stencils'
  );

  // 도형 라이브러리를 레지스트리에 등록한다(한 번만 하면 된다).
  if (STENCIL_GROUPS[0]?.shapes.length === 0) registerAllStencils();
  stencilGroups.value = STENCIL_GROUPS;

  InternalEvent.disableContextMenu(container.value);

  const g = new Graph(container.value);
  g.setPanning(true);
  g.setConnectable(true);
  g.setCellsEditable(true);
  g.setAllowDanglingEdges(false);
  g.setHtmlLabels(true);
  // eslint-disable-next-line no-new
  new RubberBandHandler(g);

  // 되돌리기 / 다시하기
  const manager = new UndoManager();
  const undoListener = (_sender: any, evt: any) => {
    manager.undoableEditHappened(evt.getProperty('edit'));
  };
  g.getDataModel().addListener(InternalEvent.UNDO, undoListener);
  g.getView().addListener(InternalEvent.UNDO, undoListener);
  undoManager = manager;

  // 편집이 끝나는 시점마다 자동 저장한다. 원본과 같은 이벤트 목록.
  const autoSaveListener = () => {
    if (!autoSaveEnabled.value || isLoadingDiagram) return;
    void save(true);
  };
  [
    InternalEvent.MOVE_CELLS,
    InternalEvent.RESIZE_CELLS,
    InternalEvent.CELLS_ADDED,
    InternalEvent.CELLS_REMOVED,
    InternalEvent.CONNECT_CELL,
    InternalEvent.LABEL_CHANGED,
    InternalEvent.CELL_CONNECTED,
  ].forEach((evt) => g.addListener(evt, autoSaveListener));

  graph = g;

  if (minimapContainer.value) {
    outline = new Outline(g, minimapContainer.value);
  }
}

/** 선택한 WBS 의 다이어그램을 캔버스에 올린다. */
async function loadDiagram() {
  if (!selectedWbsRid.value) return;

  await initGraph();
  if (!graph) return;

  loading.value = true;
  isLoadingDiagram = true;
  try {
    const saved = await getWbsDiagram(selectedWbsRid.value).catch(() => null);

    graph.getDataModel().beginUpdate();
    try {
      graph.removeCells(
        graph.getChildCells(graph.getDefaultParent(), true, true),
      );

      const raw = saved?.diagramData;
      if (raw) {
        // 저장 형식은 { xml: "<GraphDataModel>..." } 이다.
        const parsed = JSON.parse(raw);
        if (parsed?.xml) {
          const { ModelXmlSerializer } = await import('@maxgraph/core');
          new ModelXmlSerializer(graph.getDataModel()).import(parsed.xml);
        }
      }
    } finally {
      graph.getDataModel().endUpdate();
    }
  } finally {
    loading.value = false;
    // endUpdate 가 만든 이벤트가 자동 저장을 부르지 않도록 한 틱 뒤에 푼다.
    setTimeout(() => {
      isLoadingDiagram = false;
    }, 0);
  }
}

/**
 * 저장한다.
 * @param silent 자동 저장일 때는 알림을 띄우지 않는다.
 */
async function save(silent = false) {
  if (!selectedWbsRid.value || !graph) return;

  saving.value = true;
  try {
    const { ModelXmlSerializer } = await import('@maxgraph/core');
    const xml = new ModelXmlSerializer(graph.getDataModel()).export();

    await saveWbsDiagram({
      diagramData: JSON.stringify({ xml }),
      wbsRid: selectedWbsRid.value,
    });
    if (!silent) message.success('다이어그램을 저장했습니다.');
  } finally {
    saving.value = false;
  }
}

// ── 도형 추가 ─────────────────────────────────────────────

/** 기본 도형 */
function addVertex(label: string, w: number, h: number, shapeStyle: string) {
  if (!graph) return;

  graph.getDataModel().beginUpdate();
  try {
    graph.insertVertex({
      parent: graph.getDefaultParent(),
      position: [40, 40],
      size: [w, h],
      style: shapeStyle ? { shape: shapeStyle } : {},
      value: label,
    });
  } finally {
    graph.getDataModel().endUpdate();
  }
}

/** 팔레트에서 고른 stencil 도형 */
function addStencil(shape: { h: number; label: string; registryName: string; w: number }) {
  if (!graph) return;

  graph.getDataModel().beginUpdate();
  try {
    graph.insertVertex({
      parent: graph.getDefaultParent(),
      position: [60, 60],
      size: [shape.w, shape.h],
      style: { shape: shape.registryName },
      value: '',
    });
  } finally {
    graph.getDataModel().endUpdate();
  }
}

function deleteSelection() {
  graph?.removeCells(graph.getSelectionCells());
}

function zoom(direction: 'in' | 'out' | 'reset') {
  if (!graph) return;
  if (direction === 'in') graph.zoomIn();
  else if (direction === 'out') graph.zoomOut();
  else graph.zoomActual();
}

function undo() {
  undoManager?.undo();
}

function redo() {
  undoManager?.redo();
}

watch(selectedProjectId, async (id) => {
  if (!id) {
    wbsNodes.value = [];
    selectedWbsRid.value = undefined;
    return;
  }
  wbsNodes.value = (await getWbsTree(id)) ?? [];
  selectedWbsRid.value = wbsNodes.value[0]?.data.wbsRid;
});

watch(selectedWbsRid, loadDiagram);

onMounted(async () => {
  projects.value = (await getProjects()) ?? [];

  const fromQuery = route.query.projectId
    ? Number(route.query.projectId)
    : undefined;
  selectedProjectId.value = fromQuery ?? projects.value[0]?.id;
});

onBeforeUnmount(() => {
  outline?.destroy?.();
  graph?.destroy?.();
  graph = null;
  outline = null;
  undoManager = null;
});
</script>

<template>
  <Page auto-content-height>
    <!-- 도구 모음 -->
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Select
            v-model:value="selectedProjectId"
            :options="projects.map((p) => ({ label: p.name, value: p.id }))"
            option-filter-prop="label"
            placeholder="프로젝트"
            show-search
            style="width: 190px"
          />
          <TreeSelect
            v-model:value="selectedWbsRid"
            :tree-data="wbsTreeData"
            placeholder="WBS 항목"
            show-search
            style="width: 250px; max-width: 100%"
            tree-default-expand-all
            tree-node-filter-prop="label"
          />
        </Space>

        <Space wrap>
          <Tooltip title="되돌리기">
            <Button @click="undo">↶</Button>
          </Tooltip>
          <Tooltip title="다시하기">
            <Button @click="redo">↷</Button>
          </Tooltip>
          <Button danger @click="deleteSelection">삭제</Button>
          <Button @click="zoom('in')">＋</Button>
          <Button @click="zoom('out')">－</Button>
          <Button @click="zoom('reset')">100%</Button>
          <Button
            :disabled="!selectedWbsRid"
            :loading="saving"
            type="primary"
            @click="save(false)"
          >
            저장
          </Button>
        </Space>
      </div>

      <div class="mt-2 flex flex-wrap items-center gap-4">
        <Space>
          <span class="text-xs">도형 팔레트</span>
          <Switch v-model:checked="showPalette" size="small" />
        </Space>
        <Space>
          <span class="text-xs">미니맵</span>
          <Switch v-model:checked="showMinimap" size="small" />
        </Space>
        <Space>
          <span class="text-xs">자동 저장</span>
          <Switch v-model:checked="autoSaveEnabled" size="small" />
        </Space>
        <Space>
          <span class="text-xs">격자</span>
          <Select
            v-model:value="gridStyle"
            :options="GRID_OPTIONS"
            size="small"
            style="width: 110px"
          />
        </Space>
      </div>
    </Card>

    <div class="flex gap-3">
      <!-- 도형 팔레트 -->
      <Card
        v-show="showPalette"
        :body-style="{ maxHeight: '600px', overflowY: 'auto', padding: '8px' }"
        class="w-64 shrink-0"
        size="small"
        title="도형"
      >
        <div class="mb-2 grid grid-cols-4 gap-1">
          <Tooltip title="사각형">
            <Button size="small" @click="addVertex('사각형', 80, 40, 'rectangle')">
              ▭
            </Button>
          </Tooltip>
          <Tooltip title="원형">
            <Button size="small" @click="addVertex('원', 60, 60, 'ellipse')">
              ◯
            </Button>
          </Tooltip>
          <Tooltip title="마름모">
            <Button size="small" @click="addVertex('마름모', 60, 60, 'rhombus')">
              ◇
            </Button>
          </Tooltip>
          <Tooltip title="삼각형">
            <Button size="small" @click="addVertex('삼각형', 60, 60, 'triangle')">
              △
            </Button>
          </Tooltip>
        </div>

        <Collapse accordion size="small">
          <CollapsePanel
            v-for="group in stencilGroups"
            :key="group.name"
            :header="`${group.name} (${group.shapes.length})`"
          >
            <div class="grid grid-cols-3 gap-1">
              <Tooltip
                v-for="shape in group.shapes"
                :key="shape.registryName"
                :title="shape.label"
              >
                <Button
                  class="truncate text-[10px]"
                  size="small"
                  @click="addStencil(shape)"
                >
                  {{ shape.label }}
                </Button>
              </Tooltip>
            </div>
          </CollapsePanel>
        </Collapse>
      </Card>

      <!-- 캔버스 -->
      <Card
        :body-style="{ padding: 0, position: 'relative' }"
        class="min-w-0 flex-1"
        size="small"
      >
        <Empty
          v-if="!selectedWbsRid"
          class="py-10"
          description="프로젝트와 WBS 항목을 선택하세요."
        />
        <Spin v-show="selectedWbsRid" :spinning="loading">
          <div
            ref="container"
            class="hd-diagram-canvas"
            :style="{ height: '600px', overflow: 'auto', ...canvasStyle }"
          ></div>

          <!-- 미니맵 -->
          <div
            v-show="showMinimap"
            ref="minimapContainer"
            class="absolute bottom-3 right-3 h-36 w-52 overflow-hidden rounded border border-border bg-background"
          ></div>
        </Spin>
      </Card>
    </div>
  </Page>
</template>

<style scoped>
.hd-diagram-canvas {
  position: relative;
  background-color: var(--background);
}
</style>
