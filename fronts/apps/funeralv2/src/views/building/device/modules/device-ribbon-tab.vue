<script lang="ts" setup>
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue';
import {
  Button, Spin, Alert, Tooltip, Popconfirm, InputNumber, message,
} from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import {
  getDeviceRibbons,
  getMediaSources,
  bulkSaveDeviceRibbons,
} from '#/api/building';
import type { BuildingApi } from '#/api/building';

// ────────────────────────────────────────────────────────────────────
// Props
// ────────────────────────────────────────────────────────────────────
const props = defineProps<{
  deviceId: string;
  /** 장비 속성의 화면 방향 (LANDSCAPE | PORTRAIT) */
  displayOrientation?: 'LANDSCAPE' | 'PORTRAIT';
}>();

// ────────────────────────────────────────────────────────────────────
// 상태
// ────────────────────────────────────────────────────────────────────
const loading = ref(false);
const saving = ref(false);

/** 현재 배치된 리본 목록 (편집 중인 상태) */
const placedRibbons = ref<BuildingApi.DeviceRibbon[]>([]);

/** 장식 목록 (IMAGE 타입 미디어소스) */
const decorations = ref<BuildingApi.MediaSource[]>([]);
const decorationsLoading = ref(false);

/** 선택된 리본 ID (편집 패널용) */
const selectedRibbonId = ref<string | null>(null);

/** 드래그 중인 데코레이션 (사이드패널에서 모니터로 드래그) */
const draggingDecoration = ref<BuildingApi.MediaSource | null>(null);

/** 리본 이동/리사이즈 중 상태 */
const isDragging = ref(false);
const isResizing = ref(false);
const dragTarget = ref<string | null>(null);
const dragStartX = ref(0);
const dragStartY = ref(0);
const dragStartLeft = ref(0);
const dragStartTop = ref(0);
const resizeStartWidth = ref(0);
const resizeStartHeight = ref(0);

/** 모니터 미리보기 DOM ref */
const monitorRef = ref<HTMLElement | null>(null);

/** 모니터 영역 컨테이너 DOM ref (ResizeObserver 대상) */
const monitorAreaRef = ref<HTMLElement | null>(null);

/** 동적으로 계산된 모니터 화면 크기 */
const monitorWidth = ref(0);
const monitorHeight = ref(0);

/** 가로 모니터 비율: 16:9 */
const LANDSCAPE_RATIO = 16 / 9;
/** 세로 모니터 비율: 9:16 */
const PORTRAIT_RATIO = 9 / 16;

/** 화면 방향에 따른 모니터 비율 */
const isPortrait = computed(() => props.displayOrientation === 'PORTRAIT');

/** 모니터 외관 스타일 (동적 크기) - width=0이면 최솟값으로 fallback */
const monitorScreenStyle = computed(() => {
  const w = monitorWidth.value > 0 ? monitorWidth.value : 480;
  const h = monitorHeight.value > 0 ? monitorHeight.value : 270;
  return {
    width: `${w}px`,
    height: `${h}px`,
  };
});

/**
 * 컨테이너 크기로부터 모니터 화면 크기를 계산합니다.
 * 종횡비를 유지하며 패딩을 제외한 가용 공간을 최대한 사용합니다.
 */
function calcMonitorSize(containerW: number, containerH: number) {
  // 헤더(라벨) + 카운트 텍스트 = 약 60px 제외
  const HEADER_H = 60;
  const availW = Math.max(containerW - 16, 100); // 좌우 패딩 8px
  const availH = Math.max(containerH - HEADER_H, 80);

  const ratio = isPortrait.value ? PORTRAIT_RATIO : LANDSCAPE_RATIO;

  // 가용 영역에 맞게 비율 유지하면서 최대 크기 계산
  let w = availW;
  let h = w / ratio;

  if (h > availH) {
    h = availH;
    w = h * ratio;
  }

  monitorWidth.value = Math.floor(w);
  monitorHeight.value = Math.floor(h);
}

/** ResizeObserver 연결 */
function attachResizeObserver() {
  if (!monitorAreaRef.value || resizeObserver) return;
  resizeObserver = new ResizeObserver((entries) => {
    const entry = entries[0];
    if (!entry) return;
    const { width, height } = entry.contentRect;
    if (width > 0) {
      calcMonitorSize(width, height);
    }
  });
  resizeObserver.observe(monitorAreaRef.value);
  // 연결 즉시 현재 크기로 계산
  const rect = monitorAreaRef.value.getBoundingClientRect();
  if (rect.width > 0) {
    calcMonitorSize(rect.width, rect.height);
  }
}

const selectedRibbon = computed(() =>
  placedRibbons.value.find((r) => r.id === selectedRibbonId.value) ?? null,
);

// ────────────────────────────────────────────────────────────────────
// 데이터 로드
// ────────────────────────────────────────────────────────────────────

async function loadRibbons() {
  loading.value = true;
  try {
    const res = await getDeviceRibbons(props.deviceId);
    placedRibbons.value = Array.isArray(res) ? res : (res as any)?.result ?? [];
  } catch (err) {
    message.error('리본 목록 로드 실패');
  } finally {
    loading.value = false;
  }
}

async function loadDecorations() {
  decorationsLoading.value = true;
  try {
    const res = await getMediaSources('IMAGE');
    decorations.value = Array.isArray(res) ? res : (res as any)?.result ?? [];
  } catch (err) {
    message.error('장식 목록 로드 실패');
  } finally {
    decorationsLoading.value = false;
  }
}

onMounted(() => {
  loadRibbons();
  loadDecorations();
});

let resizeObserver: ResizeObserver | null = null;

onUnmounted(() => {
  resizeObserver?.disconnect();
  resizeObserver = null;
});

/**
 * monitorAreaRef DOM이 생성된 시점(로딩 완료 후 v-else 블록이 렌더럁)에
 * ResizeObserver를 연결합니다.
 */
watch(monitorAreaRef, async (el) => {
  if (el) {
    await nextTick();
    attachResizeObserver();
  } else {
    resizeObserver?.disconnect();
    resizeObserver = null;
  }
});

watch(() => props.deviceId, () => {
  placedRibbons.value = [];
  selectedRibbonId.value = null;
  loadRibbons();
});

// 화면 방향이 바뀌면 모니터 크기 재계산
watch(isPortrait, () => {
  if (monitorAreaRef.value) {
    const rect = monitorAreaRef.value.getBoundingClientRect();
    calcMonitorSize(rect.width, rect.height);
  }
});

// ────────────────────────────────────────────────────────────────────
// 저장
// ────────────────────────────────────────────────────────────────────

async function handleSave() {
  saving.value = true;
  try {
    const dto: BuildingApi.DeviceRibbonBulkSave = {
      deviceId: props.deviceId,
      ribbons: placedRibbons.value.map((r, idx) => ({
        deviceId: props.deviceId,
        mediaSourceId: r.mediaSourceId,
        positionLeft: round3(r.positionLeft),
        positionTop: round3(r.positionTop),
        width: round3(r.width),
        height: round3(r.height),
        sortOrder: idx,
        remark: r.remark,
      })),
    };
    const res = await bulkSaveDeviceRibbons(dto);
    placedRibbons.value = Array.isArray(res) ? res : (res as any)?.result ?? [];
    selectedRibbonId.value = null;
    message.success('리본 설정이 저장되었습니다.');
  } catch (err) {
    message.error('리본 설정 저장 실패');
  } finally {
    saving.value = false;
  }
}

function handleReset() {
  selectedRibbonId.value = null;
  loadRibbons();
}

// ────────────────────────────────────────────────────────────────────
// 사이드 패널 → 모니터 드래그 드롭
// ────────────────────────────────────────────────────────────────────

function onDecorationDragStart(dec: BuildingApi.MediaSource, evt: DragEvent) {
  draggingDecoration.value = dec;
  if (evt.dataTransfer) {
    evt.dataTransfer.effectAllowed = 'copy';
    evt.dataTransfer.setData('text/plain', dec.id);
  }
}

function onMonitorDragOver(evt: DragEvent) {
  evt.preventDefault();
  if (evt.dataTransfer) {
    evt.dataTransfer.dropEffect = 'copy';
  }
}

function onMonitorDrop(evt: DragEvent) {
  evt.preventDefault();
  if (!draggingDecoration.value || !monitorRef.value) return;

  const rect = monitorRef.value.getBoundingClientRect();
  const x = evt.clientX - rect.left;
  const y = evt.clientY - rect.top;

  const posLeft = round3((x / rect.width) * 100 - 5);
  const posTop = round3((y / rect.height) * 100 - 5);

  const dec = draggingDecoration.value;
  const newRibbon: BuildingApi.DeviceRibbon = {
    id: `temp-${Date.now()}`,
    deviceId: props.deviceId,
    mediaSourceId: dec.id,
    mediaSourceName: dec.name,
    mediaSourceUrl: dec.url,
    mediaSourceThumbnailUrl: dec.thumbnailUrl,
    positionLeft: Math.max(0, Math.min(posLeft, 90)),
    positionTop: Math.max(0, Math.min(posTop, 90)),
    width: 10,
    height: 10,
    sortOrder: placedRibbons.value.length,
  };

  placedRibbons.value = [...placedRibbons.value, newRibbon];
  selectedRibbonId.value = newRibbon.id;
  draggingDecoration.value = null;
}

// ────────────────────────────────────────────────────────────────────
// 리본 이동 (마우스 드래그)
// ────────────────────────────────────────────────────────────────────

function onRibbonMouseDown(ribbonId: string, evt: MouseEvent) {
  evt.stopPropagation();
  evt.preventDefault();
  selectedRibbonId.value = ribbonId;

  const ribbon = placedRibbons.value.find((r) => r.id === ribbonId);
  if (!ribbon || !monitorRef.value) return;

  isDragging.value = true;
  dragTarget.value = ribbonId;
  dragStartX.value = evt.clientX;
  dragStartY.value = evt.clientY;
  dragStartLeft.value = ribbon.positionLeft;
  dragStartTop.value = ribbon.positionTop;

  window.addEventListener('mousemove', onMouseMove);
  window.addEventListener('mouseup', onMouseUp);
}

function onResizeMouseDown(ribbonId: string, evt: MouseEvent) {
  evt.stopPropagation();
  evt.preventDefault();
  selectedRibbonId.value = ribbonId;

  const ribbon = placedRibbons.value.find((r) => r.id === ribbonId);
  if (!ribbon || !monitorRef.value) return;

  isResizing.value = true;
  dragTarget.value = ribbonId;
  dragStartX.value = evt.clientX;
  dragStartY.value = evt.clientY;
  resizeStartWidth.value = ribbon.width;
  resizeStartHeight.value = ribbon.height;

  window.addEventListener('mousemove', onMouseMove);
  window.addEventListener('mouseup', onMouseUp);
}

function onMouseMove(evt: MouseEvent) {
  if (!monitorRef.value || !dragTarget.value) return;

  const rect = monitorRef.value.getBoundingClientRect();
  const dxPct = ((evt.clientX - dragStartX.value) / rect.width) * 100;
  const dyPct = ((evt.clientY - dragStartY.value) / rect.height) * 100;

  placedRibbons.value = placedRibbons.value.map((r) => {
    if (r.id !== dragTarget.value) return r;

    if (isDragging.value) {
      const newLeft = round3(Math.max(0, Math.min(dragStartLeft.value + dxPct, 100 - r.width)));
      const newTop = round3(Math.max(0, Math.min(dragStartTop.value + dyPct, 100 - r.height)));
      return { ...r, positionLeft: newLeft, positionTop: newTop };
    }

    if (isResizing.value) {
      const newW = round3(Math.max(1, Math.min(resizeStartWidth.value + dxPct, 100 - r.positionLeft)));
      const newH = round3(Math.max(1, Math.min(resizeStartHeight.value + dyPct, 100 - r.positionTop)));
      return { ...r, width: newW, height: newH };
    }

    return r;
  });
}

function onMouseUp() {
  isDragging.value = false;
  isResizing.value = false;
  dragTarget.value = null;
  window.removeEventListener('mousemove', onMouseMove);
  window.removeEventListener('mouseup', onMouseUp);
}

// ────────────────────────────────────────────────────────────────────
// 리본 삭제
// ────────────────────────────────────────────────────────────────────

function removeRibbon(ribbonId: string) {
  placedRibbons.value = placedRibbons.value.filter((r) => r.id !== ribbonId);
  if (selectedRibbonId.value === ribbonId) {
    selectedRibbonId.value = null;
  }
}

// ────────────────────────────────────────────────────────────────────
// 수치 직접 입력 (선택된 리본)
// ────────────────────────────────────────────────────────────────────

function updateSelectedRibbon(field: keyof BuildingApi.DeviceRibbon, value: number) {
  if (!selectedRibbonId.value) return;
  placedRibbons.value = placedRibbons.value.map((r) =>
    r.id === selectedRibbonId.value ? { ...r, [field]: round3(value) } : r,
  );
}

// ────────────────────────────────────────────────────────────────────
// 유틸
// ────────────────────────────────────────────────────────────────────

function round3(val: number): number {
  return Math.round(val * 1000) / 1000;
}
</script>

<template>
  <div class="ribbon-tab flex h-full flex-col">
    <!-- 로딩 -->
    <div v-if="loading" class="flex flex-1 items-center justify-center py-16">
      <Spin tip="리본 설정 불러오는 중..." />
    </div>

    <template v-else>

      <!-- 메인 영역: 사이드패널 + 모니터 미리보기 + 속성 패널 -->
      <div class="ribbon-main flex min-h-0 flex-1 gap-3 overflow-hidden px-3 pb-3 pt-3">

        <!-- ① 장식 목록 사이드패널 -->
        <div class="decoration-panel flex w-36 shrink-0 flex-col overflow-hidden rounded-lg border border-border bg-muted/30">
          <div class="flex shrink-0 items-center gap-1.5 border-b border-border px-2 py-1.5">
            <IconifyIcon icon="lucide:image" class="size-3.5 text-primary" />
            <span class="text-xs font-semibold">장식 목록</span>
          </div>
          <div class="flex-1 overflow-y-auto">
            <div v-if="decorationsLoading" class="flex items-center justify-center py-8">
              <Spin size="small" />
            </div>
            <div v-else-if="decorations.length === 0" class="p-3 text-center text-xs text-muted-foreground">
              등록된 장식이 없습니다.
            </div>
            <div
              v-for="dec in decorations"
              :key="dec.id"
              class="decoration-item group flex cursor-grab flex-col items-center gap-1 border-b border-border/50 p-2 transition-colors hover:bg-primary/10 active:cursor-grabbing"
              draggable="true"
              :title="dec.name"
              @dragstart="onDecorationDragStart(dec, $event)"
            >
              <!-- 체크무늬 배경으로 투명 PNG 가시성 보장 -->
              <div class="checkerboard relative flex size-16 items-center justify-center overflow-hidden rounded border border-border/50">
                <img
                  :src="dec.thumbnailUrl || dec.url"
                  :alt="dec.name"
                  class="max-h-full max-w-full object-contain"
                  draggable="false"
                />
              </div>
              <span class="w-full truncate text-center text-[10px] text-muted-foreground">
                {{ dec.shortName || dec.name }}
              </span>
            </div>
          </div>
        </div>

        <!-- ② 모니터 미리보기 영역 -->
        <div ref="monitorAreaRef" class="monitor-area flex flex-1 flex-col items-center justify-center overflow-hidden">
          <div class="mb-1.5 flex shrink-0 items-center gap-2">
            <IconifyIcon icon="lucide:monitor" class="size-4 text-muted-foreground" />
            <span class="text-xs text-muted-foreground">
              {{ displayOrientation === 'PORTRAIT' ? '세로 모니터' : '가로 모니터' }}
            </span>
            <span class="text-[10px] text-muted-foreground">(드래그하여 장식 배치)</span>
          </div>

          <!-- 모니터 화면 (동적 크기: 가용 공간 최대 사용) -->
          <div
            ref="monitorRef"
            class="monitor-screen relative overflow-hidden rounded bg-gray-950 shadow-2xl ring-4 ring-gray-700 select-none"
            :style="monitorScreenStyle"
            @dragover="onMonitorDragOver"
            @drop="onMonitorDrop"
            @click.self="selectedRibbonId = null"
          >
            <!-- 화면 배경 패턴 -->
            <div class="absolute inset-0 flex items-center justify-center">
              <IconifyIcon icon="lucide:monitor-play" class="size-12 text-gray-700 opacity-30" />
            </div>

            <!-- 배치된 리본들 -->
            <div
              v-for="ribbon in placedRibbons"
              :key="ribbon.id"
              class="ribbon-item absolute"
              :class="{
                'ring-2 ring-primary ring-offset-1': selectedRibbonId === ribbon.id,
                'ring-1 ring-white/30': selectedRibbonId !== ribbon.id,
              }"
              :style="{
                left: `${ribbon.positionLeft}%`,
                top: `${ribbon.positionTop}%`,
                width: `${ribbon.width}%`,
                height: `${ribbon.height}%`,
                cursor: isDragging && dragTarget === ribbon.id ? 'grabbing' : 'grab',
              }"
              @mousedown="onRibbonMouseDown(ribbon.id, $event)"
              @click.stop="selectedRibbonId = ribbon.id"
            >
              <!-- 장식 이미지 -->
              <img
                :src="ribbon.mediaSourceThumbnailUrl || ribbon.mediaSourceUrl"
                :alt="ribbon.mediaSourceName"
                class="h-full w-full object-contain"
                draggable="false"
              />

              <!-- 삭제 버튼 -->
              <button
                v-if="selectedRibbonId === ribbon.id"
                class="ribbon-delete-btn absolute -right-2 -top-2 flex size-4 items-center justify-center rounded-full bg-destructive text-destructive-foreground shadow"
                title="리본 삭제"
                @mousedown.stop
                @click.stop="removeRibbon(ribbon.id)"
              >
                <IconifyIcon icon="lucide:x" class="size-3" />
              </button>

              <!-- 리사이즈 핸들 (우하단) -->
              <div
                v-if="selectedRibbonId === ribbon.id"
                class="ribbon-resize-handle absolute -bottom-1 -right-1 size-3 cursor-se-resize rounded-sm bg-primary shadow"
                @mousedown.stop="onResizeMouseDown(ribbon.id, $event)"
              />
            </div>

            <!-- 드래그 오버 표시 -->
            <div
              v-if="draggingDecoration"
              class="pointer-events-none absolute inset-0 border-2 border-dashed border-primary/60 bg-primary/10"
            >
              <div class="flex h-full items-center justify-center">
                <span class="rounded bg-primary/80 px-2 py-1 text-xs text-primary-foreground">
                  여기에 놓기
                </span>
              </div>
            </div>
          </div>

          <div class="mt-2 shrink-0 text-xs text-muted-foreground">
            배치된 장식: {{ placedRibbons.length }}개
            <span v-if="monitorWidth" class="ml-2 opacity-60">({{ monitorWidth }}×{{ monitorHeight }}px)</span>
          </div>
        </div>

        <!-- ③ 속성 편집 패널 -->
        <div class="property-panel flex w-52 shrink-0 flex-col overflow-hidden rounded-lg border border-border bg-muted/30">
          <div class="flex shrink-0 items-center gap-1.5 border-b border-border px-2 py-1.5">
            <IconifyIcon icon="lucide:sliders-horizontal" class="size-3.5 text-primary" />
            <span class="text-xs font-semibold">속성 조정</span>
          </div>

          <div v-if="!selectedRibbon" class="flex flex-1 flex-col items-center justify-center gap-2 p-4 text-center">
            <IconifyIcon icon="lucide:mouse-pointer-click" class="size-8 text-muted-foreground/50" />
            <span class="text-xs text-muted-foreground">모니터에 배치된 장식을 클릭하면 위치와 크기를 조정할 수 있습니다.</span>
          </div>

          <div v-else class="flex-1 overflow-y-auto p-3">
            <!-- 선택된 장식 이름 -->
            <div class="mb-3 flex items-center gap-2">
              <div class="checkerboard size-8 shrink-0 overflow-hidden rounded border border-border">
                <img
                  :src="selectedRibbon.mediaSourceThumbnailUrl || selectedRibbon.mediaSourceUrl"
                  :alt="selectedRibbon.mediaSourceName"
                  class="h-full w-full object-contain"
                />
              </div>
              <span class="truncate text-xs font-medium">{{ selectedRibbon.mediaSourceName }}</span>
            </div>

            <div class="space-y-3">
              <!-- 위치 Left -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">좌측 위치 (%)</label>
                <InputNumber
                  :value="selectedRibbon.positionLeft"
                  :min="0"
                  :max="99"
                  :precision="3"
                  :step="0.1"
                  size="small"
                  addon-after="%"
                  style="width: 100%"
                  @change="(v) => updateSelectedRibbon('positionLeft', v as number)"
                />
              </div>

              <!-- 위치 Top -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">상단 위치 (%)</label>
                <InputNumber
                  :value="selectedRibbon.positionTop"
                  :min="0"
                  :max="99"
                  :precision="3"
                  :step="0.1"
                  size="small"
                  addon-after="%"
                  style="width: 100%"
                  @change="(v) => updateSelectedRibbon('positionTop', v as number)"
                />
              </div>

              <!-- 너비 -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">너비 (%)</label>
                <InputNumber
                  :value="selectedRibbon.width"
                  :min="1"
                  :max="100"
                  :precision="3"
                  :step="0.5"
                  size="small"
                  addon-after="%"
                  style="width: 100%"
                  @change="(v) => updateSelectedRibbon('width', v as number)"
                />
              </div>

              <!-- 높이 -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">높이 (%)</label>
                <InputNumber
                  :value="selectedRibbon.height"
                  :min="1"
                  :max="100"
                  :precision="3"
                  :step="0.5"
                  size="small"
                  addon-after="%"
                  style="width: 100%"
                  @change="(v) => updateSelectedRibbon('height', v as number)"
                />
              </div>

              <!-- 현재 수치 요약 -->
              <div class="mt-1 rounded bg-muted p-2 text-[10px] text-muted-foreground leading-5">
                <div>Left: {{ selectedRibbon.positionLeft.toFixed(3) }}%</div>
                <div>Top: {{ selectedRibbon.positionTop.toFixed(3) }}%</div>
                <div>W: {{ selectedRibbon.width.toFixed(3) }}% / H: {{ selectedRibbon.height.toFixed(3) }}%</div>
              </div>

              <!-- 삭제 버튼 -->
              <Popconfirm
                title="이 장식을 제거하시겠습니까?"
                @confirm="removeRibbon(selectedRibbon!.id)"
              >
                <Button danger block size="small">
                  <IconifyIcon icon="lucide:trash-2" class="size-3.5 mr-1" />
                  장식 제거
                </Button>
              </Popconfirm>
            </div>
          </div>
        </div>

      </div>

      <!-- 하단 저장 버튼 -->
      <div class="flex shrink-0 justify-end gap-2 border-t border-border bg-muted/40 px-4 py-2">
        <Button @click="handleReset">초기화</Button>
        <Button type="primary" :loading="saving" @click="handleSave">
          <IconifyIcon icon="lucide:save" class="size-4 mr-1" />
          리본 저장
        </Button>
      </div>
    </template>
  </div>
</template>

<style scoped>
/* ─── 체크무늬 배경 (투명 PNG 가시성 확보) ─── */
.checkerboard {
  background-image: url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8' viewBox='0 0 8 8'><rect width='4' height='4' fill='%23ccc'/><rect x='4' y='4' width='4' height='4' fill='%23ccc'/></svg>");
  background-size: 8px 8px;
  background-color: #fff;
}

/* ─── 모니터 화면: 크기는 JS가 인라인 스타일로 주입 ─── */
.monitor-screen {
  position: relative;
  /* 크기는 monitorScreenStyle computed 로 동적 주입 */
}

/* ─── 리본 아이템 ─── */
.ribbon-item {
  transition: box-shadow 0.1s;
  user-select: none;
}

.ribbon-item:hover {
  z-index: 10;
}

.ribbon-item.ring-2 {
  z-index: 20;
}

/* ─── 삭제 버튼 ─── */
.ribbon-delete-btn {
  z-index: 30;
  font-size: 10px;
  line-height: 1;
  border: none;
  outline: none;
  padding: 0;
}

/* ─── 리사이즈 핸들 ─── */
.ribbon-resize-handle {
  z-index: 30;
}

/* ─── 레이아웃 조정 ─── */
.ribbon-main {
  min-height: 0;
}
</style>

