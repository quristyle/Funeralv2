<script lang="ts" setup>
/**
 * 그리드 도구줄에 있는 것과 같은 동그란 아이콘 단추.
 *
 * vxe 그리드가 도구줄에 그리는 단추(내려받기 · 새로고침 · 전체화면 · 설정)와
 * **같은 클래스**를 쓴다. 그래서 그리드가 없는 화면(카드 머리 등)에 두어도
 * 다른 화면과 모양이 어긋나지 않는다.
 *
 * ```html
 * <button class="vxe-button type--button size--small is--circle" title="Refresh">
 *   <i class="vxe-button--item vxe-button--prefix-icon vxe-table-icon-repeat"></i>
 * </button>
 * ```
 *
 * **왜 `VxeButton` 부품을 쓰지 않는가.** 그 부품은 `vxe-pc-ui` 에 있는데,
 * 그것은 `@vben/plugins` 의 의존성이라 앱의 node_modules 에서 해결되지 않는다.
 * 공유 패키지(`fronts/packages`)는 vben 상위와 맞춰 두는 곳이라
 * 이런 편의 때문에 손대지 않는다. 클래스만 빌려 쓴다 — 스타일시트는 이미 올라와 있다.
 *
 * 아이콘 이름은 vxe 것을 그대로 쓴다.
 * `vxe-table-icon-repeat`(새로고침) · `vxe-table-icon-download`(내려받기) ·
 * `vxe-table-icon-fullscreen` · `vxe-table-icon-setting` 등.
 */
interface Props {
  /** vxe 아이콘 클래스 이름 (예: `vxe-table-icon-repeat`) */
  icon: string;
  /** 마우스를 올렸을 때 나오는 설명. 아이콘만 남으므로 **반드시 준다.** */
  title: string;
  /** 도는 아이콘으로 바꿔 진행 중임을 보인다 */
  loading?: boolean;
  disabled?: boolean;
}

withDefaults(defineProps<Props>(), {
  loading: false,
  disabled: false,
});

defineEmits<{ click: [MouseEvent] }>();
</script>

<template>
  <button
    type="button"
    class="vxe-button type--button size--small is--circle"
    :class="{ 'is--disabled': disabled || loading }"
    :title="title"
    :disabled="disabled || loading"
    :aria-label="title"
    @click="$emit('click', $event)"
  >
    <i
      class="vxe-button--item vxe-button--prefix-icon"
      :class="loading ? 'vxe-icon-spinner roll' : icon"
    ></i>
  </button>
</template>
