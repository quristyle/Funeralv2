<script lang="ts" setup>
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue';
import {
  Button, Spin, Tooltip, Popconfirm, InputNumber, Input, Select, message,
} from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import {
  getDeviceTextOverlays,
  bulkSaveDeviceTextOverlays,
} from '#/api/funeral/building';
import type { BuildingApi } from '#/api/funeral/building';

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

/** 현재 배치된 텍스트 오버레이 목록 (편집 중인 상태) */
const placedOverlays = ref<BuildingApi.DeviceTextOverlay[]>([]);

/** 선택된 오버레이 ID (편집 패널용) */
const selectedOverlayId = ref<string | null>(null);

/** 드래그 이동/리사이즈 상태 */
const isDragging = ref(false);
const isResizing = ref(false);
const dragTarget = ref<string | null>(null);
const dragStartX = ref(0);
const dragStartY = ref(0);
const dragStartLeft = ref(0);
const dragStartTop = ref(0);
const resizeStartWidth = ref(0);
const resizeStartHeight = ref(0);

/** 새 텍스트 입력 (추가 패널) */
const newTextContent = ref('');

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

const isPortrait = computed(() => props.displayOrientation === 'PORTRAIT');

/** 모니터 외관 스타일 (동적 크기) */
const monitorScreenStyle = computed(() => {
  const w = monitorWidth.value > 0 ? monitorWidth.value : 480;
  const h = monitorHeight.value > 0 ? monitorHeight.value : 270;
  return { width: `${w}px`, height: `${h}px` };
});

const selectedOverlay = computed(() =>
  placedOverlays.value.find((o) => o.id === selectedOverlayId.value) ?? null,
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

async function loadOverlays() {
  loading.value = true;
  try {
    const res = await getDeviceTextOverlays(props.deviceId);
    placedOverlays.value = Array.isArray(res) ? res : (res as any)?.result ?? [];
  } catch {
    message.error('텍스트 오버레이 목록 로드 실패');
  } finally {
    loading.value = false;
  }
}

onMounted(() => { loadOverlays(); });
onUnmounted(() => { resizeObserver?.disconnect(); resizeObserver = null; });

watch(monitorAreaRef, async (el) => {
  if (el) { await nextTick(); attachResizeObserver(); }
  else { resizeObserver?.disconnect(); resizeObserver = null; }
});

watch(() => props.deviceId, () => {
  placedOverlays.value = [];
  selectedOverlayId.value = null;
  loadOverlays();
});

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
    const dto: BuildingApi.DeviceTextOverlayBulkSave = {
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
    };
    const res = await bulkSaveDeviceTextOverlays(dto);
    placedOverlays.value = Array.isArray(res) ? res : (res as any)?.result ?? [];
    selectedOverlayId.value = null;
    message.success('텍스트 오버레이 설정이 저장되었습니다.');
  } catch {
    message.error('텍스트 오버레이 저장 실패');
  } finally {
    saving.value = false;
  }
}

function handleReset() {
  selectedOverlayId.value = null;
  loadOverlays();
}

// ────────────────────────────────────────────────────────────────────
// 텍스트 추가
// ────────────────────────────────────────────────────────────────────

function addTextOverlay() {
  const text = newTextContent.value.trim();
  if (!text) {
    message.warning('추가할 텍스트를 입력해 주세요.');
    return;
  }

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
  selectedOverlayId.value = newOverlay.id;
  newTextContent.value = '';
}

// ────────────────────────────────────────────────────────────────────
// 오버레이 이동 (마우스 드래그)
// ────────────────────────────────────────────────────────────────────

function onOverlayMouseDown(overlayId: string, evt: MouseEvent) {
  evt.stopPropagation();
  evt.preventDefault();
  selectedOverlayId.value = overlayId;

  const overlay = placedOverlays.value.find((o) => o.id === overlayId);
  if (!overlay || !monitorRef.value) return;

  isDragging.value = true;
  dragTarget.value = overlayId;
  dragStartX.value = evt.clientX;
  dragStartY.value = evt.clientY;
  dragStartLeft.value = overlay.positionLeft;
  dragStartTop.value = overlay.positionTop;

  window.addEventListener('mousemove', onMouseMove);
  window.addEventListener('mouseup', onMouseUp);
}

function onResizeMouseDown(overlayId: string, evt: MouseEvent) {
  evt.stopPropagation();
  evt.preventDefault();
  selectedOverlayId.value = overlayId;

  const overlay = placedOverlays.value.find((o) => o.id === overlayId);
  if (!overlay || !monitorRef.value) return;

  isResizing.value = true;
  dragTarget.value = overlayId;
  dragStartX.value = evt.clientX;
  dragStartY.value = evt.clientY;
  resizeStartWidth.value = overlay.width;
  resizeStartHeight.value = overlay.height;

  window.addEventListener('mousemove', onMouseMove);
  window.addEventListener('mouseup', onMouseUp);
}

function onMouseMove(evt: MouseEvent) {
  if (!monitorRef.value || !dragTarget.value) return;

  const rect = monitorRef.value.getBoundingClientRect();
  const dxPct = ((evt.clientX - dragStartX.value) / rect.width) * 100;
  const dyPct = ((evt.clientY - dragStartY.value) / rect.height) * 100;

  placedOverlays.value = placedOverlays.value.map((o) => {
    if (o.id !== dragTarget.value) return o;

    if (isDragging.value) {
      const newLeft = round3(Math.max(0, Math.min(dragStartLeft.value + dxPct, 100 - o.width)));
      const newTop = round3(Math.max(0, Math.min(dragStartTop.value + dyPct, 100 - o.height)));
      return { ...o, positionLeft: newLeft, positionTop: newTop };
    }

    if (isResizing.value) {
      const newW = round3(Math.max(5, Math.min(resizeStartWidth.value + dxPct, 100 - o.positionLeft)));
      const newH = round3(Math.max(3, Math.min(resizeStartHeight.value + dyPct, 100 - o.positionTop)));
      return { ...o, width: newW, height: newH };
    }

    return o;
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
// 오버레이 삭제
// ────────────────────────────────────────────────────────────────────

function removeOverlay(overlayId: string) {
  placedOverlays.value = placedOverlays.value.filter((o) => o.id !== overlayId);
  if (selectedOverlayId.value === overlayId) selectedOverlayId.value = null;
}

// ────────────────────────────────────────────────────────────────────
// 수치 직접 입력 (선택된 오버레이)
// ────────────────────────────────────────────────────────────────────

function updateSelectedOverlay<K extends keyof BuildingApi.DeviceTextOverlay>(
  field: K,
  value: BuildingApi.DeviceTextOverlay[K],
) {
  if (!selectedOverlayId.value) return;
  placedOverlays.value = placedOverlays.value.map((o) =>
    o.id === selectedOverlayId.value
      ? { ...o, [field]: typeof value === 'number' ? round3(value as number) : value }
      : o,
  );
}

// ────────────────────────────────────────────────────────────────────
// 텍스트 오버레이 인라인 스타일 계산
// ────────────────────────────────────────────────────────────────────

function overlayStyle(o: BuildingApi.DeviceTextOverlay): Record<string, string> {
  // fontSize를 화면 높이 대비 %로 변환하여 px로 렌더링
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
    cursor: isDragging.value && dragTarget.value === o.id ? 'grabbing' : 'grab',
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
  <div class="text-overlay-tab flex h-full flex-col">
    <!-- 로딩 -->
    <div v-if="loading" class="flex flex-1 items-center justify-center py-16">
      <Spin tip="텍스트 오버레이 설정 불러오는 중..." />
    </div>

    <template v-else>
      <!-- 메인 영역: 추가 패널 + 모니터 미리보기 + 속성 패널 -->
      <div class="overlay-main flex min-h-0 flex-1 gap-3 overflow-hidden px-3 pb-3 pt-3">

        <!-- ① 텍스트 추가 사이드패널 -->
        <div class="add-panel flex w-44 shrink-0 flex-col overflow-hidden rounded-lg border border-border bg-muted/30">
          <div class="flex shrink-0 items-center gap-1.5 border-b border-border px-2 py-1.5">
            <IconifyIcon icon="lucide:type" class="size-3.5 text-primary" />
            <span class="text-xs font-semibold">텍스트 추가</span>
          </div>
          <div class="flex flex-1 flex-col gap-2 overflow-y-auto p-3">
            <div>
              <label class="mb-1 block text-[11px] text-muted-foreground">텍스트 내용</label>
              <Input.TextArea
                v-model:value="newTextContent"
                :rows="3"
                placeholder="표시할 텍스트를 입력하세요."
                size="small"
                @press-enter.prevent="addTextOverlay"
              />
            </div>
            <Button type="primary" size="small" block @click="addTextOverlay">
              <IconifyIcon icon="lucide:plus" class="size-3.5 mr-1" />
              화면에 추가
            </Button>

            <!-- 배치된 텍스트 목록 -->
            <div v-if="placedOverlays.length > 0" class="mt-2 border-t border-border pt-2">
              <div class="mb-1.5 text-[10px] font-semibold text-muted-foreground">배치된 텍스트</div>
              <div
                v-for="o in placedOverlays"
                :key="o.id"
                class="overlay-list-item group flex cursor-pointer items-center gap-1.5 rounded px-1.5 py-1 text-[10px] transition-colors hover:bg-primary/10"
                :class="{ 'bg-primary/15 font-semibold': selectedOverlayId === o.id }"
                @click="selectedOverlayId = o.id"
              >
                <IconifyIcon icon="lucide:type" class="size-3 shrink-0 text-primary/70" />
                <span class="truncate flex-1" :style="{ color: o.fontColor !== '#FFFFFF' ? o.fontColor : undefined }">
                  {{ o.textContent }}
                </span>
                <button
                  class="hidden size-3.5 items-center justify-center rounded text-destructive group-hover:flex"
                  title="삭제"
                  @click.stop="removeOverlay(o.id)"
                >
                  <IconifyIcon icon="lucide:x" class="size-2.5" />
                </button>
              </div>
            </div>
            <div v-else class="mt-2 text-center text-[10px] text-muted-foreground">
              배치된 텍스트가 없습니다.
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
            <span class="text-[10px] text-muted-foreground">(텍스트를 드래그하여 위치 조정)</span>
          </div>

          <!-- 모니터 화면 -->
          <div
            ref="monitorRef"
            class="monitor-screen relative overflow-hidden rounded bg-gray-950 shadow-2xl ring-4 ring-gray-700 select-none"
            :style="monitorScreenStyle"
            @click.self="selectedOverlayId = null"
          >
            <!-- 화면 배경 패턴 -->
            <div class="absolute inset-0 flex items-center justify-center">
              <IconifyIcon icon="lucide:monitor-play" class="size-12 text-gray-700 opacity-30" />
            </div>

            <!-- 배치된 텍스트 오버레이들 -->
            <div
              v-for="overlay in placedOverlays"
              :key="overlay.id"
              class="overlay-item absolute flex items-center justify-center overflow-hidden"
              :class="{
                'ring-2 ring-primary ring-offset-1': selectedOverlayId === overlay.id,
                'ring-1 ring-white/20': selectedOverlayId !== overlay.id,
              }"
              :style="overlayStyle(overlay)"
              @mousedown="onOverlayMouseDown(overlay.id, $event)"
              @click.stop="selectedOverlayId = overlay.id"
            >
              <!-- 텍스트 내용 -->
              <span class="w-full px-1 leading-tight break-words whitespace-pre-wrap">
                {{ overlay.textContent }}
              </span>

              <!-- 삭제 버튼 -->
              <button
                v-if="selectedOverlayId === overlay.id"
                class="overlay-delete-btn absolute -right-2 -top-2 flex size-4 items-center justify-center rounded-full bg-destructive text-destructive-foreground shadow"
                title="텍스트 삭제"
                @mousedown.stop
                @click.stop="removeOverlay(overlay.id)"
              >
                <IconifyIcon icon="lucide:x" class="size-3" />
              </button>

              <!-- 리사이즈 핸들 (우하단) -->
              <div
                v-if="selectedOverlayId === overlay.id"
                class="overlay-resize-handle absolute -bottom-1 -right-1 size-3 cursor-se-resize rounded-sm bg-primary shadow"
                @mousedown.stop="onResizeMouseDown(overlay.id, $event)"
              />
            </div>
          </div>

          <div class="mt-2 shrink-0 text-xs text-muted-foreground">
            배치된 텍스트: {{ placedOverlays.length }}개
            <span v-if="monitorWidth" class="ml-2 opacity-60">({{ monitorWidth }}×{{ monitorHeight }}px)</span>
          </div>
        </div>

        <!-- ③ 속성 편집 패널 -->
        <div class="property-panel flex w-52 shrink-0 flex-col overflow-hidden rounded-lg border border-border bg-muted/30">
          <div class="flex shrink-0 items-center gap-1.5 border-b border-border px-2 py-1.5">
            <IconifyIcon icon="lucide:sliders-horizontal" class="size-3.5 text-primary" />
            <span class="text-xs font-semibold">속성 조정</span>
          </div>

          <div v-if="!selectedOverlay" class="flex flex-1 flex-col items-center justify-center gap-2 p-4 text-center">
            <IconifyIcon icon="lucide:mouse-pointer-click" class="size-8 text-muted-foreground/50" />
            <span class="text-xs text-muted-foreground">모니터에 배치된 텍스트를 클릭하면 위치·크기·스타일을 조정할 수 있습니다.</span>
          </div>

          <div v-else class="flex-1 overflow-y-auto p-3">
            <!-- 텍스트 내용 수정 -->
            <div class="mb-3">
              <label class="mb-1 block text-[11px] text-muted-foreground">텍스트 내용</label>
              <Input.TextArea
                :value="selectedOverlay.textContent"
                :rows="2"
                size="small"
                @change="(e) => updateSelectedOverlay('textContent', (e.target as HTMLTextAreaElement).value)"
              />
            </div>

            <div class="space-y-3">
              <!-- 위치 Left -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">좌측 위치 (%)</label>
                <InputNumber
                  :value="selectedOverlay.positionLeft"
                  :min="0" :max="99" :precision="3" :step="0.5"
                  size="small" addon-after="%" style="width: 100%"
                  @change="(v) => updateSelectedOverlay('positionLeft', v as number)"
                />
              </div>

              <!-- 위치 Top -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">상단 위치 (%)</label>
                <InputNumber
                  :value="selectedOverlay.positionTop"
                  :min="0" :max="99" :precision="3" :step="0.5"
                  size="small" addon-after="%" style="width: 100%"
                  @change="(v) => updateSelectedOverlay('positionTop', v as number)"
                />
              </div>

              <!-- 너비 -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">너비 (%)</label>
                <InputNumber
                  :value="selectedOverlay.width"
                  :min="5" :max="100" :precision="3" :step="1"
                  size="small" addon-after="%" style="width: 100%"
                  @change="(v) => updateSelectedOverlay('width', v as number)"
                />
              </div>

              <!-- 높이 -->
              <div>
                <label class="mb-1 block text-[11px] text-muted-foreground">높이 (%)</label>
                <InputNumber
                  :value="selectedOverlay.height"
                  :min="3" :max="100" :precision="3" :step="1"
                  size="small" addon-after="%" style="width: 100%"
                  @change="(v) => updateSelectedOverlay('height', v as number)"
                />
              </div>

              <!-- 구분선 -->
              <div class="border-t border-border pt-2">
                <!-- 폰트 크기 (화면 높이 %) -->
                <div class="mb-3">
                  <label class="mb-1 block text-[11px] text-muted-foreground">폰트 크기 (화면 높이 %)</label>
                  <InputNumber
                    :value="selectedOverlay.fontSize"
                    :min="0.5" :max="20" :precision="1" :step="0.5"
                    size="small" addon-after="%" style="width: 100%"
                    @change="(v) => updateSelectedOverlay('fontSize', v as number)"
                  />
                </div>

                <!-- 글자 색상 -->
                <div class="mb-3">
                  <label class="mb-1 block text-[11px] text-muted-foreground">글자 색상</label>
                  <div class="flex items-center gap-2">
                    <input
                      type="color"
                      :value="selectedOverlay.fontColor"
                      class="h-7 w-10 cursor-pointer rounded border border-border bg-transparent p-0.5"
                      @input="(e) => updateSelectedOverlay('fontColor', (e.target as HTMLInputElement).value)"
                    />
                    <span class="text-[10px] text-muted-foreground font-mono">{{ selectedOverlay.fontColor }}</span>
                  </div>
                </div>

                <!-- 배경 색상 -->
                <div class="mb-3">
                  <label class="mb-1 block text-[11px] text-muted-foreground">배경 색상</label>
                  <div class="flex items-center gap-2">
                    <input
                      type="color"
                      :value="selectedOverlay.backgroundColor === 'transparent' ? '#000000' : selectedOverlay.backgroundColor"
                      class="h-7 w-10 cursor-pointer rounded border border-border bg-transparent p-0.5"
                      @input="(e) => updateSelectedOverlay('backgroundColor', (e.target as HTMLInputElement).value)"
                    />
                    <Tooltip title="배경을 투명하게 설정">
                      <button
                        class="rounded border border-border px-1.5 py-0.5 text-[10px] transition-colors hover:bg-muted"
                        :class="{ 'bg-primary text-primary-foreground border-primary': selectedOverlay.backgroundColor === 'transparent' }"
                        @click="updateSelectedOverlay('backgroundColor', 'transparent')"
                      >투명</button>
                    </Tooltip>
                  </div>
                </div>

                <!-- 정렬 -->
                <div class="mb-3">
                  <label class="mb-1 block text-[11px] text-muted-foreground">텍스트 정렬</label>
                  <Select
                    :value="selectedOverlay.textAlign"
                    :options="TEXT_ALIGN_OPTIONS"
                    size="small"
                    style="width: 100%"
                    @change="(v) => updateSelectedOverlay('textAlign', v as 'left' | 'center' | 'right')"
                  />
                </div>

                <!-- 굵기 -->
                <div class="mb-3">
                  <label class="mb-1 block text-[11px] text-muted-foreground">글자 굵기</label>
                  <Select
                    :value="selectedOverlay.fontWeight"
                    :options="FONT_WEIGHT_OPTIONS"
                    size="small"
                    style="width: 100%"
                    @change="(v) => updateSelectedOverlay('fontWeight', v as 'normal' | 'bold')"
                  />
                </div>
              </div>

              <!-- 현재 수치 요약 -->
              <div class="rounded bg-muted p-2 text-[10px] text-muted-foreground leading-5">
                <div>Left: {{ selectedOverlay.positionLeft.toFixed(3) }}%</div>
                <div>Top: {{ selectedOverlay.positionTop.toFixed(3) }}%</div>
                <div>W: {{ selectedOverlay.width.toFixed(3) }}% / H: {{ selectedOverlay.height.toFixed(3) }}%</div>
                <div>Font: {{ selectedOverlay.fontSize }}%</div>
              </div>

              <!-- 삭제 버튼 -->
              <Popconfirm
                title="이 텍스트 오버레이를 제거하시겠습니까?"
                @confirm="removeOverlay(selectedOverlay!.id)"
              >
                <Button danger block size="small">
                  <IconifyIcon icon="lucide:trash-2" class="size-3.5 mr-1" />
                  텍스트 제거
                </Button>
              </Popconfirm>
            </div>
          </div>
        </div>

      </div>

      <!-- 하단 저장 버튼 -->
      <div class="flex shrink-0 justify-end gap-2 border-t border-border bg-muted/40 px-4 py-2">
        <Button @click="handleReset">초기화</Button>
        <Button v-perm:update type="primary" :loading="saving" @click="handleSave">
          <IconifyIcon icon="lucide:save" class="size-4 mr-1" />
          텍스트 저장
        </Button>
      </div>
    </template>
  </div>
</template>

<style scoped>
/* ─── 모니터 화면 ─── */
.monitor-screen {
  position: relative;
}

/* ─── 오버레이 아이템 ─── */
.overlay-item {
  transition: box-shadow 0.1s;
  user-select: none;
}

.overlay-item:hover {
  z-index: 10;
}

.overlay-item.ring-2 {
  z-index: 20;
}

/* ─── 삭제 버튼 ─── */
.overlay-delete-btn {
  z-index: 30;
  font-size: 10px;
  line-height: 1;
  border: none;
  outline: none;
  padding: 0;
}

/* ─── 리사이즈 핸들 ─── */
.overlay-resize-handle {
  z-index: 30;
}

/* ─── 레이아웃 조정 ─── */
.overlay-main {
  min-height: 0;
}
</style>
