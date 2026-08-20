<script lang="ts" setup>
import type { Project, WbsTreeNode } from '#/api/helpdesk';

import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Empty,
  message,
  Select,
  Space,
  Spin,
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
 * 원본(utils/DiagramEditor.vue). WBS 항목마다 구성도를 그려 저장한다.
 *
 * 그래프 엔진은 원본과 같은 @maxgraph/core 를 그대로 쓴다. 화면 껍데기(툴바·목록·버튼)만
 * Ant Design Vue 로 다시 만들었다. 이미 저장된 다이어그램이 maxgraph XML 이라
 * 엔진을 바꾸면 기존 10건을 읽을 수 없게 되기 때문이다.
 */

const loading = ref(false);
const saving = ref(false);

const projects = ref<Project[]>([]);
const selectedProjectId = ref<number | undefined>();
const wbsNodes = ref<WbsTreeNode[]>([]);
const selectedWbsRid = ref<number | undefined>();

const container = ref<HTMLDivElement | null>(null);
/** maxgraph 인스턴스. 타입은 동적 로딩이라 any 로 둔다. */
let graph: any = null;

/** 트리를 평평한 선택 목록으로 만든다. */
const wbsOptions = computed(() => {
  const out: { label: string; value: number }[] = [];

  const walk = (nodes: WbsTreeNode[], depth: number) => {
    nodes.forEach((n) => {
      out.push({
        label: `${'　'.repeat(depth)}${n.data.wbsName}`,
        value: n.data.wbsRid,
      });
      if (n.children?.length) walk(n.children, depth + 1);
    });
  };
  walk(wbsNodes.value, 0);
  return out;
});

/** maxgraph 를 초기화한다. 번들 크기가 커서 화면에 들어올 때 동적으로 불러온다. */
async function initGraph() {
  if (graph || !container.value) return;

  const { Graph, InternalEvent, RubberBandHandler } = await import(
    '@maxgraph/core'
  );

  InternalEvent.disableContextMenu(container.value);

  graph = new Graph(container.value);
  graph.setPanning(true);
  graph.setConnectable(true);
  graph.setCellsEditable(true);
  graph.setAllowDanglingEdges(false);
  // 드래그로 영역 선택
  // eslint-disable-next-line no-new
  new RubberBandHandler(graph);
}

/** 선택한 WBS 의 다이어그램을 불러와 캔버스에 올린다. */
async function loadDiagram() {
  if (!selectedWbsRid.value) return;

  await initGraph();
  if (!graph) return;

  loading.value = true;
  try {
    const saved = await getWbsDiagram(selectedWbsRid.value).catch(() => null);

    graph.getDataModel().beginUpdate();
    try {
      graph.removeCells(graph.getChildCells(graph.getDefaultParent(), true, true));

      const raw = saved?.diagramData;
      if (raw) {
        // 저장 형식은 { xml: "<GraphDataModel>..." } 이다.
        const parsed = JSON.parse(raw);
        if (parsed?.xml) {
          const { Codec, ModelXmlSerializer } = await import('@maxgraph/core');
          void Codec;
          new ModelXmlSerializer(graph.getDataModel()).import(parsed.xml);
        }
      }
    } finally {
      graph.getDataModel().endUpdate();
    }
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (!selectedWbsRid.value || !graph) return;

  saving.value = true;
  try {
    const { ModelXmlSerializer } = await import('@maxgraph/core');
    const xml = new ModelXmlSerializer(graph.getDataModel()).export();

    await saveWbsDiagram({
      diagramData: JSON.stringify({ xml }),
      wbsRid: selectedWbsRid.value,
    });
    message.success('다이어그램을 저장했습니다.');
  } finally {
    saving.value = false;
  }
}

/** 기본 도형을 캔버스 가운데에 추가한다. */
function addShape(kind: 'ellipse' | 'rect') {
  if (!graph) return;

  const parent = graph.getDefaultParent();
  graph.getDataModel().beginUpdate();
  try {
    graph.insertVertex({
      parent,
      position: [40, 40],
      size: [120, 50],
      style: kind === 'ellipse' ? { shape: 'ellipse' } : {},
      value: kind === 'ellipse' ? '원' : '상자',
    });
  } finally {
    graph.getDataModel().endUpdate();
  }
}

function deleteSelection() {
  if (!graph) return;
  graph.removeCells(graph.getSelectionCells());
}

function zoom(direction: 'in' | 'out' | 'reset') {
  if (!graph) return;
  if (direction === 'in') graph.zoomIn();
  else if (direction === 'out') graph.zoomOut();
  else graph.zoomActual();
}

watch(selectedProjectId, async (id) => {
  if (!id) {
    wbsNodes.value = [];
    selectedWbsRid.value = undefined;
    return;
  }
  wbsNodes.value = (await getWbsTree(id)) ?? [];
  selectedWbsRid.value = wbsOptions.value[0]?.value;
});

watch(selectedWbsRid, loadDiagram);

onMounted(async () => {
  projects.value = (await getProjects()) ?? [];
  selectedProjectId.value = projects.value[0]?.id;
});

onBeforeUnmount(() => {
  graph?.destroy?.();
  graph = null;
});
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Select
            v-model:value="selectedProjectId"
            :options="projects.map((p) => ({ label: p.name, value: p.id }))"
            option-filter-prop="label"
            placeholder="프로젝트"
            show-search
            style="width: 200px"
          />
          <Select
            v-model:value="selectedWbsRid"
            :options="wbsOptions"
            option-filter-prop="label"
            placeholder="WBS 항목"
            show-search
            style="width: 260px"
          />
        </Space>

        <Space wrap>
          <Button @click="addShape('rect')">상자</Button>
          <Button @click="addShape('ellipse')">원</Button>
          <Button danger @click="deleteSelection">삭제</Button>
          <Button @click="zoom('in')">＋</Button>
          <Button @click="zoom('out')">－</Button>
          <Button @click="zoom('reset')">100%</Button>
          <Button
            :disabled="!selectedWbsRid"
            :loading="saving"
            type="primary"
            @click="save"
          >
            저장
          </Button>
        </Space>
      </div>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Empty
        v-if="!selectedWbsRid"
        class="py-10"
        description="프로젝트와 WBS 항목을 선택하세요."
      />
      <Spin v-show="selectedWbsRid" :spinning="loading">
        <div
          ref="container"
          class="hd-diagram-canvas"
          :style="{ height: '600px', overflow: 'auto' }"
        ></div>
      </Spin>
    </Card>
  </Page>
</template>

<style scoped>
.hd-diagram-canvas {
  position: relative;
  background: var(--background);
  background-image: radial-gradient(circle, rgb(128 128 128 / 25%) 1px, transparent 1px);
  background-size: 16px 16px;
}
</style>
