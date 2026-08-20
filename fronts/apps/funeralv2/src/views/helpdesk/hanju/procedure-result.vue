<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Empty,
  Input,
  Space,
  Table,
  Tag,
} from 'ant-design-vue';

import { executeProcedure } from '#/api/helpdesk';

/**
 * [프로시저 결과]
 *
 * 원본(hanju/ProcedureResult.vue). OADR 에 등록된 프로시저와 파라미터 정의를 조회한다.
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);
const keyword = ref('');

const columns = [
  {
    dataIndex: 'ProcedureName',
    key: 'ProcedureName',
    title: '프로시저',
    width: 200,
  },
  {
    dataIndex: 'ProcDescription',
    key: 'ProcDescription',
    title: '설명',
    ellipsis: true,
  },
  {
    dataIndex: 'ParameterName',
    key: 'ParameterName',
    title: '파라미터',
    width: 150,
  },
  {
    dataIndex: 'ParamDescription',
    key: 'ParamDescription',
    title: '파라미터 설명',
    ellipsis: true,
  },
  { dataIndex: 'DataType', key: 'DataType', title: '타입', width: 110 },
  { dataIndex: 'MaxLength', key: 'MaxLength', title: '길이', width: 80 },
  { dataIndex: 'Direction', key: 'Direction', title: '방향', width: 90 },
];

const filteredRows = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  if (!kw) return rows.value;
  return rows.value.filter((r) =>
    `${r.ProcedureName} ${r.ProcDescription} ${r.ParameterName}`
      .toLowerCase()
      .includes(kw),
  );
});

async function loadData() {
  loading.value = true;
  try {
    rows.value =
      (await executeProcedure<Record<string, any>[]>('P_QURI_PROC')) ?? [];
  } catch {
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Input
            v-model:value="keyword"
            allow-clear
            placeholder="프로시저 · 파라미터 검색"
            style="width: 260px"
          />
          <span class="text-xs text-muted-foreground">
            {{ filteredRows.length }}건
          </span>
        </Space>
        <Button :loading="loading" @click="loadData">새로고침</Button>
      </div>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :data-source="filteredRows"
        :loading="loading"
        :pagination="{ pageSize: 30, showSizeChanger: true }"
        :scroll="{ x: 1000 }"
        row-key="(record) => `${record.ProcedureName}-${record.ParameterName}`"
        size="small"
      >
        <template #emptyText>
          <Empty description="등록된 프로시저가 없습니다." />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'Direction'">
            <Tag>{{ record.Direction }}</Tag>
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
