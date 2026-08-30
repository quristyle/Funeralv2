<script lang="ts" setup>
import type { DeployStatusApi } from '#/api/portal/system/deploy-status';

import { ref } from 'vue';

import { useVbenModal } from '@vben/common-ui';

import { Table, Tag } from 'ant-design-vue';
import dayjs from 'dayjs';

/**
 * [워크플로 실행 기록 팝업]
 *
 * 배포 현황 화면은 워크플로별 마지막 상태만 카드로 보여 준다.
 * 지난 실행들은 이 팝업에서 본다 — 팝업 데이터로 { name, runs } 를 받는다.
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

const columns = [
  { dataIndex: 'status', key: 'status', title: '상태', width: 90 },
  { dataIndex: 'sha', key: 'sha', title: '커밋', width: 90 },
  { dataIndex: 'title', key: 'title', title: '커밋 메시지', ellipsis: true },
  { dataIndex: 'event', key: 'event', title: '트리거', width: 100 },
  { dataIndex: 'startedAt', key: 'startedAt', title: '시작', width: 130 },
  { dataIndex: 'durationSec', key: 'duration', title: '소요', width: 90 },
];

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
</script>

<template>
  <Modal
    :footer="false"
    :title="`${data?.name ?? ''} 실행 기록`"
    class="w-[820px]"
  >
    <div class="px-2 pb-2">
      <Table
        :columns="columns"
        :data-source="data?.runs ?? []"
        :pagination="false"
        :scroll="{ y: 440 }"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a :href="record.htmlUrl" rel="noopener" target="_blank">
              <Tag :color="tagColor(record)">{{ tagText(record) }}</Tag>
            </a>
          </template>
          <template v-else-if="column.key === 'sha'">
            <span class="font-mono">{{ record.sha.slice(0, 7) }}</span>
          </template>
          <template v-else-if="column.key === 'startedAt'">
            {{ fmtTime(record.startedAt) }}
          </template>
          <template v-else-if="column.key === 'duration'">
            {{ fmtDuration(record.durationSec) }}
          </template>
        </template>
      </Table>
      <div class="text-muted-foreground mt-2 text-xs">
        최근 20건 범위 안의 기록이다. 전체는 상태 태그를 눌러 GitHub 에서 본다.
      </div>
    </div>
  </Modal>
</template>
