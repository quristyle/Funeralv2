<script lang="ts" setup>
import type { DeployStatusApi } from '#/api/portal/system/deploy-status';

import { computed, onMounted, onUnmounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  message,
  Popconfirm,
  Spin,
  Switch,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';
import dayjs from 'dayjs';

import {
  cleanupDockerImages,
  getDeployStatus,
} from '#/api/portal/system/deploy-status';

/**
 * 상태관리 > 배포 현황.
 *
 * git push 로 도는 자동배포의 "지금 상태"를 한 화면에 모은다 —
 * GitHub Actions 실행 이력 · self-hosted 러너 · 운영서버 컨테이너(이미지 태그).
 * 재현 절차와 구조는 docs/analysis/39-jsini-deploy-setup-guide.md 에 있다.
 */

const loading = ref(false);
const data = ref<DeployStatusApi.Overview | null>(null);
const errorMsg = ref('');
const autoRefresh = ref(true);
let timer: null | ReturnType<typeof setInterval> = null;

async function fetchData(spin = false) {
  if (spin) loading.value = true;
  try {
    data.value = await getDeployStatus();
    errorMsg.value = '';
  } catch (error: any) {
    errorMsg.value = error?.message ?? '배포 현황을 불러오지 못했습니다.';
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  fetchData(true);
  timer = setInterval(() => autoRefresh.value && fetchData(), 30_000);
});
onUnmounted(() => timer && clearInterval(timer));

// ── 이미지 정리 ─────────────────────────────────────────

const cleaning = ref(false);

/** 배포가 거듭될수록 옛 태그 이미지가 쌓인다 — 사용 중 + 최근 2개만 남기고 지운다. */
async function runCleanup() {
  cleaning.value = true;
  try {
    const r = await cleanupDockerImages();
    message.success(
      `이미지 ${r.removed.length}개 정리, ${r.spaceReclaimedMb}MB 회수` +
        (r.errors.length > 0 ? ` (실패 ${r.errors.length}건)` : ''),
    );
    await fetchData();
  } catch (error: any) {
    message.error(error?.message ?? '정리에 실패했습니다.');
  } finally {
    cleaning.value = false;
  }
}

const staleImageCount = computed(
  () => (data.value?.docker.images ?? []).filter((i) => !i.inUse).length,
);

// ── 요약 ────────────────────────────────────────────────

const containers = computed(() => data.value?.docker.containers ?? []);
const runningCount = computed(
  () => containers.value.filter((c) => c.state === 'running').length,
);

/** 배포된 태그 — gateway 컨테이너의 이미지 태그가 정본이다. */
const deployedTag = computed(() => {
  const gw = containers.value.find((c) => c.service === 'gateway');
  return gw?.tag ?? '';
});

const lastBackendDeploy = computed(() =>
  data.value?.github.runs.find(
    (r) => r.name === 'deploy-backend' && r.conclusion === 'success',
  ),
);

const runner = computed(() => data.value?.github.runners[0] ?? null);

// ── 표 ──────────────────────────────────────────────────

const runColumns = [
  { dataIndex: 'name', key: 'name', title: '워크플로', width: 130 },
  { dataIndex: 'status', key: 'status', title: '상태', width: 90 },
  { dataIndex: 'sha', key: 'sha', title: '커밋', width: 90 },
  { dataIndex: 'event', key: 'event', title: '트리거', width: 110 },
  { dataIndex: 'startedAt', key: 'startedAt', title: '시작', width: 130 },
  { dataIndex: 'durationSec', key: 'duration', title: '소요', width: 70 },
];

const containerColumns = [
  { dataIndex: 'service', key: 'service', title: '서비스', width: 90 },
  { dataIndex: 'state', key: 'state', title: '상태', width: 90 },
  { dataIndex: 'tag', key: 'tag', title: '이미지 태그', width: 110 },
  { dataIndex: 'status', key: 'status', title: '가동' },
];

function runTagColor(run: DeployStatusApi.WorkflowRun) {
  if (run.status !== 'completed') return 'processing';
  if (run.conclusion === 'success') return 'success';
  if (run.conclusion === 'failure') return 'error';
  return 'default';
}

function runTagText(run: DeployStatusApi.WorkflowRun) {
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
  <Page
    description="git push 가 만드는 빌드·배포 이력과 운영서버 컨테이너 상태를 한눈에 본다."
    title="배포 현황"
  >
    <template #extra>
      <div class="flex items-center gap-3">
        <span v-if="data" class="text-muted-foreground text-xs">
          {{ fmtTime(data.generatedAt) }} 기준
        </span>
        <Tooltip title="30초마다 자동 새로고침">
          <Switch v-model:checked="autoRefresh" size="small" />
        </Tooltip>
        <Button :loading="loading" size="small" @click="fetchData(true)">
          새로고침
        </Button>
      </div>
    </template>

    <Spin :spinning="loading && !data">
      <div v-if="errorMsg" class="text-destructive mb-3 text-sm">
        {{ errorMsg }}
      </div>

      <!-- 요약 카드 -->
      <div class="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Card size="small">
          <div class="text-muted-foreground text-xs">배포된 커밋</div>
          <div class="mt-1 font-mono text-lg">
            <a
              v-if="deployedTag"
              :href="`https://github.com/${data?.repo}/commit/${deployedTag}`"
              rel="noopener"
              target="_blank"
            >
              {{ deployedTag.slice(0, 7) }}
            </a>
            <span v-else>-</span>
          </div>
        </Card>
        <Card size="small">
          <div class="text-muted-foreground text-xs">컨테이너</div>
          <div class="mt-1 text-lg">
            <template v-if="data?.docker.available">
              {{ runningCount }} / {{ containers.length }} 실행 중
            </template>
            <Tooltip v-else :title="data?.docker.error ?? '개발 환경에서는 비활성'">
              <span class="text-muted-foreground">조회 불가</span>
            </Tooltip>
          </div>
        </Card>
        <Card size="small">
          <div class="text-muted-foreground text-xs">배포 러너</div>
          <div class="mt-1 text-lg">
            <Tag v-if="runner" :color="runner.status === 'online' ? 'success' : 'error'">
              {{ runner.name }} · {{ runner.status === 'online' ? (runner.busy ? '작업 중' : '대기') : '오프라인' }}
            </Tag>
            <span v-else>-</span>
          </div>
        </Card>
        <Card size="small">
          <div class="text-muted-foreground text-xs">마지막 백엔드 배포</div>
          <div class="mt-1 text-lg">{{ fmtTime(lastBackendDeploy?.updatedAt ?? null) }}</div>
        </Card>
      </div>

      <div class="grid grid-cols-1 gap-3 xl:grid-cols-5">
        <!-- 최근 실행 -->
        <Card class="xl:col-span-3" size="small" title="최근 빌드·배포 실행">
          <template #extra>
            <a
              :href="`https://github.com/${data?.repo}/actions`"
              rel="noopener"
              target="_blank"
            >
              GitHub Actions
            </a>
          </template>
          <div v-if="data?.github.error" class="text-destructive mb-2 text-xs">
            GitHub 조회 실패: {{ data.github.error }}
          </div>
          <Table
            :columns="runColumns"
            :data-source="data?.github.runs ?? []"
            :pagination="false"
            :scroll="{ y: 420 }"
            row-key="id"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'name'">
                <a :href="record.htmlUrl" rel="noopener" target="_blank">
                  {{ record.name }}
                </a>
              </template>
              <template v-else-if="column.key === 'status'">
                <Tag :color="runTagColor(record)">{{ runTagText(record) }}</Tag>
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
        </Card>

        <!-- 운영 컨테이너 -->
        <Card class="xl:col-span-2" size="small" title="운영서버 컨테이너">
          <template #extra>
            <div v-if="data?.docker.available" class="flex items-center gap-2">
              <span class="text-muted-foreground text-xs">
                이미지 {{ data.docker.images.length }}개 ·
                {{ (data.docker.imagesTotalMb / 1024).toFixed(1) }}GB
              </span>
              <Popconfirm
                cancel-text="취소"
                ok-text="정리"
                title="저장소별로 사용 중 + 최근 2개 태그만 남기고 지웁니다. 롤백 여지는 유지됩니다."
                @confirm="runCleanup"
              >
                <Button
                  :disabled="staleImageCount === 0"
                  :loading="cleaning"
                  danger
                  size="small"
                >
                  오래된 이미지 정리
                </Button>
              </Popconfirm>
            </div>
          </template>
          <div v-if="!data?.docker.available" class="text-muted-foreground py-6 text-center text-sm">
            운영서버에서만 조회된다 —
            {{ data?.docker.error ?? '개발 환경에서는 Docker 소켓이 없다.' }}
          </div>
          <Table
            v-else
            :columns="containerColumns"
            :data-source="containers"
            :pagination="false"
            row-key="service"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'state'">
                <Tag :color="record.state === 'running' ? 'success' : 'error'">
                  {{ record.state === 'running' ? '실행 중' : record.state }}
                </Tag>
              </template>
              <template v-else-if="column.key === 'tag'">
                <Tooltip :title="record.image">
                  <span class="font-mono">{{ record.tag.slice(0, 7) }}</span>
                </Tooltip>
              </template>
            </template>
          </Table>
        </Card>
      </div>
    </Spin>
  </Page>
</template>
