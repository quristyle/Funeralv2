<script lang="ts" setup>
import type { DeployStatusApi } from '#/api/portal/system/deploy-status';

import { ref } from 'vue';

import { useVbenModal } from '@vben/common-ui';

import { Tag } from 'ant-design-vue';
import dayjs from 'dayjs';

import { useVbenVxeGrid } from '#/adapter/vxe-table';

/**
 * [워크플로 실행 기록 팝업]
 *
 * 배포 현황 화면은 워크플로별 마지막 상태만 카드로 보여 준다.
 * 지난 실행들은 이 팝업에서 본다 — 팝업 데이터로 { name, runs } 를 받는다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 부모가 이미 받아 둔 배열을 팝업 데이터로 받는다.
 * 팝업 안이라 `page-fill-last` 가 없으므로 높이를 숫자로 준다(원본의 `scroll.y`).
 * ------------------------------------------------------------
 */

interface HistoryData {
  name: string;
  runs: DeployStatusApi.WorkflowRun[];
}

const data = ref<HistoryData | null>(null);

const [Modal, modalApi] = useVbenModal<HistoryData>({
  destroyOnClose: true,
  onOpenChange(isOpen) {
    if (isOpen) data.value = modalApi.getData() ?? null;
  },
});

function tagColor(run: DeployStatusApi.WorkflowRun) {
  if (run.status !== 'completed') return 'processing';
  if (run.conclusion === 'success') return 'success';
  if (run.conclusion === 'failure') return 'error';
  return 'default';
}

function tagText(run: DeployStatusApi.WorkflowRun) {
  if (run.status !== 'completed') return '진행 중';
  if (run.conclusion === 'success') return '성공';
  if (run.conclusion === 'failure') return '실패';
  return run.conclusion ?? run.status;
}

function fmtTime(v: null | string) {
  return v ? dayjs(v).format('MM-DD HH:mm') : '-';
}

function fmtDuration(sec: null | number) {
  if (sec === null || sec === undefined) return '-';
  const m = Math.floor(sec / 60);
  const s = Math.round(sec % 60);
  return m > 0 ? `${m}분 ${s}초` : `${s}초`;
}

const [Grid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'status',
        // 보이는 글자는 status 하나가 아니라 status + conclusion 을 합친 것이라
        // 고르는 칸(filterOptions)으로는 성공/실패를 가를 수 없다 — 글자를 훑게 한다.
        params: { filterText: (row: any) => tagText(row) },
        slots: { default: 'status' },
        title: '상태',
        width: 90,
      },
      {
        field: 'sha',
        // 화면에는 앞 7자만 보이지만 필터는 커밋 해시 전체를 훑는다.
        params: { filterText: (row: any) => row.sha ?? '' },
        slots: { default: 'sha' },
        title: '커밋',
        width: 90,
      },
      { field: 'title', minWidth: 220, title: '커밋 메시지' },
      { field: 'event', title: '트리거', width: 100 },
      {
        field: 'startedAt',
        params: { filterText: (row: any) => fmtTime(row.startedAt) },
        slots: { default: 'startedAt' },
        title: '시작',
        width: 130,
      },
      {
        field: 'durationSec',
        params: { filterText: (row: any) => fmtDuration(row.durationSec) },
        slots: { default: 'duration' },
        title: '소요',
        width: 90,
      },
    ],
    emptyText: '실행 기록이 없습니다.',
    height: 440,
    // 부모가 받아 둔 배열을 그대로 받는다 — 페이저를 켜 두면 안 된다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  },
});
</script>

<template>
  <Modal
    :footer="false"
    :title="`${data?.name ?? ''} 실행 기록`"
    class="w-[820px]"
  >
    <div class="px-2 pb-2">
      <Grid :table-data="data?.runs ?? []">
        <template #status="{ row }">
          <a :href="row.htmlUrl" rel="noopener" target="_blank">
            <Tag :color="tagColor(row)">{{ tagText(row) }}</Tag>
          </a>
        </template>
        <template #sha="{ row }">
          <span class="font-mono">{{ row.sha.slice(0, 7) }}</span>
        </template>
        <template #startedAt="{ row }">{{ fmtTime(row.startedAt) }}</template>
        <template #duration="{ row }">
          {{ fmtDuration(row.durationSec) }}
        </template>
      </Grid>
      <div class="text-muted-foreground mt-2 text-xs">
        최근 20건 범위 안의 기록이다. 전체는 상태 태그를 눌러 GitHub 에서 본다.
      </div>
    </div>
  </Modal>
</template>
