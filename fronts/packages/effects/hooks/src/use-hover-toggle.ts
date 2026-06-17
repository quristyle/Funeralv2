import type { Arrayable, MaybeElementRef } from '@vueuse/core';

import type { Ref } from 'vue';

import { computed, effectScope, onUnmounted, ref, unref, watch } from 'vue';

import { isFunction } from '@vben/utils';

import { useElementHover } from '@vueuse/core';

interface HoverDelayOptions {
  /** 마우스 진입 지연 시간 */
  enterDelay?: (() => number) | number;
  /** 마우스 이탈 지연 시간 */
  leaveDelay?: (() => number) | number;
}

const DEFAULT_LEAVE_DELAY = 500; // 마우스 이탈 지연 시간, 기본값은 500ms
const DEFAULT_ENTER_DELAY = 0; // 마우스 진입 지연 시간, 기본값은 0 (즉시 응답)

/**
 * 마우스가 요소 내부에 있는지 감지하여, 내부에 있으면 true, 그렇지 않으면 false를 반환합니다.
 * @param refElement 감지가 필요한 모든 요소. 단일 요소, 요소 배열 또는 반응형 참조가 포함된 요소 배열을 지원합니다. 마우스가 어느 한 요소 내부에라도 있으면 true를 반환합니다.
 * @param delay 상태 업데이트 지연 시간. 숫자이거나 진입/이탈 지연을 포함하는 구성 객체일 수 있습니다.
 * @returns 배열을 반환합니다. 첫 번째 요소는 마우스가 요소 내부에 있는지 여부를 나타내는 ref이고, 두 번째 요소는 enable 및 disable 메서드를 통해 리스너의 활성화/비활성화를 제어할 수 있는 컨트롤러입니다.
 */
export function useHoverToggle(
  refElement: Arrayable<MaybeElementRef> | Ref<HTMLElement[] | null>,
  delay: (() => number) | HoverDelayOptions | number = DEFAULT_LEAVE_DELAY,
) {
  // 이전 버전 API 호환
  const normalizedOptions: HoverDelayOptions =
    typeof delay === 'number' || isFunction(delay)
      ? { enterDelay: DEFAULT_ENTER_DELAY, leaveDelay: delay }
      : {
          enterDelay: DEFAULT_ENTER_DELAY,
          leaveDelay: DEFAULT_LEAVE_DELAY,
          ...delay,
        };

  const value = ref(false);
  const enterTimer = ref<ReturnType<typeof setTimeout> | undefined>();
  const leaveTimer = ref<ReturnType<typeof setTimeout> | undefined>();
  const hoverScopes = ref<ReturnType<typeof effectScope>[]>([]);

  // refElement를 계산된 속성으로 래핑하여 반응형으로 만듭니다.
  const refs = computed(() => {
    const raw = unref(refElement);
    if (raw === null) return [];
    return Array.isArray(raw) ? raw : [raw];
  });
  // 모든 hover 상태 저장
  const isHovers = ref<Array<Ref<boolean>>>([]);

  // hover 리스너 업데이트 함수
  function updateHovers() {
    // 이전 스코프를 중지하고 정리
    hoverScopes.value.forEach((scope) => scope.stop());
    hoverScopes.value = [];

    isHovers.value = refs.value.map((refEle) => {
      if (!refEle) {
        return ref(false);
      }
      const eleRef = computed(() => {
        const ele = unref(refEle);
        return ele instanceof Element ? ele : (ele?.$el as Element);
      });

      // 각 요소에 대해 독립적인 스코프 생성
      const scope = effectScope();
      const hoverRef = scope.run(() => useElementHover(eleRef)) || ref(false);
      hoverScopes.value.push(scope);

      return hoverRef;
    });
  }

  // 요소 수의 변화를 감지하여 과도한 실행 방지
  const elementsCount = computed(() => {
    const raw = unref(refElement);
    if (raw === null) return 0;
    return Array.isArray(raw) ? raw.length : 1;
  });

  // 초기 설정
  updateHovers();

  // 요소 수의 변화가 있을 때만 리스너를 다시 설정
  const stopWatcher = watch(elementsCount, updateHovers, { deep: false });

  const isOutsideAll = computed(() => isHovers.value.every((v) => !v.value));

  function clearTimers() {
    if (enterTimer.value) {
      clearTimeout(enterTimer.value);
      enterTimer.value = undefined;
    }
    if (leaveTimer.value) {
      clearTimeout(leaveTimer.value);
      leaveTimer.value = undefined;
    }
  }

  function setValueDelay(val: boolean) {
    clearTimers();

    if (val) {
      // 마우스 진입
      const enterDelay = normalizedOptions.enterDelay ?? DEFAULT_ENTER_DELAY;
      const delayTime = isFunction(enterDelay) ? enterDelay() : enterDelay;

      if (delayTime <= 0) {
        value.value = true;
      } else {
        enterTimer.value = setTimeout(() => {
          value.value = true;
          enterTimer.value = undefined;
        }, delayTime);
      }
    } else {
      // 마우스 이탈
      const leaveDelay = normalizedOptions.leaveDelay ?? DEFAULT_LEAVE_DELAY;
      const delayTime = isFunction(leaveDelay) ? leaveDelay() : leaveDelay;

      if (delayTime <= 0) {
        value.value = false;
      } else {
        leaveTimer.value = setTimeout(() => {
          value.value = false;
          leaveTimer.value = undefined;
        }, delayTime);
      }
    }
  }

  const hoverWatcher = watch(
    isOutsideAll,
    (val) => {
      setValueDelay(!val);
    },
    { immediate: true },
  );

  const controller = {
    enable() {
      hoverWatcher.resume();
    },
    disable() {
      hoverWatcher.pause();
    },
  };

  onUnmounted(() => {
    clearTimers();
    // 리스너 중지
    stopWatcher();
    // 남아 있는 모든 스코프 중지
    hoverScopes.value.forEach((scope) => scope.stop());
  });

  return [value, controller] as [typeof value, typeof controller];
}
