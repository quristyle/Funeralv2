<script lang="ts" setup>
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue';
import {
  Button, Spin, Tooltip, Popconfirm, InputNumber, Input, Select, message,
} from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import {
  getDeviceRibbons,
  getMediaSources,
  bulkSaveDeviceRibbons,
  getDeviceTextOverlays,
  bulkSaveDeviceTextOverlays,
} from '#/api/building';
import type { BuildingApi } from '#/api/building';

// ────────────────────────────────────────────────────────────────────
// Props
// ────────────────────────────────────────────────────────────────────
const props = defineProps<{
  deviceId: string;
  displayOrientation?: 'LANDSCAPE' | 'PORTRAIT';
}>();

// ────────────────────────────────────────────────────────────────────
// 선택 아이템 유형 정의
// ────────────────────────────────────────────────────────────────────
type SelectedType = 'ribbon' | 'overlay' | null;

// ────────────────────────────────────────────────────────────────────
// 상태
// ────────────────────────────────────────────────────────────────────
const loading = ref(false);
const saving = ref(false);

/** 배치된 리본 목록 */
const placedRibbons = ref<BuildingApi.DeviceRibbon[]>([]);
/** 배치된 텍스트 오버레이 목록 */
const placedOverlays = ref<BuildingApi.DeviceTextOverlay[]>([]);

/** 장식 이미지 목록 (사이드 패널) */
const decorations = ref<BuildingApi.MediaSource[]>([]);
const decorationsLoading = ref(false);

/** 선택된 아이템 */
const selectedItemId = ref<string | null>(null);
const selectedItemType = ref<SelectedType>(null);

/** 새 텍스트 입력값 */
const newTextContent = ref('');

/** 사이드패널에서 드래그 중인 장식 */
const draggingDecoration = ref<BuildingApi.MediaSource | null>(null);

/** 드래그/리사이즈 공통 상태 */
const isDragging = ref(false);
const isResizing = ref(false);
const dragTargetId = ref<string | null>(null);
const dragTargetType = ref<SelectedType>(null);
const dragStartX = ref(0);
const dragStartY = ref(0);
const dragStartLeft = ref(0);
const dragStartTop = ref(0);
const resizeStartWidth = ref(0);
const resizeStartHeight = ref(0);

/** 왼쪽 사이드 패널 활성 섹션 ('image' | 'text') */
const sideSection = ref<'image' | 'text'>('image');

/** 모니터 DOM refs */
const monitorRef = ref<HTMLElement | null>(null);
const monitorAreaRef = ref<HTMLElement | null>(null);
const monitorWidth = ref(0);
const monitorHeight = ref(0);

/** 초기 데이터 로드 완료 여부 (로드 직후 watch 트리거 방지) */
const isDataReady = ref(false);

/** 자동 저장 디바운스 타이머 */
const autoSaveTimer = ref<NodeJS.Timeout | null>(null);

const LANDSCAPE_RATIO = 16 / 9;
const PORTRAIT_RATIO = 9 / 16;
const isPortrait = computed(() => props.displayOrientation === 'PORTRAIT');

// ────────────────────────────────────────────────────────────────────
// Computed
// ────────────────────────────────────────────────────────────────────
const monitorScreenStyle = computed(() => {
  const w = monitorWidth.value > 0 ? monitorWidth.value : 480;
  const h = monitorHeight.value > 0 ? monitorHeight.value : 270;
  return { width: `${w}px`, height: `${h}px` };
});

const selectedRibbon = computed(() =>
  selectedItemType.value === 'ribbon'
    ? (placedRibbons.value.find((r) => r.id === selectedItemId.value) ?? null)
    : null,
);

const selectedOverlay = computed(() =>
  selectedItemType.value === 'overlay'
    ? (placedOverlays.value.find((o) => o.id === selectedItemId.value) ?? null)
    : null,
);

// ────────────────────────────────────────────────────────────────────
// 모니터 크기 계산
// ────────────────────────────────────────────────────────────────────
function calcMonitorSize(containerW: number, containerH: number) {
  const HEADER_H = 60;
  const availW = Math.max(containerW - 16, 100);
  const availH = Math.max(containerH - HEADER_H, 80);
  const ratio = isPortrait.value ? PORTRAIT_RATIO : LANDSCAPE_RATIO;
  let w = availW;
  let h = w / ratio;
  if (h > availH) { h = availH; w = h * ratio; }
  monitorWidth.value = Math.floor(w);
  monitorHeight.value = Math.floor(h);
}

let resizeObserver: ResizeObserver | null = null;

function attachResizeObserver() {
  if (!monitorAreaRef.value || resizeObserver) return;
  resizeObserver = new ResizeObserver((entries) => {
    const entry = entries[0];
    if (!entry) return;
    const { width, height } = entry.contentRect;
    if (width > 0) calcMonitorSize(width, height);
  });
  resizeObserver.observe(monitorAreaRef.value);
  const rect = monitorAreaRef.value.getBoundingClientRect();
  if (rect.width > 0) calcMonitorSize(rect.width, rect.height);
}

// ────────────────────────────────────────────────────────────────────
// 데이터 로드
// ────────────────────────────────────────────────────────────────────
async function loadAll() {
  isDataReady.value = false;
  loading.value = true;
  try {
    const [ribbonRes, overlayRes] = await Promise.all([
      getDeviceRibbons(props.deviceId),
      getDeviceTextOverlays(props.deviceId),
    ]);
    placedRibbons.value = Array.isArray(ribbonRes) ? ribbonRes : (ribbonRes as any)?.result ?? [];
    placedOverlays.value = Array.isArray(overlayRes) ? overlayRes : (overlayRes as any)?.result ?? [];
  } catch {
    message.error('데이터 로드 실패');
  } finally {
    loading.value = false;
    // 데이터 로드 완료 후 watch 활성화 (nextTick으로 watch 첫 실행 방지)
    await nextTick();
    isDataReady.value = true;
  }
}

async function loadDecorations() {
  decorationsLoading.value = true;
  try {
    const res = await getMediaSources('IMAGE');
    decorations.value = Array.isArray(res) ? res : (res as any)?.result ?? [];
  } catch {
    message.error('장식 목록 로드 실패');
  } finally {
    decorationsLoading.value = false;
  }
}

onMounted(() => { loadAll(); loadDecorations(); });
onUnmounted(() => {
  resizeObserver?.disconnect();
  resizeObserver = null;
  if (autoSaveTimer.value) clearTimeout(autoSaveTimer.value);
});

watch(monitorAreaRef, async (el) => {
  if (el) { await nextTick(); attachResizeObserver(); }
  else { resizeObserver?.disconnect(); resizeObserver = null; }
});

watch(() => props.deviceId, () => {
  placedRibbons.value = [];
  placedOverlays.value = [];
  selectedItemId.value = null;
  selectedItemType.value = null;
  loadAll();
});

watch(isPortrait, () => {
  if (monitorAreaRef.value) {
    const rect = monitorAreaRef.value.getBoundingClientRect();
    calcMonitorSize(rect.width, rect.height);
  }
});

// ────────────────────────────────────────────────────────────────────
// 자동 저장: 리본 또는 오버레이 목록이 변경되면 디바운스 후 저장
// ────────────────────────────────────────────────────────────────────
watch(
  [() => JSON.stringify(placedRibbons.value), () => JSON.stringify(placedOverlays.value)],
  () => {
    // 초기 로드 중이거나 로드 직후는 저장하지 않음
    if (!isDataReady.value) return;
    // 드래그 중에는 저장하지 않음 (mouseUp에서 처리)
    if (isDragging.value || isResizing.value) return;

    if (autoSaveTimer.value) clearTimeout(autoSaveTimer.value);
    autoSaveTimer.value = setTimeout(() => {
      handleSave(true);
    }, 1500);
  },
);

// ────────────────────────────────────────────────────────────────────
// 저장 (리본 + 오버레이 동시)
// ────────────────────────────────────────────────────────────────────
async function handleSave(silent = false) {
  saving.value = true;
  try {
    const [ribbonRes, overlayRes] = await Promise.all([
      bulkSaveDeviceRibbons({
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
      }),
      bulkSaveDeviceTextOverlays({
        deviceId: props.deviceId,
        overlays: placedOverlays.value.map((o, idx) => ({
          deviceId: props.deviceId,
          textContent: o.textContent,
          fontSize: round3(o.fontSize),
          fontColor: o.fontColor,
          backgroundColor: o.backgroundColor,
          textAlign: o.textAlign,
          fontWeight: o.fontWeight,
          positionLeft: round3(o.positionLeft),
          positionTop: round3(o.positionTop),
          width: round3(o.width),
          height: round3(o.height),
          sortOrder: idx,
          remark: o.remark,
        })),
      }),
    ]);

    // 저장 완료 후 서버 응답으로 상태 갱신 (temp id → 실제 id 교체)
    isDataReady.value = false;
    placedRibbons.value = Array.isArray(ribbonRes) ? ribbonRes : (ribbonRes as any)?.result ?? [];
    placedOverlays.value = Array.isArray(overlayRes) ? overlayRes : (overlayRes as any)?.result ?? [];
    await nextTick();
    isDataReady.value = true;

    if (!silent) {
      selectedItemId.value = null;
      selectedItemType.value = null;
      message.success('저장되었습니다.');
    }
  } catch {
    message.error('저장 실패');
  } finally {
    saving.value = false;
  }
}

function handleReset() {
  selectedItemId.value = null;
  selectedItemType.value = null;
  loadAll();
}

// ────────────────────────────────────────────────────────────────────
// 선택 헬퍼
// ────────────────────────────────────────────────────────────────────
function selectItem(id: string, type: 'ribbon' | 'overlay') {
  selectedItemId.value = id;
  selectedItemType.value = type;
}

function clearSelection() {
  selectedItemId.value = null;
  selectedItemType.value = null;
}

// ────────────────────────────────────────────────────────────────────
// 장식 이미지 드래그 → 모니터 드롭
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
  if (evt.dataTransfer) evt.dataTransfer.dropEffect = 'copy';
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
  selectItem(newRibbon.id, 'ribbon');
  draggingDecoration.value = null;
}

// ────────────────────────────────────────────────────────────────────
// 텍스트 오버레이 추가
// ────────────────────────────────────────────────────────────────────
function addTextOverlay() {
  const text = newTextContent.value.trim();
  if (!text) { message.warning('추가할 텍스트를 입력해 주세요.'); return; }
  const newOverlay: BuildingApi.DeviceTextOverlay = {
    id: `temp-${Date.now()}`,
    deviceId: props.deviceId,
    textContent: text,
    fontSize: 3,
    fontColor: '#FFFFFF',
    backgroundColor: 'transparent',
    textAlign: 'center',
    fontWeight: 'normal',
    positionLeft: 35,
    positionTop: 45,
    width: 30,
    height: 10,
    sortOrder: placedOverlays.value.length,
  };
  placedOverlays.value = [...placedOverlays.value, newOverlay];
  selectItem(newOverlay.id, 'overlay');
  newTextContent.value = '';
  // 텍스트 추가 시 텍스트 섹션으로 포커스
  sideSection.value = 'text';
}

// ────────────────────────────────────────────────────────────────────
// 마우스 드래그 이동/리사이즈
// ────────────────────────────────────────────────────────────────────
function onItemMouseDown(id: string, type: 'ribbon' | 'overlay', evt: MouseEvent) {
  evt.stopPropagation();
  evt.preventDefault();
  selectItem(id, type);

  const item = type === 'ribbon'
    ? placedRibbons.value.find((r) => r.id === id)
    : placedOverlays.value.find((o) => o.id === id);
  if (!item || !monitorRef.value) return;

  isDragging.value = true;
  dragTargetId.value = id;
  dragTargetType.value = type;
  dragStartX.value = evt.clientX;
  dragStartY.value = evt.clientY;
  dragStartLeft.value = item.positionLeft;
  dragStartTop.value = item.positionTop;

  window.addEventListener('mousemove', onMouseMove);
  window.addEventListener('mouseup', onMouseUp);
}

function onResizeMouseDown(id: string, type: 'ribbon' | 'overlay', evt: MouseEvent) {
  evt.stopPropagation();
  evt.preventDefault();
  selectItem(id, type);

  const item = type === 'ribbon'
    ? placedRibbons.value.find((r) => r.id === id)
    : placedOverlays.value.find((o) => o.id === id);
  if (!item || !monitorRef.value) return;

  isResizing.value = true;
  dragTargetId.value = id;
  dragTargetType.value = type;
  dragStartX.value = evt.clientX;
  dragStartY.value = evt.clientY;
  resizeStartWidth.value = item.width;
  resizeStartHeight.value = item.height;

  window.addEventListener('mousemove', onMouseMove);
  window.addEventListener('mouseup', onMouseUp);
}

function onMouseMove(evt: MouseEvent) {
  if (!monitorRef.value || !dragTargetId.value || !dragTargetType.value) return;
  const rect = monitorRef.value.getBoundingClientRect();
  const dxPct = ((evt.clientX - dragStartX.value) / rect.width) * 100;
  const dyPct = ((evt.clientY - dragStartY.value) / rect.height) * 100;

  if (dragTargetType.value === 'ribbon') {
    placedRibbons.value = placedRibbons.value.map((r) => {
      if (r.id !== dragTargetId.value) return r;
      if (isDragging.value) {
        return {
          ...r,
          positionLeft: round3(Math.max(0, Math.min(dragStartLeft.value + dxPct, 100 - r.width))),
          positionTop: round3(Math.max(0, Math.min(dragStartTop.value + dyPct, 100 - r.height))),
        };
      }
      if (isResizing.value) {
        return {
          ...r,
          width: round3(Math.max(1, Math.min(resizeStartWidth.value + dxPct, 100 - r.positionLeft))),
          height: round3(Math.max(1, Math.min(resizeStartHeight.value + dyPct, 100 - r.positionTop))),
        };
      }
      return r;
    });
  } else {
    placedOverlays.value = placedOverlays.value.map((o) => {
      if (o.id !== dragTargetId.value) return o;
      if (isDragging.value) {
        return {
          ...o,
          positionLeft: round3(Math.max(0, Math.min(dragStartLeft.value + dxPct, 100 - o.width))),
          positionTop: round3(Math.max(0, Math.min(dragStartTop.value + dyPct, 100 - o.height))),
        };
      }
      if (isResizing.value) {
        return {
          ...o,
          width: round3(Math.max(5, Math.min(resizeStartWidth.value + dxPct, 100 - o.positionLeft))),
          height: round3(Math.max(3, Math.min(resizeStartHeight.value + dyPct, 100 - o.positionTop))),
        };
      }
      return o;
    });
  }
}

function onMouseUp() {
  const wasDraggingOrResizing = isDragging.value || isResizing.value;
  isDragging.value = false;
  isResizing.value = false;
  dragTargetId.value = null;
  dragTargetType.value = null;
  window.removeEventListener('mousemove', onMouseMove);
  window.removeEventListener('mouseup', onMouseUp);

  // 드래그/리사이즈 완료 후 위치 변경이 있었다면 즉시 저장 트리거
  if (wasDraggingOrResizing && isDataReady.value) {
    if (autoSaveTimer.value) clearTimeout(autoSaveTimer.value);
    autoSaveTimer.value = setTimeout(() => {
      handleSave(true);
    }, 800);
  }
}

// ────────────────────────────────────────────────────────────────────
// 삭제
// ────────────────────────────────────────────────────────────────────
function removeRibbon(id: string) {
  placedRibbons.value = placedRibbons.value.filter((r) => r.id !== id);
  if (selectedItemId.value === id) clearSelection();
}

function removeOverlay(id: string) {
  placedOverlays.value = placedOverlays.value.filter((o) => o.id !== id);
  if (selectedItemId.value === id) clearSelection();
}

// ────────────────────────────────────────────────────────────────────
// 속성 직접 입력
// ────────────────────────────────────────────────────────────────────
function updateRibbon(field: keyof BuildingApi.DeviceRibbon, value: number) {
  if (!selectedItemId.value) return;
  placedRibbons.value = placedRibbons.value.map((r) =>
    r.id === selectedItemId.value ? { ...r, [field]: round3(value) } : r,
  );
}

function updateOverlay<K extends keyof BuildingApi.DeviceTextOverlay>(
  field: K, value: BuildingApi.DeviceTextOverlay[K],
) {
  if (!selectedItemId.value) return;
  placedOverlays.value = placedOverlays.value.map((o) =>
    o.id === selectedItemId.value
      ? { ...o, [field]: typeof value === 'number' ? round3(value as number) : value }
      : o,
  );
}

// ────────────────────────────────────────────────────────────────────
// 텍스트 오버레이 인라인 스타일
// ────────────────────────────────────────────────────────────────────
function overlayStyle(o: BuildingApi.DeviceTextOverlay): Record<string, string> {
  const fontPx = monitorHeight.value > 0
    ? `${Math.round((o.fontSize / 100) * monitorHeight.value)}px`
    : `${o.fontSize * 3}px`;
  return {
    left: `${o.positionLeft}%`,
    top: `${o.positionTop}%`,
    width: `${o.width}%`,
    height: `${o.height}%`,
    fontSize: fontPx,
    color: o.fontColor,
    backgroundColor: o.backgroundColor === 'transparent' ? 'transparent' : o.backgroundColor,
    textAlign: o.textAlign,
    fontWeight: o.fontWeight,
    cursor: isDragging.value && dragTargetId.value === o.id ? 'grabbing' : 'grab',
  };
}

// ────────────────────────────────────────────────────────────────────
// 유틸
// ────────────────────────────────────────────────────────────────────
function round3(val: number): number {
  return Math.round(val * 1000) / 1000;
}

const TEXT_ALIGN_OPTIONS = [
  { label: '왼쪽', value: 'left' },
  { label: '가운데', value: 'center' },
  { label: '오른쪽', value: 'right' },
];

const FONT_WEIGHT_OPTIONS = [
  { label: '보통', value: 'normal' },
  { label: '굵게', value: 'bold' },
];
</script>

<template>

  <div class="ribbon-tab flex h-full flex-col">
    <!-- 로딩 -->
    <div v-if="loading" class="flex flex-1 items-center justify-center py-16">
      <Spin tip="설정 불러오는 중..." />
    </div>

    <template v-else>
      <div class="ribbon-main flex min-h-0 flex-1 gap-3 overflow-hidden px-3 pb-3 pt-3">

        <!-- ① 왼쪽 사이드 패널 -->
        <div class="side-panel flex w-44 shrink-0 flex-col overflow-hidden rounded-lg border border-border bg-muted/30">

          <!-- 탭 전환: 장식 이미지 | 텍스트 -->
          <div class="flex shrink-0 border-b border-border">
            <button
              class="side-tab flex flex-1 items-center justify-center gap-1 py-1.5 text-[11px] font-semibold transition-colors"
              :class="sideSection === 'image' ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted'"
              @click="sideSection = 'image'"
            >
              <IconifyIcon icon="lucide:image" class="size-3.5" />
              장식 이미지
            </button>
            <button
              class="side-tab flex flex-1 items-center justify-center gap-1 py-1.5 text-[11px] font-semibold transition-colors border-l border-border"
              :class="sideSection === 'text' ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted'"
              @click="sideSection = 'text'"
            >
              <IconifyIcon icon="lucide:type" class="size-3.5" />
              텍스트
            </button>
          </div>

          <!-- 장식 이미지 섹션 -->
          <div v-show="sideSection === 'image'" class="flex-1 overflow-y-auto">
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
              <div class="checkerboard relative flex size-14 items-center justify-center overflow-hidden rounded border border-border/50">
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

            <!-- 배치된 리본 목록 -->
            <div v-if="placedRibbons.length > 0" class="border-t border-border px-2 pt-2">
              <div class="mb-1 text-[10px] font-semibold text-muted-foreground">배치된 장식 ({{ placedRibbons.length }})</div>
              <div
                v-for="r in placedRibbons"
                :key="r.id"
                class="group mb-1 flex cursor-pointer items-center gap-1.5 rounded px-1 py-1 text-[10px] transition-colors hover:bg-primary/10"
                :class="{ 'bg-primary/15 font-semibold': selectedItemId === r.id && selectedItemType === 'ribbon' }"
                @click="selectItem(r.id, 'ribbon')"
              >
                <div class="checkerboard size-5 shrink-0 overflow-hidden rounded border border-border/50">
                  <img :src="r.mediaSourceThumbnailUrl || r.mediaSourceUrl" class="h-full w-full object-contain" draggable="false" />
                </div>
                <span class="flex-1 truncate">{{ r.mediaSourceName }}</span>
                <button class="hidden size-3.5 items-center justify-center text-destructive group-hover:flex" @click.stop="removeRibbon(r.id)">
                  <IconifyIcon icon="lucide:x" class="size-2.5" />
                </button>
              </div>
            </div>
          </div>

          <!-- 텍스트 섹션 -->
          <div v-show="sideSection === 'text'" class="flex flex-1 flex-col gap-2 overflow-y-auto p-3">
            <div>
              <label class="mb-1 block text-[11px] text-muted-foreground">텍스트 내용</label>
              <Input.TextArea
                v-model:value="newTextContent"
                :rows="3"
                placeholder="표시할 텍스트를 입력하세요."
                size="small"
              />
            </div>
            <Button type="primary" size="small" block @click="addTextOverlay">
              <IconifyIcon icon="lucide:plus" class="mr-1 size-3.5" />
              화면에 추가
            </Button>

            <!-- 배치된 텍스트 목록 -->
            <div v-if="placedOverlays.length > 0" class="border-t border-border pt-2">
              <div class="mb-1 text-[10px] font-semibold text-muted-foreground">배치된 텍스트 ({{ placedOverlays.length }})</div>
              <div
                v-for="o in placedOverlays"
                :key="o.id"
                class="group mb-1 flex cursor-pointer items-center gap-1.5 rounded px-1 py-1 text-[10px] transition-colors hover:bg-primary/10"
                :class="{ 'bg-primary/15 font-semibold': selectedItemId === o.id && selectedItemType === 'overlay' }"
                @click="selectItem(o.id, 'overlay')"
              >
                <IconifyIcon icon="lucide:type" class="size-3 shrink-0 text-primary/70" />
                <span class="flex-1 truncate">{{ o.textContent }}</span>
                <button class="hidden size-3.5 items-center justify-center text-destructive group-hover:flex" @click.stop="removeOverlay(o.id)">
                  <IconifyIcon icon="lucide:x" class="size-2.5" />
                </button>
              </div>
            </div>
            <div v-else class="text-center text-[10px] text-muted-foreground">
              배치된 텍스트가 없습니다.
            </div>
          </div>
        </div>

        <!-- ② 모니터 미리보기 -->
        <div ref="monitorAreaRef" class="monitor-area flex flex-1 flex-col items-center justify-center overflow-hidden">
          <div class="mb-1.5 flex shrink-0 items-center gap-2">
            <IconifyIcon icon="lucide:monitor" class="size-4 text-muted-foreground" />
            <span class="text-xs text-muted-foreground">
              {{ displayOrientation === 'PORTRAIT' ? '세로 모니터' : '가로 모니터' }}
            </span>
            <span class="text-[10px] text-muted-foreground">
              (장식: 드래그 배치 · 텍스트: 추가 후 드래그)
            </span>
          </div>

          <div
            ref="monitorRef"
            class="monitor-screen relative overflow-hidden rounded bg-gray-950 shadow-2xl ring-4 ring-gray-700 select-none"
            :style="monitorScreenStyle"
            @dragover="onMonitorDragOver"
            @drop="onMonitorDrop"
            @click.self="clearSelection"
          >
            <!-- 배경 패턴 -->
            <div class="absolute inset-0 flex items-center justify-center">
              <IconifyIcon icon="lucide:monitor-play" class="size-12 text-gray-700 opacity-30" />
            </div>

            <!-- 배치된 리본 (이미지) -->
            <div
              v-for="ribbon in placedRibbons"
              :key="ribbon.id"
              class="placed-item absolute"
              :class="{
                'ring-2 ring-primary ring-offset-1': selectedItemId === ribbon.id && selectedItemType === 'ribbon',
                'ring-1 ring-white/30': !(selectedItemId === ribbon.id && selectedItemType === 'ribbon'),
              }"
              :style="{
                left: `${ribbon.positionLeft}%`,
                top: `${ribbon.positionTop}%`,
                width: `${ribbon.width}%`,
                height: `${ribbon.height}%`,
                cursor: isDragging && dragTargetId === ribbon.id ? 'grabbing' : 'grab',
              }"
              @mousedown="onItemMouseDown(ribbon.id, 'ribbon', $event)"
              @click.stop="selectItem(ribbon.id, 'ribbon')"
            >
              <img
                :src="ribbon.mediaSourceThumbnailUrl || ribbon.mediaSourceUrl"
                :alt="ribbon.mediaSourceName"
                class="h-full w-full object-contain"
                draggable="false"
              />
              <!-- 리본 유형 배지 -->
              <span class="item-badge absolute left-0 top-0 rounded-br bg-blue-600/80 px-0.5 text-[8px] text-white leading-tight">이미지</span>
              <!-- 삭제 버튼 -->
              <button
                v-if="selectedItemId === ribbon.id && selectedItemType === 'ribbon'"
                class="item-delete-btn absolute -right-2 -top-2 flex size-4 items-center justify-center rounded-full bg-destructive text-destructive-foreground shadow"
                @mousedown.stop @click.stop="removeRibbon(ribbon.id)"
              >
                <IconifyIcon icon="lucide:x" class="size-3" />
              </button>
              <!-- 리사이즈 핸들 -->
              <div
                v-if="selectedItemId === ribbon.id && selectedItemType === 'ribbon'"
                class="item-resize-handle absolute -bottom-1 -right-1 size-3 cursor-se-resize rounded-sm bg-primary shadow"
                @mousedown.stop="onResizeMouseDown(ribbon.id, 'ribbon', $event)"
              />
            </div>

            <!-- 배치된 텍스트 오버레이 -->
            <div
              v-for="overlay in placedOverlays"
              :key="overlay.id"
              class="placed-item absolute flex items-center justify-center overflow-hidden"
              :class="{
                'ring-2 ring-yellow-400 ring-offset-1': selectedItemId === overlay.id && selectedItemType === 'overlay',
                'ring-1 ring-white/20': !(selectedItemId === overlay.id && selectedItemType === 'overlay'),
              }"
              :style="overlayStyle(overlay)"
              @mousedown="onItemMouseDown(overlay.id, 'overlay', $event)"
              @click.stop="selectItem(overlay.id, 'overlay')"
            >
              <span class="w-full px-1 leading-tight whitespace-pre-wrap break-words">{{ overlay.textContent }}</span>
              <!-- 텍스트 유형 배지 -->
              <span class="item-badge absolute left-0 top-0 rounded-br bg-yellow-500/80 px-0.5 text-[8px] text-black leading-tight">텍스트</span>
              <!-- 삭제 버튼 -->
              <button
                v-if="selectedItemId === overlay.id && selectedItemType === 'overlay'"
                class="item-delete-btn absolute -right-2 -top-2 flex size-4 items-center justify-center rounded-full bg-destructive text-destructive-foreground shadow"
                @mousedown.stop @click.stop="removeOverlay(overlay.id)"
              >
                <IconifyIcon icon="lucide:x" class="size-3" />
              </button>
              <!-- 리사이즈 핸들 -->
              <div
                v-if="selectedItemId === overlay.id && selectedItemType === 'overlay'"
                class="item-resize-handle absolute -bottom-1 -right-1 size-3 cursor-se-resize rounded-sm bg-yellow-400 shadow"
                @mousedown.stop="onResizeMouseDown(overlay.id, 'overlay', $event)"
              />
            </div>

            <!-- 드래그 오버 표시 -->
            <div
              v-if="draggingDecoration"
              class="pointer-events-none absolute inset-0 border-2 border-dashed border-primary/60 bg-primary/10"
            >
              <div class="flex h-full items-center justify-center">
                <span class="rounded bg-primary/80 px-2 py-1 text-xs text-primary-foreground">여기에 놓기</span>
              </div>
            </div>
          </div>

          <div class="mt-2 shrink-0 text-xs text-muted-foreground">
            <span class="mr-2">
              <span class="inline-block size-2 rounded-sm bg-blue-500 mr-0.5"></span>
              장식 {{ placedRibbons.length }}개
            </span>
            <span>
              <span class="inline-block size-2 rounded-sm bg-yellow-400 mr-0.5"></span>
              텍스트 {{ placedOverlays.length }}개
            </span>
            <span v-if="monitorWidth" class="ml-2 opacity-60">({{ monitorWidth }}×{{ monitorHeight }}px)</span>
          </div>
        </div>

        <!-- ③ 속성 편집 패널 -->
        <div class="property-panel flex w-52 shrink-0 flex-col overflow-hidden rounded-lg border border-border bg-muted/30">
          <div class="flex shrink-0 items-center gap-1.5 border-b border-border px-2 py-1.5">
            <IconifyIcon icon="lucide:sliders-horizontal" class="size-3.5 text-primary" />
            <span class="text-xs font-semibold">속성 조정</span>
          </div>

          <!-- 미선택 상태 -->
          <div v-if="!selectedItemId" class="flex flex-1 flex-col items-center justify-center gap-2 p-4 text-center">
            <IconifyIcon icon="lucide:mouse-pointer-click" class="size-8 text-muted-foreground/50" />
            <span class="text-xs text-muted-foreground">모니터의 장식 이미지 또는 텍스트를 클릭하면 위치·크기·스타일을 조정할 수 있습니다.</span>
          </div>

          <!-- 리본(이미지) 속성 -->
          <div v-else-if="selectedItemType === 'ribbon' && selectedRibbon" class="flex-1 overflow-y-auto p-3">
            <!-- 미리보기 -->
            <div class="mb-3 flex items-center gap-2 rounded bg-muted p-1.5">
              <div class="checkerboard size-8 shrink-0 overflow-hidden rounded border border-border">
                <img :src="selectedRibbon.mediaSourceThumbnailUrl || selectedRibbon.mediaSourceUrl" class="h-full w-full object-contain" />
              </div>
              <div class="min-w-0">
                <div class="truncate text-[10px] font-semibold">{{ selectedRibbon.mediaSourceName }}</div>
                <div class="text-[9px] text-primary">장식 이미지</div>
              </div>
            </div>

            <div class="space-y-3">
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">좌측 위치 (%)</label>
                <InputNumber :value="selectedRibbon.positionLeft" :min="0" :max="99" :precision="3" :step="0.5" size="small" addon-after="%" style="width:100%" @change="(v) => updateRibbon('positionLeft', v as number)" />
              </div>
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">상단 위치 (%)</label>
                <InputNumber :value="selectedRibbon.positionTop" :min="0" :max="99" :precision="3" :step="0.5" size="small" addon-after="%" style="width:100%" @change="(v) => updateRibbon('positionTop', v as number)" />
              </div>
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">너비 (%)</label>
                <InputNumber :value="selectedRibbon.width" :min="1" :max="100" :precision="3" :step="0.5" size="small" addon-after="%" style="width:100%" @change="(v) => updateRibbon('width', v as number)" />
              </div>
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">높이 (%)</label>
                <InputNumber :value="selectedRibbon.height" :min="1" :max="100" :precision="3" :step="0.5" size="small" addon-after="%" style="width:100%" @change="(v) => updateRibbon('height', v as number)" />
              </div>
              <div class="rounded bg-muted p-2 text-[10px] text-muted-foreground leading-5">
                <div>Left: {{ selectedRibbon.positionLeft.toFixed(3) }}%</div>
                <div>Top: {{ selectedRibbon.positionTop.toFixed(3) }}%</div>
                <div>W: {{ selectedRibbon.width.toFixed(3) }}% / H: {{ selectedRibbon.height.toFixed(3) }}%</div>
              </div>
              <Popconfirm title="이 장식을 제거하시겠습니까?" @confirm="removeRibbon(selectedRibbon!.id)">
                <Button danger block size="small">
                  <IconifyIcon icon="lucide:trash-2" class="mr-1 size-3.5" />
                  장식 제거
                </Button>
              </Popconfirm>
            </div>
          </div>

          <!-- 텍스트 오버레이 속성 -->
          <div v-else-if="selectedItemType === 'overlay' && selectedOverlay" class="flex-1 overflow-y-auto p-3">
            <!-- 미리보기 -->
            <div class="mb-3 flex items-center gap-2 rounded bg-muted p-1.5">
              <div class="flex size-8 shrink-0 items-center justify-center rounded border border-border bg-gray-800">
                <IconifyIcon icon="lucide:type" class="size-4 text-yellow-400" />
              </div>
              <div class="min-w-0">
                <div class="truncate text-[10px] font-semibold">{{ selectedOverlay.textContent }}</div>
                <div class="text-[9px] text-yellow-500">텍스트 오버레이</div>
              </div>
            </div>

            <div class="space-y-3">
              <!-- 텍스트 내용 -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">텍스트 내용</label>
                <Input.TextArea
                  :value="selectedOverlay.textContent"
                  :rows="2" size="small"
                  @change="(e) => updateOverlay('textContent', (e.target as HTMLTextAreaElement).value)"
                />
              </div>
              <!-- 위치/크기 -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">좌측 위치 (%)</label>
                <InputNumber :value="selectedOverlay.positionLeft" :min="0" :max="99" :precision="3" :step="0.5" size="small" addon-after="%" style="width:100%" @change="(v) => updateOverlay('positionLeft', v as number)" />
              </div>
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">상단 위치 (%)</label>
                <InputNumber :value="selectedOverlay.positionTop" :min="0" :max="99" :precision="3" :step="0.5" size="small" addon-after="%" style="width:100%" @change="(v) => updateOverlay('positionTop', v as number)" />
              </div>
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">너비 (%)</label>
                <InputNumber :value="selectedOverlay.width" :min="5" :max="100" :precision="3" :step="1" size="small" addon-after="%" style="width:100%" @change="(v) => updateOverlay('width', v as number)" />
              </div>
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">높이 (%)</label>
                <InputNumber :value="selectedOverlay.height" :min="3" :max="100" :precision="3" :step="1" size="small" addon-after="%" style="width:100%" @change="(v) => updateOverlay('height', v as number)" />
              </div>

              <!-- 스타일 구분선 -->
              <div class="border-t border-border pt-2">
                <div class="mb-2 text-[10px] font-semibold text-muted-foreground">텍스트 스타일</div>
                <div class="mb-2">
                  <label class="mb-1 block text-[11px] text-muted-foreground">폰트 크기 (화면 높이 %)</label>
                  <InputNumber :value="selectedOverlay.fontSize" :min="0.5" :max="20" :precision="1" :step="0.5" size="small" addon-after="%" style="width:100%" @change="(v) => updateOverlay('fontSize', v as number)" />
                </div>
                <div class="mb-2">
                  <label class="mb-1 block text-[11px] text-muted-foreground">글자 색상</label>
                  <div class="flex items-center gap-2">
                    <input type="color" :value="selectedOverlay.fontColor" class="h-7 w-10 cursor-pointer rounded border border-border bg-transparent p-0.5" @input="(e) => updateOverlay('fontColor', (e.target as HTMLInputElement).value)" />
                    <span class="font-mono text-[10px] text-muted-foreground">{{ selectedOverlay.fontColor }}</span>
                  </div>
                </div>
                <div class="mb-2">
                  <label class="mb-1 block text-[11px] text-muted-foreground">배경 색상</label>
                  <div class="flex items-center gap-2">
                    <input type="color" :value="selectedOverlay.backgroundColor === 'transparent' ? '#000000' : selectedOverlay.backgroundColor" class="h-7 w-10 cursor-pointer rounded border border-border bg-transparent p-0.5" @input="(e) => updateOverlay('backgroundColor', (e.target as HTMLInputElement).value)" />
                    <Tooltip title="배경을 투명하게">
                      <button
                        class="rounded border border-border px-1.5 py-0.5 text-[10px] transition-colors hover:bg-muted"
                        :class="{ 'bg-primary text-primary-foreground border-primary': selectedOverlay.backgroundColor === 'transparent' }"
                        @click="updateOverlay('backgroundColor', 'transparent')"
                      >투명</button>
                    </Tooltip>
                  </div>
                </div>
                <div class="mb-2">
                  <label class="mb-1 block text-[11px] text-muted-foreground">텍스트 정렬</label>
                  <Select :value="selectedOverlay.textAlign" :options="TEXT_ALIGN_OPTIONS" size="small" style="width:100%" @change="(v) => updateOverlay('textAlign', v as 'left' | 'center' | 'right')" />
                </div>
                <div class="mb-2">
                  <label class="mb-1 block text-[11px] text-muted-foreground">글자 굵기</label>
                  <Select :value="selectedOverlay.fontWeight" :options="FONT_WEIGHT_OPTIONS" size="small" style="width:100%" @change="(v) => updateOverlay('fontWeight', v as 'normal' | 'bold')" />
                </div>
              </div>

              <!-- 수치 요약 -->
              <div class="rounded bg-muted p-2 text-[10px] text-muted-foreground leading-5">
                <div>Left: {{ selectedOverlay.positionLeft.toFixed(3) }}%</div>
                <div>Top: {{ selectedOverlay.positionTop.toFixed(3) }}%</div>
                <div>W: {{ selectedOverlay.width.toFixed(3) }}% / H: {{ selectedOverlay.height.toFixed(3) }}%</div>
                <div>Font: {{ selectedOverlay.fontSize }}%</div>
              </div>

              <Popconfirm title="이 텍스트 오버레이를 제거하시겠습니까?" @confirm="removeOverlay(selectedOverlay!.id)">
                <Button danger block size="small">
                  <IconifyIcon icon="lucide:trash-2" class="mr-1 size-3.5" />
                  텍스트 제거
                </Button>
              </Popconfirm>
            </div>
          </div>
        </div>

      </div>

      <!-- 하단 저장 버튼 -->
      <div class="flex shrink-0 items-center justify-between border-t border-border bg-muted/40 px-4 py-2">
        <div class="text-xs text-muted-foreground">
          <span class="mr-3">
            <span class="inline-block size-2 rounded-sm bg-blue-500 mr-0.5 align-middle"></span>
            장식 이미지 {{ placedRibbons.length }}개
          </span>
          <span>
            <span class="inline-block size-2 rounded-sm bg-yellow-400 mr-0.5 align-middle"></span>
            텍스트 오버레이 {{ placedOverlays.length }}개
          </span>
        </div>
        <div class="flex gap-2">
          <Button @click="handleReset">초기화</Button>
          <Button type="primary" :loading="saving" @click="handleSave(false)">
            <IconifyIcon icon="lucide:save" class="mr-1 size-4" />
            모두 저장
          </Button>
        </div>
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

/* ─── 모니터 화면 ─── */
.monitor-screen {
  position: relative;
}

/* ─── 배치 아이템 공통 ─── */
.placed-item {
  transition: box-shadow 0.1s;
  user-select: none;
}

.placed-item:hover {
  z-index: 10;
}

.placed-item.ring-2 {
  z-index: 20;
}

/* ─── 유형 배지 ─── */
.item-badge {
  pointer-events: none;
  z-index: 25;
}

/* ─── 삭제 버튼 ─── */
.item-delete-btn {
  z-index: 30;
  font-size: 10px;
  line-height: 1;
  border: none;
  outline: none;
  padding: 0;
}

/* ─── 리사이즈 핸들 ─── */
.item-resize-handle {
  z-index: 30;
}

/* ─── 사이드 탭 ─── */
.side-tab {
  border: none;
  background: none;
  cursor: pointer;
}

/* ─── 레이아웃 ─── */
.ribbon-main {
  min-height: 0;
}
</style>
