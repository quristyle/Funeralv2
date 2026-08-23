<script setup lang="ts">
/**
 * 좌우(또는 상하) 분할 패널 — 이식 전 `RadzenSplitter`.
 *
 * 프로젝트관리 화면 대부분이 "왼쪽에서 고르면 오른쪽에 상세" 구조라
 * 분할 패널이 없으면 화면 절반이 어색해진다. ant-design-vue 4.2 에는
 * Splitter 가 없어 필요한 만큼만 직접 만들었다.
 *
 * 경계를 끌어 비율을 바꿀 수 있고, 비율은 부모가 `v-model:size` 로 붙잡아 둘 수 있다.
 */
import { computed, onBeforeUnmount, ref } from 'vue';

interface Props {
  /** 첫 패널의 비율(%) */
  size?: number;
  min?: number;
  max?: number;
  direction?: 'horizontal' | 'vertical';
}

const props = withDefaults(defineProps<Props>(), {
  size: 30,
  min: 10,
  max: 90,
  direction: 'horizontal',
});

const emit = defineEmits<{
  (e: 'update:size', value: number): void;
}>();

const containerRef = ref<HTMLElement>();
const dragging = ref(false);
const innerSize = ref(props.size);

const currentSize = computed(() => props.size ?? innerSize.value);
const isHorizontal = computed(() => props.direction === 'horizontal');

function onPointerMove(event: PointerEvent) {
  const el = containerRef.value;
  if (!dragging.value || !el) return;

  const rect = el.getBoundingClientRect();
  const ratio = isHorizontal.value
    ? ((event.clientX - rect.left) / rect.width) * 100
    : ((event.clientY - rect.top) / rect.height) * 100;

  const clamped = Math.min(props.max, Math.max(props.min, ratio));
  innerSize.value = clamped;
  emit('update:size', clamped);
}

function stopDrag() {
  dragging.value = false;
  window.removeEventListener('pointermove', onPointerMove);
  window.removeEventListener('pointerup', stopDrag);
}

function startDrag() {
  dragging.value = true;
  window.addEventListener('pointermove', onPointerMove);
  window.addEventListener('pointerup', stopDrag);
}

onBeforeUnmount(stopDrag);
</script>

<template>
  <div
    ref="containerRef"
    class="flex h-full min-h-0 w-full"
    :class="isHorizontal ? 'flex-row' : 'flex-col'"
  >
    <div
      class="min-h-0 min-w-0 overflow-hidden"
      :style="
        isHorizontal
          ? { width: `${currentSize}%` }
          : { height: `${currentSize}%` }
      "
    >
      <slot name="first"></slot>
    </div>

    <div
      class="hover:bg-primary/40 shrink-0 transition-colors"
      :class="[
        isHorizontal ? 'w-1 cursor-col-resize' : 'h-1 cursor-row-resize',
        dragging ? 'bg-primary/60' : 'bg-border',
      ]"
      @pointerdown.prevent="startDrag"
    ></div>

    <div class="min-h-0 min-w-0 flex-1 overflow-hidden">
      <slot name="second"></slot>
    </div>
  </div>
</template>
