<script lang="ts" setup>
import { ref, watch } from 'vue';

import { cn } from '@vben-core/shared/utils';

interface Props {
  class?: string;
  /**
   * @ko_KR 최소 로딩 시간
   * @en_US Minimum loading time
   */
  minLoadingTime?: number;
  /**
   * @ko_KR 로딩 상태 활성화
   */
  spinning?: boolean;
}

defineOptions({
  name: 'VbenSpinner',
});

const props = withDefaults(defineProps<Props>(), {
  minLoadingTime: 50,
});
// const startTime = ref(0);
const showSpinner = ref(false);
const renderSpinner = ref(false);
const timer = ref<ReturnType<typeof setTimeout>>();

watch(
  () => props.spinning,
  (show) => {
    if (!show) {
      showSpinner.value = false;
      clearTimeout(timer.value);
      return;
    }

    // startTime.value = performance.now();
    timer.value = setTimeout(() => {
      // const loadingTime = performance.now() - startTime.value;

      showSpinner.value = true;
      if (showSpinner.value) {
        renderSpinner.value = true;
      }
    }, props.minLoadingTime);
  },
  {
    immediate: true,
  },
);

function onTransitionEnd() {
  if (!showSpinner.value) {
    renderSpinner.value = false;
  }
}
</script>

<!--
  화면 전환·대기 표시.

  예전에는 정사각형이 통통 튀며 굴렀다(`loader-jump-ani`). 브랜드를 입히면서 바꿨다.

  **심볼(JS 인터록)을 굴리지 않는다.** 사용 규칙(docs/brand/README.md 5절)이
  기울이기·회전·비율 변경을 금지한다. 정체성 마크를 찌그러뜨리며 돌리는 것이 로딩 표시라,
  심볼은 애초에 대상이 아니다.

  대신 **상승 조각**을 쓴다. 이것은 "페이지 전체에 같은 리듬을 반복하는 보조 그래픽" 으로
  둔 것이라(README 6절) 기다림 표시에 그대로 맞고, 각도(사선 22 : 높이 100)도 규칙 안에 있다.

  좌표와 움직임은 `docs/brand/generate.py` 가 만든 `loader-shards.svg` 를 옮긴 것이다.
  **여기서 고치지 않는다** — generate.py 를 고치고 다시 뽑아서 옮긴다.
  파일을 가져오지 않고 인라인한 이유는 이것이 공용 패키지라, 특정 앱의 `public/` 경로를
  박으면 그 파일이 없는 앱에서 깨지기 때문이다.

  색은 `currentColor` 다. 아래에서 `text-foreground` 를 주므로 밝은/어두운 테마가 알아서 맞는다.
-->
<template>
  <div
    :class="
      cn(
        'flex-center bg-overlay-content absolute top-0 left-0 z-100 size-full backdrop-blur-xs transition-all duration-500',
        {
          'invisible pointer-events-none opacity-0': !showSpinner,
          'pointer-events-auto': showSpinner,
        },
        props.class,
      )
    "
    @transitionend="onTransitionEnd"
  >
    <svg
      v-if="renderSpinner"
      :class="{ paused: !renderSpinner }"
      class="jsini-loader text-foreground size-12"
      viewBox="0 0 48 48"
      role="img"
      aria-label="loading"
    >
      <g transform="translate(4.84,0) skewX(-12.4)">
        <rect
          class="jsini-loader-bar"
          x="5"
          y="28"
          width="6"
          height="16"
          fill="currentColor"
          opacity="0.3"
        />
        <rect
          class="jsini-loader-bar"
          x="16"
          y="22"
          width="6"
          height="22"
          fill="currentColor"
          opacity="0.5"
        />
        <rect
          class="jsini-loader-bar"
          x="27"
          y="16"
          width="6"
          height="28"
          fill="currentColor"
          opacity="0.75"
        />
        <rect
          class="jsini-loader-bar"
          x="38"
          y="10"
          width="6"
          height="34"
          fill="currentColor"
          opacity="1"
        />
      </g>
    </svg>
  </div>
</template>

<style scoped>
.jsini-loader-bar {
  transform-origin: center bottom;
  animation: jsini-loader-rise 1.1s ease-in-out infinite;
}

.jsini-loader-bar:nth-child(1) {
  animation-delay: 0s;
}

.jsini-loader-bar:nth-child(2) {
  animation-delay: 0.12s;
}

.jsini-loader-bar:nth-child(3) {
  animation-delay: 0.24s;
}

.jsini-loader-bar:nth-child(4) {
  animation-delay: 0.36s;
}

/* 사라지는 중에는 멈춰 둔다. 페이드아웃과 움직임이 겹치면 지저분하다. */
.paused .jsini-loader-bar {
  animation-play-state: paused;
}

@keyframes jsini-loader-rise {
  0%,
  100% {
    transform: scaleY(0.55);
  }

  50% {
    transform: scaleY(1);
  }
}

/* 움직임을 원하지 않는 사람에게는 멈춘 그림을 준다. */
@media (prefers-reduced-motion: reduce) {
  .jsini-loader-bar {
    animation: none;
  }
}
</style>
