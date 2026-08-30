<script lang="ts" setup>
import { computed, watch } from 'vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';

/**
 * OADR 응답을 그대로 보여주는 표.
 * 컬럼을 지정하지 않으면 첫 행의 키에서 자동으로 만든다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 부모가 쓰는 것(props)은 그대로다 — `columns` · `rows` · `scrollY`.
 * 행 배열은 `:table-data` 로 넘긴다. 컬럼은 응답마다 달라지므로 바뀔 때마다
 * `setGridOptions` 로 다시 넣는다(그 경로로 들어온 컬럼도 공통 레이어를 거친다).
 * ------------------------------------------------------------
 */
const props = defineProps<{
  columns?: { key: string; title: string; width?: number }[];
  rows: Record<string, any>[];
  scrollY?: number;
}>();

const gridColumns = computed(() => {
  const defs =
    props.columns ??
    Object.keys(props.rows[0] ?? {}).map((key) => ({ key, title: key }));

  return defs.map((c) => ({
    field: c.key,
    // 너비를 안 주면 vxe 는 칸을 좁게 눌러 버린다.
    minWidth: 140,
    title: c.title,
    width: (c as any).width,
  }));
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: gridColumns.value,
    // 행 배열은 `:table-data` 로 간다.
    data: [],
    emptyText: '데이터가 없습니다.',
    height: props.scrollY ?? 320,
    // 전량을 한 번에 받는 표다. 켜 두면 응답을 `{ result, page }` 로 읽어 한 행도 안 나온다.
    pagerConfig: { enabled: false },
  },
});

/** 컬럼은 응답마다 달라진다. 바뀌면 그리드에 다시 넣는다. */
watch(gridColumns, (columns) => gridApi.setGridOptions({ columns }));
</script>

<template>
  <Grid :table-data="rows" />
</template>
