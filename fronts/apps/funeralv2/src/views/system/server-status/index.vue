<script lang="ts" setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Alert, Badge, Button, Card, Spin, Statistic, Table, Tag } from 'ant-design-vue';
import { getGatewayStatus } from '#/api/gateway';
import type { GatewayApi } from '#/api/gateway';

/**
 * [서버 상태 모니터링]
 *
 * 게이트웨이가 자신이 알고 있는 모든 클러스터의 목적지를 대신 조회해 준다.
 * 브라우저가 각 서비스(:5264, :5320 ...)를 직접 호출하는 방식은
 * CORS 와 내부망 접근 문제로 불가능하므로 게이트웨이를 경유한다.
 */

const loading = ref(false);
const errorMessage = ref<string>('');
const status = ref<GatewayApi.StatusResponse | null>(null);
const lastCheckedAt = ref<string>('');

/** 자동 새로고침 주기(초). 0 이면 수동. */
const AUTO_REFRESH_SEC = 10;
let timer: ReturnType<typeof setInterval> | null = null;

/** 클러스터 ID 를 사람이 읽는 이름으로 바꾼다. */
const CLUSTER_LABEL: Record<string, string> = {
  'auth-cluster': '인증 서버 (AuthServer)',
  'funeral-cluster': '업무 API (funeralv2Api)',
  'file-cluster': '파일 서버 (FileServer)',
  'ai-cluster': 'AI 에이전트 (AIAgentServer)',
};

function clusterLabel(id: string) {
  return CLUSTER_LABEL[id] ?? id;
}

async function fetchStatus(showSpinner = false) {
  if (showSpinner) loading.value = true;
  try {
    status.value = await getGatewayStatus();
    errorMessage.value = '';
    lastCheckedAt.value = new Date().toLocaleTimeString('ko-KR');
  } catch {
    // 게이트웨이 자체가 죽으면 이 요청도 실패한다. 그것이 곧 게이트웨이 DOWN 이다.
    status.value = null;
    errorMessage.value = '게이트웨이에 연결할 수 없습니다. 게이트웨이가 중지되었거나 응답하지 않습니다.';
    lastCheckedAt.value = new Date().toLocaleTimeString('ko-KR');
  } finally {
    loading.value = false;
  }
}

const services = computed(() => status.value?.services ?? []);
const upCount = computed(() => services.value.filter((s) => s.status === 'UP').length);
const downCount = computed(() => services.value.filter((s) => s.status === 'DOWN').length);
const degradedCount = computed(() => services.value.filter((s) => s.status === 'DEGRADED').length);

const columns = [
  { title: '서비스', dataIndex: 'cluster', key: 'cluster', width: 240 },
  { title: '주소', dataIndex: 'address', key: 'address', width: 220 },
  { title: '상태', dataIndex: 'status', key: 'status', width: 120 },
  { title: '응답 시간', dataIndex: 'latencyMs', key: 'latencyMs', width: 110 },
  { title: 'HTTP', dataIndex: 'httpStatus', key: 'httpStatus', width: 90 },
  { title: '비고', dataIndex: 'error', key: 'error' },
];

onMounted(() => {
  fetchStatus(true);
  timer = setInterval(() => fetchStatus(false), AUTO_REFRESH_SEC * 1000);
});

onUnmounted(() => {
  if (timer) clearInterval(timer);
  timer = null;
});
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3">
      <!-- 요약 -->
      <div class="flex items-center gap-3">
        <Card size="small" class="flex-1">
          <Statistic title="게이트웨이" :value="errorMessage ? '중지' : '정상'">
            <template #prefix>
              <Badge :status="errorMessage ? 'error' : 'success'" />
            </template>
          </Statistic>
        </Card>
        <Card size="small" class="flex-1">
          <Statistic title="정상" :value="upCount" :value-style="{ color: '#52c41a' }" />
        </Card>
        <Card size="small" class="flex-1">
          <Statistic title="응답 이상" :value="degradedCount" :value-style="{ color: '#faad14' }" />
        </Card>
        <Card size="small" class="flex-1">
          <Statistic title="중지" :value="downCount" :value-style="{ color: '#ff4d4f' }" />
        </Card>
        <div class="flex flex-col items-end gap-1">
          <Button type="primary" :loading="loading" @click="fetchStatus(true)">
            <IconifyIcon icon="lucide:refresh-cw" class="mr-1 size-4" />
            새로고침
          </Button>
          <span class="text-xs text-muted-foreground">
            {{ lastCheckedAt ? `${lastCheckedAt} 확인` : '확인 중...' }} · {{ AUTO_REFRESH_SEC }}초마다 자동
          </span>
        </div>
      </div>

      <Alert v-if="errorMessage" type="error" show-icon :message="errorMessage" />

      <!-- 목록 -->
      <Card size="small" class="flex-1 overflow-auto" title="서비스 상태">
        <Spin :spinning="loading && !status">
          <Table
            :columns="columns"
            :data-source="services"
            :pagination="false"
            size="small"
            row-key="destination"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'cluster'">
                <div class="font-medium">{{ clusterLabel(record.cluster) }}</div>
                <div class="text-xs text-muted-foreground">{{ record.cluster }}</div>
              </template>

              <template v-else-if="column.key === 'status'">
                <Tag v-if="record.status === 'UP'" color="success">정상</Tag>
                <Tag v-else-if="record.status === 'DEGRADED'" color="warning">응답 이상</Tag>
                <Tag v-else color="error">중지</Tag>
              </template>

              <template v-else-if="column.key === 'latencyMs'">
                <span :class="record.latencyMs > 1000 ? 'text-orange-500' : ''">
                  {{ record.latencyMs }}ms
                </span>
              </template>

              <template v-else-if="column.key === 'httpStatus'">
                {{ record.httpStatus ?? '-' }}
              </template>

              <template v-else-if="column.key === 'error'">
                <span class="text-xs text-muted-foreground">{{ record.error ?? '' }}</span>
              </template>
            </template>
          </Table>
        </Spin>
      </Card>

      <div class="text-xs text-muted-foreground">
        각 서비스의 <code>/health</code> 를 게이트웨이가 3초 타임아웃으로 직접 호출한 결과입니다.
        "응답 이상"은 프로세스는 살아 있으나 <code>/health</code> 가 정상 응답을 주지 않는 상태(구버전 배포 등)입니다.
      </div>
    </div>
  </Page>
</template>
