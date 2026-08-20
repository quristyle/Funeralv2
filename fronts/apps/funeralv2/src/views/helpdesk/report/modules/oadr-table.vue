<script lang="ts" setup>
import { computed } from 'vue';

import { Empty, Table } from 'ant-design-vue';

/**
 * OADR 응답을 그대로 보여주는 표.
 * 컬럼을 지정하지 않으면 첫 행의 키에서 자동으로 만든다.
 */
const props = defineProps<{
  columns?: { key: string; title: string; width?: number }[];
  rows: Record<string, any>[];
  scrollY?: number;
}>();

const tableColumns = computed(() => {
  const defs =
    props.columns ??
    Object.keys(props.rows[0] ?? {}).map((key) => ({ key, title: key }));

  return defs.map((c) => ({
    dataIndex: c.key,
    ellipsis: true,
    key: c.key,
    title: c.title,
    width: (c as any).width,
  }));
});
</script>

<template>
  <Table
    :columns="tableColumns"
    :data-source="rows"
    :pagination="false"
    :scroll="{ x: true, y: scrollY ?? 320 }"
    row-key="__index"
    size="small"
  >
    <template #emptyText>
      <Empty description="데이터가 없습니다." />
    </template>
  </Table>
</template>
