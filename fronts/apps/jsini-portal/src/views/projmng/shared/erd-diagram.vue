<script setup lang="ts">
/**
 * ERD / 플로우 다이어그램 — 이식 전 `QuriDiagram`(wwwroot/lib/mxgraph).
 *
 * 저장 형식은 이식 전과 같은 `ErdInfo` JSON 이다. DB(`sp_dev_db_prop_exec` 의
 * `db_pvalue`)에 이미 이 형식으로 쌓여 있어 바꿀 수 없다.
 *
 *   { entities: [{ id, name, desc, fields, x, y, w, h }],
 *     relations: [{ from, to, label }] }
 *
 * 엔진은 mxgraph 의 후속인 `@maxgraph/core` 를 쓴다 — 포털이 이미 쓰고 있는
 * 라이브러리다(헬프데스크 다이어그램 화면). 좌표를 그대로 읽고 쓰므로
 * 이식 전에 배치해 둔 도형 위치가 그대로 살아난다.
 */
import type { ErdEntity, ErdModel } from './erd-types';

import { onBeforeUnmount, ref, shallowRef } from 'vue';

interface Props {
  height?: number | string;
}

withDefaults(defineProps<Props>(), { height: '100%' });

const container = ref<HTMLElement>();
const graph = shallowRef<any>(null);
/** 셀 → 엔터티 id. 저장할 때 좌표를 되돌려 담기 위해 들고 있는다. */
const cellToEntity = new Map<any, ErdEntity>();

const DEFAULT_W = 180;
const DEFAULT_H = 60;

async function initGraph() {
  if (graph.value || !container.value) return;

  // 번들이 커서 화면에 들어올 때 불러온다(헬프데스크 다이어그램과 같은 방식).
  const { Graph, InternalEvent, RubberBandHandler } = await import(
    '@maxgraph/core'
  );

  InternalEvent.disableContextMenu(container.value);

  const g = new Graph(container.value);
  g.setPanning(true);
  g.setConnectable(true);
  g.setCellsEditable(false);
  g.setCellsResizable(true);
  g.setAllowDanglingEdges(false);
  g.setHtmlLabels(true);
  // eslint-disable-next-line no-new
  new RubberBandHandler(g);

  graph.value = g;
}

/** 모델을 캔버스에 그린다. 기존 도형은 모두 지운다. */
async function load(model: ErdModel) {
  await initGraph();
  const g = graph.value;
  if (!g) return;

  cellToEntity.clear();
  const byId = new Map<string, any>();

  g.getDataModel().beginUpdate();
  try {
    g.removeCells(g.getChildCells(g.getDefaultParent(), true, true));

    // 좌표가 없는 엔터티는 격자로 흩뿌린다. 겹쳐 쌓이면 아무것도 알아볼 수 없다.
    let autoIndex = 0;

    (model.entities ?? []).forEach((entity) => {
      const hasPos = Boolean(entity.x || entity.y);
      const x = hasPos ? (entity.x ?? 0) : 40 + (autoIndex % 5) * (DEFAULT_W + 30);
      const y = hasPos
        ? (entity.y ?? 0)
        : 40 + Math.floor(autoIndex / 5) * (DEFAULT_H + 40);
      if (!hasPos) autoIndex += 1;

      const label = entity.desc
        ? `${entity.name}\n${entity.desc}`
        : entity.name || entity.id;

      const cell = g.insertVertex({
        parent: g.getDefaultParent(),
        position: [x, y],
        size: [entity.w || DEFAULT_W, entity.h || DEFAULT_H],
        style: {
          fillColor: '#ffffff',
          strokeColor: '#8c8c8c',
          fontColor: '#262626',
          rounded: true,
          whiteSpace: 'wrap',
        },
        value: label,
      });

      byId.set(entity.id, cell);
      cellToEntity.set(cell, entity);
    });

    (model.relations ?? []).forEach((relation) => {
      const source = byId.get(relation.from);
      const target = byId.get(relation.to);
      if (!source || !target) return;

      g.insertEdge({
        parent: g.getDefaultParent(),
        source,
        target,
        value: relation.label ?? '',
      });
    });
  } finally {
    g.getDataModel().endUpdate();
  }
}

/**
 * 캔버스의 현재 상태를 모델로 되돌린다.
 *
 * 엔터티의 좌표·크기만 갱신한다. 이름·설명은 DB 메타에서 오는 값이라
 * 다이어그램에서 고치는 대상이 아니다(이식 전과 같다).
 */
function save(): ErdModel {
  const g = graph.value;
  const entities: ErdEntity[] = [];
  const relations: ErdModel['relations'] = [];

  if (!g) return { entities, relations };

  const idOfCell = new Map<any, string>();

  cellToEntity.forEach((entity, cell) => {
    const geo = g.getCellGeometry(cell);
    entities.push({
      ...entity,
      x: Math.round(geo?.x ?? entity.x ?? 0),
      y: Math.round(geo?.y ?? entity.y ?? 0),
      w: Math.round(geo?.width ?? entity.w ?? DEFAULT_W),
      h: Math.round(geo?.height ?? entity.h ?? DEFAULT_H),
    });
    idOfCell.set(cell, entity.id);
  });

  g.getChildCells(g.getDefaultParent(), false, true).forEach((edge: any) => {
    const from = idOfCell.get(edge.source);
    const to = idOfCell.get(edge.target);
    if (!from || !to) return;
    relations.push({ from, to, label: String(edge.value ?? '') });
  });

  return { entities, relations };
}

function zoomIn() {
  graph.value?.zoomIn();
}
function zoomOut() {
  graph.value?.zoomOut();
}
function fit() {
  graph.value?.fit();
  graph.value?.center();
}

onBeforeUnmount(() => {
  graph.value?.destroy?.();
  graph.value = null;
});

defineExpose({ load, save, zoomIn, zoomOut, fit });
</script>

<template>
  <div class="relative h-full w-full">
    <div
      ref="container"
      class="border-border h-full w-full overflow-hidden rounded-md border"
      :style="{
        height: typeof height === 'number' ? `${height}px` : height,
        backgroundImage:
          'radial-gradient(circle, rgb(128 128 128 / 25%) 1px, transparent 1px)',
        backgroundSize: '16px 16px',
      }"
    ></div>
  </div>
</template>
