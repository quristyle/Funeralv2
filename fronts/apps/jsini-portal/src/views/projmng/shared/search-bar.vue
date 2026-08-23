<script setup lang="ts">
/**
 * 검색 줄 — 이식 전 `QuriSearchs` + 감싸던 `RadzenCard`.
 *
 * 왼쪽에 조건 입력기(기본 슬롯), 오른쪽에 버튼(actions 슬롯)을 둔다.
 * 이식 전에는 드롭다운이 모두 로드될 때까지 기다렸다가 최초 조회를 걸었는데
 * (`AllLoadComplete`), Vue 에서는 `code-select` 가 값이 정해질 때
 * `change` 를 올려 주므로 그 신호로 조회하면 된다. 별도의 대기 장치가 필요 없다.
 */
</script>

<template>
  <div
    class="border-border bg-card flex flex-wrap items-center gap-2 rounded-lg border px-3 py-2"
  >
    <div class="flex flex-1 flex-wrap items-center gap-2">
      <slot></slot>
    </div>
    <div class="flex items-center gap-2">
      <slot name="actions"></slot>
    </div>
  </div>
</template>
