<script lang="ts" setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Alert, Button, Spin, Tooltip } from 'ant-design-vue';
import { getGatewayStatus } from '#/api/portal/gateway';
import type { GatewayApi } from '#/api/portal/gateway';

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

/** 자동 새로고침 주기(초) */
const AUTO_REFRESH_SEC = 10;
let timer: ReturnType<typeof setInterval> | null = null;

/** 클러스터별 표시 정보 (이름 / 아이콘 / 역할 설명) */
const CLUSTER_META: Record<string, { desc: string; icon: string; name: string }> = {
  'auth-cluster': {
    name: 'AuthServer',
    icon: 'lucide:key-round',
    desc: '인증 · JWT 발급 · 공통코드',
  },
  'funeral-cluster': {
    name: 'funeralv2Api',
    icon: 'lucide:building-2',
    desc: '업무 API · 장비 실시간 허브',
  },
  'file-cluster': {
    name: 'FileServer',
    icon: 'lucide:hard-drive',
    desc: '파일 업로드 · 변환 · 다운로드',
  },
  'ai-cluster': {
    name: 'AIAgentServer',
    icon: 'lucide:sparkles',
    desc: 'AI 에이전트 · 번역 · 추천',
  },
  'helpdesk-cluster': {
    name: 'HelpDeskServer',
    icon: 'lucide:life-buoy',
    desc: '헬프데스크 · 요청/WBS · 일정',
  },
};

function metaOf(clusterId: string) {
  return (
    CLUSTER_META[clusterId] ?? {
      name: clusterId,
      icon: 'lucide:server',
      desc: '',
    }
  );
}

/** 상태별 색상 토큰. 카드 강조선 · 점 · 뱃지에 함께 쓴다. */
const STATUS_STYLE = {
  UP: {
    label: '정상',
    accent: 'bg-emerald-500',
    dot: 'bg-emerald-500',
    ring: 'ring-emerald-500/20',
    text: 'text-emerald-600 dark:text-emerald-400',
    chip: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400',
  },
  DEGRADED: {
    label: '응답 이상',
    accent: 'bg-amber-500',
    dot: 'bg-amber-500',
    ring: 'ring-amber-500/20',
    text: 'text-amber-600 dark:text-amber-400',
    chip: 'bg-amber-500/10 text-amber-600 dark:text-amber-400',
  },
  DOWN: {
    label: '중지',
    accent: 'bg-rose-500',
    dot: 'bg-rose-500',
    ring: 'ring-rose-500/20',
    text: 'text-rose-600 dark:text-rose-400',
    chip: 'bg-rose-500/10 text-rose-600 dark:text-rose-400',
  },
} as const;

function styleOf(s: string) {
  return STATUS_STYLE[s as keyof typeof STATUS_STYLE] ?? STATUS_STYLE.DOWN;
}

/** 응답 시간이 길면 눈에 띄게 한다. */
function latencyClass(ms: number) {
  if (ms >= 1000) return 'text-rose-500';
  if (ms >= 300) return 'text-amber-500';
  return 'text-foreground';
}

async function fetchStatus(showSpinner = false) {
  if (showSpinner) loading.value = true;
  try {
    status.value = await getGatewayStatus();
    errorMessage.value = '';
  } catch {
    // 게이트웨이 자체가 죽으면 이 요청도 실패한다. 그것이 곧 게이트웨이 DOWN 이다.
    status.value = null;
    errorMessage.value =
      '게이트웨이에 연결할 수 없습니다. 게이트웨이가 중지되었거나 응답하지 않습니다.';
  } finally {
    lastCheckedAt.value = new Date().toLocaleTimeString('ko-KR');
    loading.value = false;
  }
}

const services = computed(() => status.value?.services ?? []);
const gatewayUp = computed(() => !errorMessage.value);
const upCount = computed(() => services.value.filter((s) => s.status === 'UP').length);
const degradedCount = computed(() => services.value.filter((s) => s.status === 'DEGRADED').length);
const downCount = computed(() => services.value.filter((s) => s.status === 'DOWN').length);

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
    <div class="flex h-full flex-col gap-4">
      <!-- ===== 상단: 게이트웨이 상태 + 요약 ===== -->
      <div
        class="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-border bg-card p-4"
      >
        <div class="flex items-center gap-3">
          <div
            class="flex size-11 items-center justify-center rounded-lg"
            :class="gatewayUp ? 'bg-emerald-500/10' : 'bg-rose-500/10'"
          >
            <IconifyIcon
              icon="lucide:network"
              class="size-6"
              :class="gatewayUp ? 'text-emerald-500' : 'text-rose-500'"
            />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-base font-semibold">API Gateway</span>
              <span
                class="rounded-full px-2 py-0.5 text-xs font-medium"
                :class="gatewayUp ? STATUS_STYLE.UP.chip : STATUS_STYLE.DOWN.chip"
              >
                {{ gatewayUp ? '정상' : '중지' }}
              </span>
            </div>
            <div class="text-xs text-muted-foreground">
              모든 서비스 요청의 단일 진입점 · localhost:5265
            </div>
          </div>
        </div>

        <!-- 요약 수치 -->
        <div class="flex items-center gap-6">
          <div class="text-center">
            <div class="text-2xl font-semibold text-emerald-500">{{ upCount }}</div>
            <div class="text-xs text-muted-foreground">정상</div>
          </div>
          <div class="text-center">
            <div class="text-2xl font-semibold text-amber-500">{{ degradedCount }}</div>
            <div class="text-xs text-muted-foreground">응답 이상</div>
          </div>
          <div class="text-center">
            <div class="text-2xl font-semibold text-rose-500">{{ downCount }}</div>
            <div class="text-xs text-muted-foreground">중지</div>
          </div>

          <div class="ml-2 flex flex-col items-end gap-1">
            <Button type="primary" :loading="loading" @click="fetchStatus(true)">
              <IconifyIcon icon="lucide:refresh-cw" class="mr-1 size-4" />
              새로고침
            </Button>
            <span class="text-[11px] text-muted-foreground">
              {{ lastCheckedAt || '확인 중' }} · {{ AUTO_REFRESH_SEC }}초마다 자동
            </span>
          </div>
        </div>
      </div>

      <Alert v-if="errorMessage" type="error" show-icon :message="errorMessage" />

      <!-- ===== 서비스 카드 ===== -->
      <Spin :spinning="loading && !status">
        <div class="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          <div
            v-for="svc in services"
            :key="svc.destination + svc.cluster"
            class="group relative overflow-hidden rounded-xl border border-border bg-card p-4 ring-1 ring-transparent transition-all hover:-translate-y-0.5 hover:shadow-lg"
            :class="styleOf(svc.status).ring"
          >
            <!-- 좌측 상태 강조선 -->
            <span
              class="absolute inset-y-0 left-0 w-1"
              :class="styleOf(svc.status).accent"
            ></span>

            <!-- 헤더: 아이콘 + 이름 + 상태칩 -->
            <div class="flex items-start justify-between gap-2 pl-2">
              <div class="flex items-center gap-3">
                <div class="flex size-10 items-center justify-center rounded-lg bg-muted">
                  <IconifyIcon
                    :icon="metaOf(svc.cluster).icon"
                    class="size-5 text-muted-foreground"
                  />
                </div>
                <div class="min-w-0">
                  <div class="truncate text-sm font-semibold">
                    {{ metaOf(svc.cluster).name }}
                  </div>
                  <div class="truncate text-xs text-muted-foreground">
                    {{ metaOf(svc.cluster).desc }}
                  </div>
                </div>
              </div>

              <span
                class="flex shrink-0 items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium"
                :class="styleOf(svc.status).chip"
              >
                <span
                  class="size-1.5 rounded-full"
                  :class="[styleOf(svc.status).dot, svc.status === 'UP' ? 'animate-pulse' : '']"
                ></span>
                {{ styleOf(svc.status).label }}
              </span>
            </div>

            <!-- 지표 -->
            <div class="mt-4 grid grid-cols-2 gap-2 pl-2">
              <div class="rounded-lg bg-muted/50 px-3 py-2">
                <div class="text-[11px] text-muted-foreground">응답 시간</div>
                <div class="text-lg font-semibold" :class="latencyClass(svc.latencyMs)">
                  {{ svc.latencyMs }}<span class="ml-0.5 text-xs font-normal">ms</span>
                </div>
              </div>
              <div class="rounded-lg bg-muted/50 px-3 py-2">
                <div class="text-[11px] text-muted-foreground">HTTP</div>
                <div class="text-lg font-semibold">
                  {{ svc.httpStatus ?? '—' }}
                </div>
              </div>
            </div>

            <!-- 주소 · 클러스터 -->
            <div class="mt-3 flex items-center justify-between gap-2 pl-2">
              <span class="truncate font-mono text-[11px] text-muted-foreground">
                {{ svc.address }}
              </span>
              <span class="shrink-0 rounded bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground">
                {{ svc.cluster }}
              </span>
            </div>

            <!-- 실패 사유 -->
            <Tooltip v-if="svc.error" :title="svc.error">
              <div
                class="mt-3 flex items-start gap-1.5 rounded-lg bg-rose-500/5 px-3 py-2 pl-2 text-[11px]"
                :class="styleOf(svc.status).text"
              >
                <IconifyIcon icon="lucide:alert-circle" class="mt-0.5 size-3.5 shrink-0" />
                <span class="line-clamp-2">{{ svc.error }}</span>
              </div>
            </Tooltip>
          </div>
        </div>

        <!-- 데이터 없음 -->
        <div
          v-if="!loading && services.length === 0 && !errorMessage"
          class="rounded-xl border border-dashed border-border py-16 text-center text-sm text-muted-foreground"
        >
          등록된 서비스가 없습니다.
        </div>
      </Spin>

      <div class="text-xs text-muted-foreground">
        각 서비스의 <code>/health</code> 를 게이트웨이가 3초 타임아웃으로 직접 호출한 결과입니다.
        <span class="text-amber-500">응답 이상</span>은 프로세스는 살아 있으나
        <code>/health</code> 가 정상 응답을 주지 않는 상태(구버전 배포 등)를 뜻합니다.
      </div>
    </div>
  </Page>
</template>
