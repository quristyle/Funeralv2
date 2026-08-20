<script lang="ts" setup>
import type { WbsLink, WbsTreeNode } from '#/api/helpdesk';

import { computed, ref } from 'vue';

import { Empty, Segmented, Space, Tooltip } from 'ant-design-vue';

/**
 * WBS 간트 차트.
 *
 * 원본(JinReception)은 dhtmlx-gantt 를 썼지만 funeralv2 는 Ant Design Vue 로 통일하기로 해
 * 같은 기능을 직접 구현했다. 지원하는 동작:
 *  - 계층 트리 + 접기/펼치기
 *  - 막대 드래그로 일정 이동, 좌우 끝 드래그로 기간 조정
 *  - 진행률 오버레이
 *  - 선후행 연결선(SVG)
 *
 * `readonly` 를 켜면 드래그가 막힌다(원본 WbsReadonly.vue 대응).
 */

const props = withDefaults(
  defineProps<{
    links?: WbsLink[];
    nodes: WbsTreeNode[];
    readonly?: boolean;
    showLinks?: boolean;
  }>(),
  { links: () => [], readonly: false, showLinks: true },
);

const emit = defineEmits<{
  /** 막대를 옮기거나 늘렸을 때. 저장은 부모가 한다. */
  change: [payload: { planEnd: string; planStart: string; wbsRid: number }];
  select: [wbsRid: number];
}>();

/** 눈금 단위 */
const scale = ref<'day' | 'month' | 'week'>('day');
const SCALE_OPTIONS = [
  { label: '일', value: 'day' },
  { label: '주', value: 'week' },
  { label: '월', value: 'month' },
];

/** 눈금 한 칸의 픽셀 폭 */
const CELL_WIDTH = { day: 28, month: 8, week: 12 } as const;
const ROW_HEIGHT = 32;
const NAME_WIDTH = 260;

const collapsed = ref<Set<string>>(new Set());

const DAY_MS = 86_400_000;

function parseDay(value?: null | string) {
  if (!value) return null;
  const d = new Date(String(value).slice(0, 10));
  return Number.isNaN(d.getTime()) ? null : d;
}

function toIso(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** 트리를 화면에 그릴 순서대로 편다. 접힌 노드의 자식은 건너뛴다. */
interface FlatRow {
  depth: number;
  hasChildren: boolean;
  key: string;
  node: WbsTreeNode;
}

const flatRows = computed<FlatRow[]>(() => {
  const rows: FlatRow[] = [];

  const walk = (nodes: WbsTreeNode[], depth: number) => {
    nodes.forEach((node) => {
      const hasChildren = Boolean(node.children?.length);
      rows.push({ depth, hasChildren, key: node.key, node });
      if (hasChildren && !collapsed.value.has(node.key)) {
        walk(node.children!, depth + 1);
      }
    });
  };

  walk(props.nodes, 0);
  return rows;
});

/** 전체 일정 범위. 앞뒤로 며칠 여유를 둔다. */
const range = computed(() => {
  let min: Date | null = null;
  let max: Date | null = null;

  const visit = (nodes: WbsTreeNode[]) => {
    nodes.forEach((n) => {
      const s = parseDay(n.data.planStart);
      const e = parseDay(n.data.planEnd);
      if (s && (!min || s < min)) min = s;
      if (e && (!max || e > max)) max = e;
      if (n.children?.length) visit(n.children);
    });
  };
  visit(props.nodes);

  const today = new Date();
  const start = new Date(min ?? today);
  const end = new Date(max ?? today);
  start.setDate(start.getDate() - 3);
  end.setDate(end.getDate() + 3);

  const days = Math.max(
    1,
    Math.round((end.getTime() - start.getTime()) / DAY_MS) + 1,
  );
  return { days, start };
});

const cellWidth = computed(() => CELL_WIDTH[scale.value]);
const timelineWidth = computed(() => range.value.days * cellWidth.value);

/** 눈금 라벨 */
const ticks = computed(() => {
  const out: { label: string; left: number; major: boolean }[] = [];
  const { days, start } = range.value;

  for (let i = 0; i < days; i++) {
    const d = new Date(start.getTime() + i * DAY_MS);
    const left = i * cellWidth.value;

    if (scale.value === 'day') {
      out.push({ label: String(d.getDate()), left, major: d.getDay() === 1 });
    } else if (scale.value === 'week') {
      if (d.getDay() === 1) {
        out.push({ label: `${d.getMonth() + 1}/${d.getDate()}`, left, major: true });
      }
    } else if (d.getDate() === 1) {
      out.push({ label: `${d.getFullYear()}.${d.getMonth() + 1}`, left, major: true });
    }
  }
  return out;
});

/** 주말 배경 */
const weekendBands = computed(() => {
  if (scale.value !== 'day') return [];
  const bands: { left: number }[] = [];
  const { days, start } = range.value;
  for (let i = 0; i < days; i++) {
    const d = new Date(start.getTime() + i * DAY_MS);
    if (d.getDay() === 0 || d.getDay() === 6) {
      bands.push({ left: i * cellWidth.value });
    }
  }
  return bands;
});

/** 막대 위치 계산 */
function barOf(node: WbsTreeNode) {
  const s = parseDay(node.data.planStart);
  const e = parseDay(node.data.planEnd) ?? s;
  if (!s || !e) return null;

  const offsetDays = Math.round(
    (s.getTime() - range.value.start.getTime()) / DAY_MS,
  );
  const spanDays = Math.max(1, Math.round((e.getTime() - s.getTime()) / DAY_MS) + 1);

  return {
    left: offsetDays * cellWidth.value,
    width: spanDays * cellWidth.value,
  };
}

/** 연결선 좌표. source→target 막대의 오른쪽 끝에서 왼쪽 끝으로 잇는다. */
const linkPaths = computed(() => {
  if (!props.showLinks) return [];

  const rowIndex = new Map<number, number>();
  flatRows.value.forEach((r, i) => rowIndex.set(r.node.data.wbsRid, i));

  return props.links
    .map((link) => {
      const si = rowIndex.get(link.source);
      const ti = rowIndex.get(link.target);
      if (si === undefined || ti === undefined) return null;

      const sourceRow = flatRows.value[si];
      const targetRow = flatRows.value[ti];
      if (!sourceRow || !targetRow) return null;
      const sBar = barOf(sourceRow.node);
      const tBar = barOf(targetRow.node);
      if (!sBar || !tBar) return null;

      const x1 = sBar.left + sBar.width;
      const y1 = si * ROW_HEIGHT + ROW_HEIGHT / 2;
      const x2 = tBar.left;
      const y2 = ti * ROW_HEIGHT + ROW_HEIGHT / 2;
      const midX = Math.max(x1 + 8, x2 - 8);

      return {
        id: link.id,
        points: `${x1},${y1} ${midX},${y1} ${midX},${y2} ${x2},${y2}`,
      };
    })
    .filter(Boolean) as { id: number; points: string }[];
});

function toggleCollapse(key: string) {
  const next = new Set(collapsed.value);
  if (next.has(key)) {
    next.delete(key);
  } else {
    next.add(key);
  }
  collapsed.value = next;
}

// ── 막대 드래그 ────────────────────────────────────────────
interface DragState {
  bar: { left: number; width: number };
  mode: 'move' | 'resize-end' | 'resize-start';
  node: WbsTreeNode;
  startX: number;
}

const drag = ref<DragState | null>(null);
/** 드래그 중 미리보기 오프셋(픽셀) */
const dragPreview = ref<{ left: number; width: number } | null>(null);

function onBarPointerDown(
  event: PointerEvent,
  node: WbsTreeNode,
  mode: DragState['mode'],
) {
  if (props.readonly) return;

  const bar = barOf(node);
  if (!bar) return;

  event.preventDefault();
  (event.target as HTMLElement).setPointerCapture?.(event.pointerId);
  drag.value = { bar, mode, node, startX: event.clientX };
  dragPreview.value = { ...bar };
}

function onPointerMove(event: PointerEvent) {
  const state = drag.value;
  if (!state) return;

  const deltaDays = Math.round(
    (event.clientX - state.startX) / cellWidth.value,
  );
  const delta = deltaDays * cellWidth.value;

  if (state.mode === 'move') {
    dragPreview.value = { left: state.bar.left + delta, width: state.bar.width };
  } else if (state.mode === 'resize-start') {
    const width = Math.max(cellWidth.value, state.bar.width - delta);
    dragPreview.value = { left: state.bar.left + delta, width };
  } else {
    dragPreview.value = {
      left: state.bar.left,
      width: Math.max(cellWidth.value, state.bar.width + delta),
    };
  }
}

function onPointerUp() {
  const state = drag.value;
  const preview = dragPreview.value;
  drag.value = null;
  dragPreview.value = null;
  if (!state || !preview) return;

  // 픽셀 위치를 날짜로 되돌린다.
  const startDayOffset = Math.round(preview.left / cellWidth.value);
  const spanDays = Math.max(1, Math.round(preview.width / cellWidth.value));

  const newStart = new Date(range.value.start.getTime() + startDayOffset * DAY_MS);
  const newEnd = new Date(newStart.getTime() + (spanDays - 1) * DAY_MS);

  const oldStart = parseDay(state.node.data.planStart);
  const oldEnd = parseDay(state.node.data.planEnd);
  if (
    oldStart &&
    oldEnd &&
    toIso(newStart) === toIso(oldStart) &&
    toIso(newEnd) === toIso(oldEnd)
  ) {
    return; // 변화 없음
  }

  emit('change', {
    planEnd: toIso(newEnd),
    planStart: toIso(newStart),
    wbsRid: state.node.data.wbsRid,
  });
}

/** 드래그 중인 막대인지 */
function isDragging(node: WbsTreeNode) {
  return drag.value?.node.data.wbsRid === node.data.wbsRid;
}

function displayBar(node: WbsTreeNode) {
  return isDragging(node) && dragPreview.value
    ? dragPreview.value
    : barOf(node);
}
</script>

<template>
  <div>
    <div class="mb-2 flex items-center justify-between">
      <Space>
        <Segmented v-model:value="scale" :options="SCALE_OPTIONS" size="small" />
      </Space>
      <span v-if="readonly" class="text-xs text-muted-foreground">
        읽기 전용
      </span>
      <span v-else class="text-xs text-muted-foreground">
        막대를 끌어 옮기고, 양 끝을 끌어 기간을 조정합니다.
      </span>
    </div>

    <Empty v-if="flatRows.length === 0" description="표시할 작업이 없습니다." />

    <div
      v-else
      class="flex overflow-auto rounded border border-border"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
    >
      <!-- 작업명 고정 열 -->
      <div
        class="sticky left-0 z-10 shrink-0 border-r border-border bg-background"
        :style="{ width: `${NAME_WIDTH}px` }"
      >
        <div
          class="border-b border-border bg-muted/40 px-2 text-xs font-medium leading-[38px]"
        >
          작업명
        </div>
        <div
          v-for="row in flatRows"
          :key="row.key"
          class="flex items-center gap-1 border-b border-border px-2 text-xs"
          :style="{ height: `${ROW_HEIGHT}px`, paddingLeft: `${8 + row.depth * 14}px` }"
        >
          <button
            v-if="row.hasChildren"
            class="w-4 shrink-0 text-muted-foreground"
            type="button"
            @click="toggleCollapse(row.key)"
          >
            {{ collapsed.has(row.key) ? '▸' : '▾' }}
          </button>
          <span v-else class="w-4 shrink-0"></span>
          <span class="truncate" @click="emit('select', row.node.data.wbsRid)">
            {{ row.node.data.wbsName }}
          </span>
        </div>
      </div>

      <!-- 타임라인 -->
      <div class="relative" :style="{ width: `${timelineWidth}px` }">
        <!-- 눈금 -->
        <div class="relative h-[38px] border-b border-border bg-muted/40">
          <span
            v-for="tick in ticks"
            :key="tick.left"
            class="absolute top-0 text-[10px] leading-[38px] text-muted-foreground"
            :class="tick.major ? 'font-medium' : ''"
            :style="{ left: `${tick.left + 2}px` }"
          >
            {{ tick.label }}
          </span>
        </div>

        <div
          class="relative"
          :style="{ height: `${flatRows.length * ROW_HEIGHT}px` }"
        >
          <!-- 주말 -->
          <div
            v-for="band in weekendBands"
            :key="`w-${band.left}`"
            class="absolute top-0 h-full bg-muted/40"
            :style="{ left: `${band.left}px`, width: `${cellWidth}px` }"
          ></div>

          <!-- 행 구분선 -->
          <div
            v-for="(row, index) in flatRows"
            :key="`r-${row.key}`"
            class="absolute left-0 w-full border-b border-border"
            :style="{ height: `${ROW_HEIGHT}px`, top: `${index * ROW_HEIGHT}px` }"
          ></div>

          <!-- 연결선 -->
          <svg
            v-if="showLinks"
            class="pointer-events-none absolute left-0 top-0 h-full w-full"
          >
            <polyline
              v-for="path in linkPaths"
              :key="path.id"
              fill="none"
              :points="path.points"
              stroke="currentColor"
              stroke-width="1"
              class="text-muted-foreground"
            />
          </svg>

          <!-- 막대 -->
          <template v-for="(row, index) in flatRows" :key="`b-${row.key}`">
            <Tooltip
              v-if="displayBar(row.node)"
              :title="`${row.node.data.wbsName} (${row.node.data.progress ?? 0}%)`"
            >
              <div
                class="absolute rounded"
                :class="[
                  row.hasChildren ? 'bg-slate-500' : 'bg-blue-500',
                  readonly ? '' : 'cursor-move',
                ]"
                :style="{
                  height: '18px',
                  left: `${displayBar(row.node)!.left}px`,
                  top: `${index * ROW_HEIGHT + 7}px`,
                  width: `${displayBar(row.node)!.width}px`,
                }"
                @pointerdown="onBarPointerDown($event, row.node, 'move')"
              >
                <!-- 진행률 -->
                <div
                  class="h-full rounded-l bg-green-500/80"
                  :style="{ width: `${Math.min(100, row.node.data.progress ?? 0)}%` }"
                ></div>

                <!-- 좌우 리사이즈 손잡이 -->
                <template v-if="!readonly">
                  <div
                    class="absolute left-0 top-0 h-full w-1.5 cursor-ew-resize"
                    @pointerdown.stop="
                      onBarPointerDown($event, row.node, 'resize-start')
                    "
                  ></div>
                  <div
                    class="absolute right-0 top-0 h-full w-1.5 cursor-ew-resize"
                    @pointerdown.stop="
                      onBarPointerDown($event, row.node, 'resize-end')
                    "
                  ></div>
                </template>
              </div>
            </Tooltip>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>
