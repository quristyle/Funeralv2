<script lang="ts" setup>
import type {
  AiFailover,
  AiModelRotation,
  AiModelSubstitution,
  AiProviderInfo,
  AiRestingModel,
} from '#/api/portal/ai/provider';
import type { GatewayApi } from '#/api/portal/gateway';

import { computed, onMounted, onUnmounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Alert, Button, Spin, Tooltip } from 'ant-design-vue';

import { getAiProviders, getFreeModels } from '#/api/portal/ai/provider';
import { deepCheckLlm, getGatewayStatus } from '#/api/portal/gateway';

/**
 * [서버 상태 모니터링]
 *
 * 게이트웨이가 자신이 알고 있는 모든 클러스터의 목적지를 대신 조회해 준다.
 * 브라우저가 각 서비스(:5264, :5320 ...)를 직접 호출하는 방식은
 * CORS 와 내부망 접근 문제로 불가능하므로 게이트웨이를 경유한다.
 *
 * [배치: 타일 + 상세 패널]
 *
 * 예전에는 서비스마다 큰 카드를 3열로 늘어놓았다. 서비스가 9개가 되자 카드가
 * 3줄이 되어 **세로 스크롤이 생겼다**(준수사항 4 위반). MSA 는 계속 늘어날 것이므로
 * "카드를 조금 줄이는" 식으로는 곧 다시 넘친다.
 *
 * 그래서 목록과 상세를 나눴다.
 *   · 왼쪽 — 한 줄짜리 **타일**. 폭에 맞춰 열 수가 자동으로 늘어난다
 *     (`auto-fill`). 서비스가 늘면 **줄이 아니라 칸이 채워진다.**
 *   · 오른쪽 — 고른 것 하나의 **상세**. 주소 · 실패 사유 · 딸린 것 · AI 공급자.
 *
 * 상세를 늘 한 개만 그리므로 서비스가 20개가 되어도 화면 높이는 그대로다.
 * 넘칠 때 스크롤되는 것은 **각 칸 안쪽**이고 페이지 자체는 스크롤되지 않는다.
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
  'projmng-cluster': {
    name: 'ProjMngServer',
    icon: 'lucide:folder-git-2',
    desc: '프로젝트관리 · WBS · 개발도구',
  },
  'site-cluster': {
    name: 'SiteServer',
    icon: 'lucide:globe',
    desc: '회사 소개 사이트 · 문의 접수',
  },
  'notification-cluster': {
    name: 'NotificationServer',
    icon: 'lucide:bell',
    desc: '푸시 · 이메일 발송',
  },
  // 우리 서비스가 아니다. 외부 고객사 시스템이라 `/health` 규약을 따르지 않고
  // 게이트웨이도 헬스체크를 걸지 않는다(appsettings 의 oadr-cluster 주석 참고).
  // 이름을 그대로 두면 우리 MSA 하나가 죽은 것처럼 읽히므로 '외부' 라고 밝힌다.
  'oadr-cluster': {
    name: 'OADR (외부)',
    icon: 'lucide:external-link',
    desc: '외부 고객사 시스템 · 헬스체크 대상 아님',
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

// ============================================================
// 딸린 것 (LLM · DB · 큐 · 저장소 …)
//
// 서비스가 스스로 점검해 /health 본문에 담아 보내고, 게이트웨이가 읽어 올려 준다.
// ============================================================

/** 점검 이름을 사람이 읽는 말로. 모르는 이름은 그대로 보여 준다. */
const DEPENDENCY_LABEL: Record<string, string> = {
  database: 'DB',
  llm: 'AI 모델 (기본 공급자)',
  'release-queue': '배포 큐',
  storage: '파일 저장소',
};

function depLabel(name: string) {
  return DEPENDENCY_LABEL[name] ?? name;
}

/**
 * 점검 상태별 색.
 *
 * `Degraded` 를 **노란색**으로 둔다 — 프로세스는 살아 있고 딸린 것이 죽은 상태다.
 * 빨강(중지)으로 칠하면 "서비스가 죽었다" 로 읽혀서 실제 상황과 다르게 전달된다.
 */
const DEP_STYLE = {
  Degraded: {
    label: '연결 안 됨',
    bg: 'bg-amber-500/5',
    dot: 'bg-amber-500',
    text: 'text-amber-600 dark:text-amber-400',
  },
  Healthy: {
    label: '연결됨',
    bg: 'bg-emerald-500/5',
    dot: 'bg-emerald-500',
    text: 'text-emerald-600 dark:text-emerald-400',
  },
  Unhealthy: {
    label: '오류',
    bg: 'bg-rose-500/5',
    dot: 'bg-rose-500',
    text: 'text-rose-600 dark:text-rose-400',
  },
} as const;

function depStyle(s: string) {
  return DEP_STYLE[s as keyof typeof DEP_STYLE] ?? DEP_STYLE.Unhealthy;
}

// ============================================================
// AI 공급자
//
// `/health` 의 `llm` 항목은 **기본 공급자 하나**만 본다. 헬스체크는 서버 전체의
// 상태를 말하는 자리라 특정 사용자의 선택을 따라갈 수 없고, 공급자를 전부 찔러
// 보게 하면 점검 한 번에 여러 곳이 붙어 느려진다. **Groq 는 그 점검만으로도
// 무료 한도를 깎는다.**
//
// 그래서 공급자 현황은 헬스체크가 아니라 AI 서비스의 `/ai/providers` 에서 받는다.
// 그 응답의 사용량은 **실제 요청이 오갈 때 지나간 응답 헤더를 서버가 주워 둔 것**이라,
// 이 화면을 열어 두는 것만으로는 한도가 줄지 않는다.
// ============================================================

const aiProviders = ref<AiProviderInfo[]>([]);
const aiFailoverEnabled = ref(false);
const aiLastFailover = ref<AiFailover | null>(null);
const aiLastSubstitution = ref<AiModelSubstitution | null>(null);

/**
 * 한도에 걸려 다른 무료 모델로 바꿔 부른 마지막 건, 그리고 지금 쉬는 모델들.
 *
 * `aiLastSubstitution`(무료가 아니어서 바꿈)과 **따로 보여 준다.** 저쪽은 설정
 * 목록을 손봐야 한다는 신호이고, 이쪽은 시간이 지나면 풀리는 혼잡이다.
 * 한 칸에 뭉개면 목록을 고쳐야 하는지 그냥 기다리면 되는지 알 수 없다.
 */
const aiLastRotation = ref<AiModelRotation | null>(null);
const aiRestingModels = ref<AiRestingModel[]>([]);

/**
 * 지금 실제로 무료인 모델 수. OpenRouter 처럼 무료 목록이 바뀌는 공급자에만 뜬다.
 *
 * 카탈로그(가격표)만 읽으므로 이 화면을 열어 두어도 AI 한도를 쓰지 않는다.
 * `null` 이면 아직 못 받았다.
 */
const aiFreeModels = ref<null | {
  count: number;
  currentModelIsFree: boolean;
  available: boolean;
}>(null);

async function fetchAiProviders() {
  try {
    const res = await getAiProviders();
    aiProviders.value = res?.providers ?? [];
    aiFailoverEnabled.value = res?.failoverEnabled ?? false;
    aiLastFailover.value = res?.lastFailover ?? null;
    aiLastSubstitution.value = res?.lastModelSubstitution ?? null;
    aiLastRotation.value = res?.lastModelRotation ?? null;
    aiRestingModels.value = res?.restingModels ?? [];

    // 무료 강제가 걸린 공급자가 있으면 그 목록 상태도 함께 받는다.
    const freeOnly = aiProviders.value.find((p) => p.requireFreeModel);
    if (freeOnly) {
      const models = await getFreeModels(freeOnly.key);
      aiFreeModels.value = {
        count: models.models.length,
        currentModelIsFree: models.currentModelIsFree,
        available: models.available,
      };
    } else {
      aiFreeModels.value = null;
    }
  } catch {
    // AI 서비스가 죽어 있으면 실패한다. 그 사실은 이미 서비스 카드가 보여 주므로
    // 여기서 따로 알리지 않고 목록만 비운다.
    aiProviders.value = [];
    aiLastFailover.value = null;
    aiLastSubstitution.value = null;
    aiLastRotation.value = null;
    aiRestingModels.value = [];
    aiFreeModels.value = null;
  }
}

/**
 * 모델 이름을 짧게. 서버가 답에 붙이는 안내와 같은 방식으로 줄인다.
 *
 * `google/gemma-4-31b-it:free` → `gemma-4-31b-it`
 */
function shortModel(model: string) {
  const trimmed = model.replace(':free', '');
  const slash = trimmed.lastIndexOf('/');
  return slash >= 0 ? trimmed.slice(slash + 1) : trimmed;
}

/** 쉬는 것이 풀리기까지 남은 시간. 지났으면 빈 문자열. */
function restRemaining(until: string) {
  const left = (new Date(until).getTime() - Date.now()) / 1000;
  if (left <= 0) return '';
  return left < 60 ? `${Math.ceil(left)}초 후` : `${Math.ceil(left / 60)}분 후`;
}

/** 공급자 키 → 사람이 읽는 이름. 자동 전환 안내에 쓴다. */
function providerName(key: string) {
  return aiProviders.value.find((p) => p.key === key)?.displayName ?? key;
}

/** `12m57.599s` 처럼 오는 값을 짧게. 그대로 보여 주면 자리를 많이 먹는다. */
function shortReset(raw: null | string) {
  if (!raw) return '';
  const m = /^(?:(\d+)m)?([\d.]+)s$/.exec(raw.trim());
  if (!m) return raw;
  const min = m[1] ? Number(m[1]) : 0;
  const sec = Math.round(Number(m[2]));
  if (min > 0) return `${min}분 ${sec}초`;
  return `${sec}초`;
}

/** 관측 시각을 "n분 전" 으로. 언제 기준인지 모르면 남은 한도를 믿을 수 없다. */
function agoText(iso: null | string) {
  if (!iso) return '';
  const diffSec = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (diffSec < 60) return '방금 기준';
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)}분 전 기준`;
  return `${Math.floor(diffSec / 3600)}시간 전 기준`;
}

function toNumber(raw: null | string) {
  const n = Number(raw);
  return Number.isFinite(n) ? n : null;
}

/** 남은 비율(0~100). 막대 길이로 쓴다. */
function remainPercent(remaining: null | string, limit: null | string) {
  const r = toNumber(remaining);
  const l = toNumber(limit);
  if (r === null || l === null || l <= 0) return null;
  return Math.max(0, Math.min(100, (r / l) * 100));
}

/** 잔량이 적으면 눈에 띄게. 20% 아래면 곧 막힌다. */
function remainClass(pct: null | number) {
  if (pct === null) return 'bg-muted-foreground/40';
  if (pct <= 10) return 'bg-rose-500';
  if (pct <= 30) return 'bg-amber-500';
  return 'bg-emerald-500';
}

/**
 * 정밀 확인 — 실제로 응답을 만들어 내는지.
 *
 * 자동 점검은 접속과 모델 목록까지만 본다. 생성까지 확인하려면 GPU 를 쓰고
 * 모델 로드에 수십 초가 걸릴 수 있어, 사람이 누를 때만 실행한다.
 *
 * **공급자별로 따로 누른다.** 로컬 장비가 꺼져 있을 때 Groq 는 멀쩡한지
 * 확인하는 것이 이 버튼의 주 용도다.
 */
const deepChecking = ref<string>('');
const deepResults = ref<
  Record<string, { message: string; ok: boolean; rateLimited: boolean }>
>({});

async function runDeepCheck(providerKey: string) {
  deepChecking.value = providerKey;
  delete deepResults.value[providerKey];
  try {
    const res = await deepCheckLlm(providerKey);
    deepResults.value = {
      ...deepResults.value,
      [providerKey]: {
        message: res.message,
        ok: res.ok,
        rateLimited: res.rateLimited,
      },
    };
    // 정밀 확인이 끝나면 서버가 자동 점검 캐시를 버린다. 새 값을 바로 받아 온다.
    // 방금 호출로 사용량도 갱신됐으므로 공급자 목록도 다시 읽는다.
    await Promise.all([fetchStatus(), fetchAiProviders()]);
  } catch {
    deepResults.value = {
      ...deepResults.value,
      [providerKey]: {
        message: 'AIAgentServer 에 연결할 수 없어 정밀 확인을 하지 못했습니다.',
        ok: false,
        rateLimited: false,
      },
    };
  } finally {
    deepChecking.value = '';
  }
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
const degradedCount = computed(
  () => services.value.filter((s) => s.status === 'DEGRADED').length,
);
const downCount = computed(() => services.value.filter((s) => s.status === 'DOWN').length);

// ── 선택 ────────────────────────────────────────────────────
//
// 새로고침이 10초마다 돌기 때문에 선택을 **키로** 붙들어 둔다. 인덱스로 잡으면
// 서비스 순서가 바뀔 때 보고 있던 상세가 다른 서비스로 바뀐다.

function keyOf(svc: GatewayApi.ServiceStatus) {
  return `${svc.cluster}::${svc.destination}`;
}

const selectedKey = ref<string>('');

const selected = computed(
  () => services.value.find((s) => keyOf(s) === selectedKey.value) ?? null,
);

/** 처음 열었을 때는 **문제 있는 것**을 먼저 보여 준다. 그것이 보러 온 이유다. */
watch(services, (list) => {
  if (list.length === 0) {
    selectedKey.value = '';
    return;
  }
  if (list.some((s) => keyOf(s) === selectedKey.value)) return;

  const worst =
    list.find((s) => s.status === 'DOWN') ??
    list.find((s) => s.status === 'DEGRADED') ??
    list[0];
  selectedKey.value = worst ? keyOf(worst) : '';
});

/** 타일에 딸린 것 상태를 점으로 요약해 보여 준다. */
function depDots(svc: GatewayApi.ServiceStatus) {
  return (svc.dependencies ?? []).map((d) => depStyle(d.status).dot);
}

const isAiSelected = computed(() => selected.value?.cluster === 'ai-cluster');

/**
 * `/health` 의 `llm` 점검. 아래 두 곳에서 쓴다.
 *
 * 이 점검은 **기본 공급자 하나**를 찔러 본 결과다. 그래서 아래 'AI 공급자' 목록의
 * 그 공급자 줄과 내용이 겹친다(모델 · 주소). 두 곳에 똑같이 늘어놓으면 자리만
 * 먹으므로, **'연결 대상' 에서는 빼고 해당 공급자 줄에 붙여** 보여 준다.
 */
const llmDep = computed(
  () => selected.value?.dependencies?.find((d) => d.name === 'llm') ?? null,
);

/** '연결 대상' 에 보여 줄 것들. AI 화면에서는 `llm` 을 뺀다(위 설명 참고). */
const visibleDependencies = computed(() => {
  const list = selected.value?.dependencies ?? [];
  return isAiSelected.value ? list.filter((d) => d.name !== 'llm') : list;
});

/**
 * 이 공급자가 `/health` 점검 대상이었는지.
 *
 * 헬스체크가 `data.provider` 에 자기가 본 공급자 키를 담아 준다.
 * 옛 버전(그 값이 없는 서버)이면 기본 공급자에 붙인다.
 */
function healthOf(p: AiProviderInfo) {
  const dep = llmDep.value;
  if (!dep) return null;
  const probed = (dep.data?.provider as string | undefined) ?? '';
  const matches = probed ? probed === p.key : p.isDefault;
  return matches ? dep : null;
}

onMounted(() => {
  fetchStatus(true);
  fetchAiProviders();
  timer = setInterval(() => {
    fetchStatus(false);
    fetchAiProviders();
  }, AUTO_REFRESH_SEC * 1000);
});

onUnmounted(() => {
  if (timer) clearInterval(timer);
  timer = null;
});
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3 overflow-hidden">
      <!-- ===== 상단: 게이트웨이 상태 + 요약 ===== -->
      <div
        class="border-border bg-card flex shrink-0 flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-2.5"
      >
        <div class="flex items-center gap-3">
          <div
            class="flex size-9 items-center justify-center rounded-lg"
            :class="gatewayUp ? 'bg-emerald-500/10' : 'bg-rose-500/10'"
          >
            <IconifyIcon
              icon="lucide:network"
              class="size-5"
              :class="gatewayUp ? 'text-emerald-500' : 'text-rose-500'"
            />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-sm font-semibold">API Gateway</span>
              <span
                class="rounded-full px-2 py-0.5 text-[11px] font-medium"
                :class="gatewayUp ? STATUS_STYLE.UP.chip : STATUS_STYLE.DOWN.chip"
              >
                {{ gatewayUp ? '정상' : '중지' }}
              </span>
              <!--
                설명은 툴팁으로 접었다. 예전에는 화면 아래에 문단으로 붙어 있어
                그만큼 세로를 먹었다 — 자주 읽는 글이 아니다.
              -->
              <Tooltip>
                <template #title>
                  각 서비스의 <code>/health</code> 를 게이트웨이가 6초 타임아웃으로
                  직접 호출한 결과입니다.
                  <b>응답 이상</b>은 프로세스는 살아 있으나 제 일을 못 하는 상태입니다 —
                  딸린 것(AI 모델 · DB · 큐 · 저장소)이 끊겼거나
                  <code>/health</code> 가 정상 응답을 주지 않는 경우(구버전 배포 등)입니다.
                  '딸린 것' 은 각 서비스가 스스로 점검해 보고한 값이며,
                  실제 응답 생성은 <b>정밀 확인</b>을 누를 때만 확인합니다.
                </template>
                <IconifyIcon
                  icon="lucide:circle-help"
                  class="text-muted-foreground size-3.5 cursor-help"
                />
              </Tooltip>
            </div>
            <div class="text-muted-foreground text-[11px]">
              모든 서비스 요청의 단일 진입점 · localhost:5265
            </div>
          </div>
        </div>

        <!-- 요약 수치 -->
        <div class="flex items-center gap-5">
          <div class="text-center">
            <div class="text-xl font-semibold leading-none text-emerald-500">
              {{ upCount }}
            </div>
            <div class="text-muted-foreground mt-0.5 text-[11px]">정상</div>
          </div>
          <div class="text-center">
            <div class="text-xl font-semibold leading-none text-amber-500">
              {{ degradedCount }}
            </div>
            <div class="text-muted-foreground mt-0.5 text-[11px]">응답 이상</div>
          </div>
          <div class="text-center">
            <div class="text-xl font-semibold leading-none text-rose-500">
              {{ downCount }}
            </div>
            <div class="text-muted-foreground mt-0.5 text-[11px]">중지</div>
          </div>

          <div class="flex flex-col items-end gap-0.5">
            <Button size="small" type="primary" :loading="loading" @click="fetchStatus(true)">
              <IconifyIcon icon="lucide:refresh-cw" class="mr-1 size-3.5" />
              새로고침
            </Button>
            <span class="text-muted-foreground text-[10px]">
              {{ lastCheckedAt || '확인 중' }} · {{ AUTO_REFRESH_SEC }}초마다
            </span>
          </div>
        </div>
      </div>

      <Alert v-if="errorMessage" type="error" show-icon :message="errorMessage" class="shrink-0" />

      <!-- ===== 본문: 타일 목록 + 상세 ===== -->
      <div
        class="grid min-h-0 flex-1 grid-cols-1 gap-3 lg:grid-cols-[minmax(0,1fr)_380px]"
      >
        <!-- 왼쪽: 타일. 서비스가 늘면 줄이 아니라 칸이 채워진다. -->
        <Spin :spinning="loading && !status" class="min-h-0" wrapper-class-name="h-full">
          <div class="h-full min-h-0 overflow-auto pr-0.5">
            <div
              class="grid gap-2"
              style="grid-template-columns: repeat(auto-fill, minmax(190px, 1fr))"
            >
              <button
                v-for="svc in services"
                :key="keyOf(svc)"
                type="button"
                class="group border-border bg-card relative overflow-hidden rounded-lg border p-2.5 pl-3.5 text-left transition-all hover:shadow-md"
                :class="
                  keyOf(svc) === selectedKey
                    ? 'ring-2 ring-primary/40 border-primary/40'
                    : 'hover:border-border/80'
                "
                @click="selectedKey = keyOf(svc)"
              >
                <!-- 좌측 상태 강조선 -->
                <span
                  class="absolute inset-y-0 left-0 w-1"
                  :class="styleOf(svc.status).accent"
                ></span>

                <div class="flex items-center gap-2">
                  <IconifyIcon
                    :icon="metaOf(svc.cluster).icon"
                    class="text-muted-foreground size-4 shrink-0"
                  />
                  <span class="truncate text-xs font-semibold">
                    {{ metaOf(svc.cluster).name }}
                  </span>
                  <span
                    class="ml-auto size-1.5 shrink-0 rounded-full"
                    :class="[
                      styleOf(svc.status).dot,
                      svc.status === 'UP' ? 'animate-pulse' : '',
                    ]"
                  ></span>
                </div>

                <div class="mt-1.5 flex items-center gap-2">
                  <span
                    class="text-[11px] font-medium"
                    :class="styleOf(svc.status).text"
                  >
                    {{ styleOf(svc.status).label }}
                  </span>
                  <span
                    class="ml-auto text-[11px] font-semibold"
                    :class="latencyClass(svc.latencyMs)"
                  >
                    {{ svc.latencyMs }}<span class="font-normal">ms</span>
                  </span>
                </div>

                <!-- 딸린 것 요약: 점 하나가 점검 하나다 -->
                <div v-if="depDots(svc).length" class="mt-1.5 flex items-center gap-1">
                  <span class="text-muted-foreground text-[10px]">연결</span>
                  <span
                    v-for="(dot, i) in depDots(svc)"
                    :key="i"
                    class="size-1.5 rounded-full"
                    :class="dot"
                  ></span>
                </div>
              </button>
            </div>

            <!-- 데이터 없음 -->
            <div
              v-if="!loading && services.length === 0 && !errorMessage"
              class="border-border text-muted-foreground rounded-xl border border-dashed py-16 text-center text-sm"
            >
              등록된 서비스가 없습니다.
            </div>
          </div>
        </Spin>

        <!-- 오른쪽: 고른 서비스 하나의 상세 -->
        <div
          v-if="selected"
          class="border-border bg-card flex min-h-0 flex-col overflow-hidden rounded-xl border"
        >
          <!-- 상세 헤더 -->
          <div class="border-border flex shrink-0 items-start gap-2.5 border-b p-3">
            <div class="bg-muted flex size-9 shrink-0 items-center justify-center rounded-lg">
              <IconifyIcon
                :icon="metaOf(selected.cluster).icon"
                class="text-muted-foreground size-5"
              />
            </div>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <span class="truncate text-sm font-semibold">
                  {{ metaOf(selected.cluster).name }}
                </span>
                <span
                  class="shrink-0 rounded-full px-2 py-0.5 text-[10px] font-medium"
                  :class="styleOf(selected.status).chip"
                >
                  {{ styleOf(selected.status).label }}
                </span>
              </div>
              <div class="text-muted-foreground truncate text-[11px]">
                {{ metaOf(selected.cluster).desc }}
              </div>
            </div>
          </div>

          <!-- 상세 본문 -->
          <div class="min-h-0 flex-1 space-y-3 overflow-auto p-3">
            <!--
              지표 · 주소 · 클러스터를 한 덩어리로 붙였다. 예전에는 큰 상자 두 개와
              별도 줄이어서 그만큼 세로를 먹었다 — 상세 패널에서 가장 아쉬운 것이 높이다.
            -->
            <div class="bg-muted/50 space-y-1 rounded-lg px-3 py-2">
              <div class="flex items-center gap-3">
                <span class="text-muted-foreground text-[11px]">응답</span>
                <span class="text-sm font-semibold" :class="latencyClass(selected.latencyMs)">
                  {{ selected.latencyMs }}<span class="text-xs font-normal">ms</span>
                </span>
                <span class="text-muted-foreground ml-auto text-[11px]">HTTP</span>
                <span class="text-sm font-semibold">{{ selected.httpStatus ?? '—' }}</span>
              </div>
              <div class="flex items-center justify-between gap-2">
                <span class="text-muted-foreground truncate font-mono text-[10px]">
                  {{ selected.address }}
                </span>
                <span
                  class="bg-muted text-muted-foreground shrink-0 rounded px-1.5 py-0.5 text-[10px]"
                >
                  {{ selected.cluster }}
                </span>
              </div>
            </div>

            <!-- 실패 사유 -->
            <div
              v-if="selected.error"
              class="flex items-start gap-1.5 rounded-lg bg-rose-500/5 px-3 py-2 text-[11px]"
              :class="styleOf(selected.status).text"
            >
              <IconifyIcon icon="lucide:alert-circle" class="mt-0.5 size-3.5 shrink-0" />
              <span>{{ selected.error }}</span>
            </div>

            <!--
              [딸린 것]

              프로세스가 살아 있는 것과 서비스가 **제 일을 하는 것**은 다르다.
              AIAgentServer 는 AI 모델에 닿지 못하면 아무 일도 못 하는데,
              예전에는 프로세스만 보고 '정상' 으로 보여 주어 오해를 만들었다.
            -->
            <div v-if="visibleDependencies.length" class="border-border space-y-1.5 border-t pt-3">
              <div class="text-muted-foreground text-[11px] font-medium">연결 대상</div>

              <div
                v-for="dep in visibleDependencies"
                :key="dep.name"
                class="rounded-lg px-2.5 py-2"
                :class="depStyle(dep.status).bg"
              >
                <div class="flex items-center gap-1.5">
                  <span
                    class="size-1.5 shrink-0 rounded-full"
                    :class="depStyle(dep.status).dot"
                  ></span>
                  <span class="text-[11px] font-medium">{{ depLabel(dep.name) }}</span>
                  <span class="text-[10px]" :class="depStyle(dep.status).text">
                    {{ depStyle(dep.status).label }}
                  </span>
                  <span
                    v-if="dep.data?.latencyMs != null"
                    class="text-muted-foreground ml-auto shrink-0 text-[10px]"
                  >
                    {{ dep.data.latencyMs }}ms
                  </span>
                </div>

                <div
                  v-if="dep.description"
                  class="text-muted-foreground mt-1 text-[11px] leading-snug"
                >
                  {{ dep.description }}
                </div>

                <div
                  v-if="dep.data?.endpoint || dep.data?.model"
                  class="text-muted-foreground mt-1 flex flex-wrap gap-x-2 font-mono text-[10px]"
                >
                  <span v-if="dep.data.endpoint">{{ dep.data.endpoint }}</span>
                  <span v-if="dep.data.model">{{ dep.data.model }}</span>
                </div>
              </div>
            </div>

            <!--
              [AI 공급자]

              `연결 대상` 의 `llm` 항목은 **기본 공급자 하나**만 본다. 사용자는
              환경설정에서 저마다 다른 공급자를 고를 수 있으므로, 고를 수 있는 것
              전부의 상태가 여기 필요하다.

              사용량은 **실제 요청이 오갈 때 지나간 응답 헤더를 서버가 주워 둔 값**이다.
              이 화면을 열어 두는 것만으로는 한도가 줄지 않는다 — 사용량을 알려고
              공급자를 찔러 보면 그 호출이 무료 한도를 깎기 때문에 그렇게 하지 않는다.
            -->
            <div v-if="isAiSelected" class="border-border space-y-2 border-t pt-3">
              <div class="flex items-center gap-1.5">
                <span class="text-muted-foreground text-[11px] font-medium">
                  AI 공급자
                </span>
                <Tooltip>
                  <template #title>
                    사용자가 환경설정 → AI 에서 고른 공급자로 처리됩니다.
                    사용량은 실제 요청이 오갈 때 공급자가 준 응답 헤더를 서버가 기록해 둔
                    값이며, 이 화면이 따로 조회하지는 않습니다(그 조회 자체가 무료 한도를
                    깎기 때문입니다).
                  </template>
                  <IconifyIcon
                    icon="lucide:circle-help"
                    class="text-muted-foreground size-3 cursor-help"
                  />
                </Tooltip>
              </div>

              <div
                v-if="aiProviders.length === 0"
                class="text-muted-foreground rounded-lg bg-muted/40 px-2.5 py-2 text-[11px]"
              >
                공급자 목록을 받지 못했습니다. AIAgentServer 가 응답하지 않거나
                구버전이 떠 있을 수 있습니다.
              </div>

              <!--
                [최근 자동 전환]

                전환이 조용히 일어나면 안 된다. 사용자는 다른 모델의 답을 받았고,
                관리자는 **로컬 장비가 꺼져 있다는 사실**을 알아야 한다.
                횟수가 쌓이면 장비를 손봐야 한다는 신호다.
              -->
              <div
                v-if="aiLastFailover"
                class="rounded-lg bg-amber-500/5 px-2.5 py-2"
              >
                <div class="flex items-center gap-1.5">
                  <IconifyIcon
                    icon="lucide:corner-down-right"
                    class="size-3 shrink-0 text-amber-600 dark:text-amber-400"
                  />
                  <span class="text-[10px] font-medium text-amber-600 dark:text-amber-400">
                    자동 전환 {{ aiLastFailover.count }}회
                  </span>
                  <span class="text-muted-foreground ml-auto shrink-0 text-[10px]">
                    {{ agoText(aiLastFailover.at).replace(' 기준', '') }}
                  </span>
                </div>
                <!-- 접속 실패에만 전환된다. 화살표 하나로 읽히게 둔다. -->
                <div class="text-muted-foreground mt-0.5 text-[10px] leading-snug">
                  {{ providerName(aiLastFailover.from) }} ↛
                  {{ providerName(aiLastFailover.to) }} (접속 실패)
                </div>
              </div>

              <!-- 꺼져 있으면 그 사실을 알려 준다. '전환 안 됨' 과 다르다. -->
              <div
                v-else-if="!aiFailoverEnabled && aiProviders.length > 0"
                class="text-muted-foreground text-[10px]"
              >
                자동 전환이 꺼져 있습니다. 고른 공급자가 응답하지 않으면 그대로 실패합니다.
              </div>

              <!--
                [모델 바꿔치기]

                고른 모델이 무료가 아니어서 **부르지 않고** 기본 모델로 돌린 경우다.
                OpenRouter 가 무료 목록을 바꾸면 정상적으로 일어난다 —
                이것이 뜨면 환경설정의 모델 목록을 손볼 때다.
              -->
              <div
                v-if="aiLastSubstitution"
                class="rounded-lg bg-amber-500/5 px-2.5 py-2"
              >
                <div class="flex items-center gap-1.5">
                  <IconifyIcon
                    icon="lucide:shield-check"
                    class="size-3 shrink-0 text-amber-600 dark:text-amber-400"
                  />
                  <span class="text-[10px] font-medium text-amber-600 dark:text-amber-400">
                    무료 아닌 모델 차단 {{ aiLastSubstitution.count }}회
                  </span>
                  <span class="text-muted-foreground ml-auto shrink-0 text-[10px]">
                    {{ agoText(aiLastSubstitution.at).replace(' 기준', '') }}
                  </span>
                </div>
                <div class="text-muted-foreground mt-0.5 font-mono text-[10px] leading-snug">
                  {{ aiLastSubstitution.from }} ↛ {{ aiLastSubstitution.to }}
                </div>
                <div class="text-muted-foreground mt-0.5 text-[10px] leading-snug">
                  {{ aiLastSubstitution.reason }}
                </div>
              </div>

              <!--
                [한도에 걸려 모델을 바꿔 부름]

                위의 '무료 아닌 모델 차단' 과 **원인이 다르다.** 저쪽은 목록을 고쳐야
                하는 신호이고, 이쪽은 그 모델이 지금 붐빈 것이라 시간이 지나면 풀린다.
                한 칸에 뭉개면 무엇을 해야 하는지 알 수 없어 따로 둔다.

                자주 뜨면 환경설정 기본 모델을 실제로 잘 답하는 것으로 바꿀 때다.
              -->
              <div
                v-if="aiLastRotation"
                class="rounded-lg bg-sky-500/5 px-2.5 py-2"
              >
                <div class="flex items-center gap-1.5">
                  <IconifyIcon
                    icon="lucide:repeat-2"
                    class="size-3 shrink-0 text-sky-600 dark:text-sky-400"
                  />
                  <span class="text-[10px] font-medium text-sky-600 dark:text-sky-400">
                    한도로 모델 전환 {{ aiLastRotation.count }}회
                  </span>
                  <span class="text-muted-foreground ml-auto shrink-0 text-[10px]">
                    {{ agoText(aiLastRotation.at).replace(' 기준', '') }}
                  </span>
                </div>
                <div class="text-muted-foreground mt-0.5 font-mono text-[10px] leading-snug">
                  {{ shortModel(aiLastRotation.from) }} →
                  {{ shortModel(aiLastRotation.to) }}
                </div>
              </div>

              <!--
                [지금 쉬는 모델]

                한도에 걸린 모델은 잠시 건너뛴다. **사용자가 고른 모델이 여기 있으면
                고른 것과 다른 모델이 답하고 있다는 뜻**이라 반드시 보여야 한다.
                그러지 않으면 "왜 고른 것과 다르게 동작하지" 를 알아낼 방법이 없다.
              -->
              <div
                v-if="aiRestingModels.length > 0"
                class="rounded-lg bg-amber-500/5 px-2.5 py-2"
              >
                <div class="flex items-center gap-1.5">
                  <IconifyIcon
                    icon="lucide:pause"
                    class="size-3 shrink-0 text-amber-600 dark:text-amber-400"
                  />
                  <span class="text-[10px] font-medium text-amber-600 dark:text-amber-400">
                    쉬는 모델 {{ aiRestingModels.length }}개
                  </span>
                </div>
                <div
                  v-for="r in aiRestingModels"
                  :key="`${r.provider}|${r.model}`"
                  class="text-muted-foreground mt-0.5 flex items-baseline gap-1.5 text-[10px] leading-snug"
                >
                  <span class="font-mono">{{ shortModel(r.model) }}</span>
                  <span class="ml-auto shrink-0">{{ restRemaining(r.until) }} 복귀</span>
                </div>
              </div>

              <div
                v-for="p in aiProviders"
                :key="p.key"
                class="border-border rounded-lg border px-2.5 py-2"
                :class="p.configured ? '' : 'bg-muted/30'"
              >
                <!-- 이름 · 기본 여부 · 설정 여부 -->
                <div class="flex items-center gap-1.5">
                  <span class="text-[11px] font-medium">{{ p.displayName }}</span>
                  <span
                    v-if="p.isDefault"
                    class="bg-primary/10 text-primary shrink-0 rounded px-1.5 py-0.5 text-[10px]"
                  >
                    기본
                  </span>
                  <span
                    class="ml-auto shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-medium"
                    :class="
                      p.configured
                        ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
                        : 'bg-muted text-muted-foreground'
                    "
                  >
                    {{ p.configured ? '사용 가능' : '미설정' }}
                  </span>
                </div>

                <div class="text-muted-foreground mt-1 font-mono text-[10px]">
                  {{ p.model || '(모델 없음)' }} · 최대 {{ p.maxTokens }} 토큰
                </div>

                <!--
                  [무료 모델만 쓴다]

                  OpenRouter 는 같은 API 로 유료 모델도 부를 수 있다. 그래서 이
                  공급자만 서버가 모델을 검사한다 — 이름이 `:free` 로 끝나고
                  카탈로그의 실제 단가가 0 이어야 통과한다. 요청 본문에도
                  `max_price=0` · `allow_fallbacks=false` 를 함께 보낸다.

                  사람이 "정말 무료만 쓰는지" 를 확인할 수 있어야 하므로 상태를 밝힌다.
                -->
                <div v-if="p.requireFreeModel" class="mt-1.5 flex flex-wrap items-center gap-1">
                  <span
                    class="rounded bg-emerald-500/10 px-1.5 py-0.5 text-[10px] font-medium text-emerald-600 dark:text-emerald-400"
                  >
                    무료 모델만
                  </span>
                  <span
                    v-if="p.allowModelChoice"
                    class="bg-muted text-muted-foreground rounded px-1.5 py-0.5 text-[10px]"
                  >
                    환경설정에서 선택
                  </span>
                  <span
                    v-if="aiFreeModels?.available"
                    class="text-muted-foreground text-[10px]"
                  >
                    · 확인된 무료 {{ aiFreeModels.count }}종
                  </span>
                  <span v-else class="text-[10px] text-amber-600 dark:text-amber-400">
                    · 목록 확인 못 함
                  </span>
                </div>

                <!--
                  설정된 기본 모델이 무료 목록에서 빠졌다면 알려야 한다.
                  이 상태에서도 과금되지는 않는다(서버가 부르지 않는다) — 다만
                  기본값을 갈아 줘야 OpenRouter 를 정상적으로 쓸 수 있다.
                -->
                <div
                  v-if="
                    p.requireFreeModel &&
                    aiFreeModels?.available &&
                    !aiFreeModels.currentModelIsFree
                  "
                  class="mt-1 rounded bg-amber-500/5 px-2 py-1 text-[10px] leading-snug text-amber-600 dark:text-amber-400"
                >
                  설정된 기본 모델이 무료 목록에 없습니다. 과금되지는 않지만
                  <code>appsettings</code> 의 Model 을 무료 모델로 바꿔야 합니다.
                </div>
                <!--
                  대기 시간 두 가지. 하나로 보이면 오해한다 —
                  '접속' 은 장비 꺼짐을 알아채는 값이고, '응답' 은 생성 예산이다.
                -->
                <div class="text-muted-foreground mt-0.5 text-[10px]">
                  대기 — 응답 {{ p.timeoutSeconds }}초<span v-if="p.connectTimeoutSeconds != null">
                    · 접속 {{ p.connectTimeoutSeconds }}초</span>
                </div>

                <!--
                  [자동 점검 결과]

                  `/health` 의 `llm` 점검은 기본 공급자 하나를 30초마다 찔러 본다
                  (접속 + 모델 목록까지. 생성은 '정밀 확인' 이 맡는다).
                  그 결과를 '연결 대상' 에 따로 두지 않고 해당 공급자 줄에 붙인다 —
                  같은 내용을 두 곳에 늘어놓으면 자리만 먹는다.
                -->
                <div
                  v-if="healthOf(p)"
                  class="mt-1.5 rounded px-2 py-1.5"
                  :class="depStyle(healthOf(p)!.status).bg"
                >
                  <div class="flex items-center gap-1.5">
                    <span
                      class="size-1.5 shrink-0 rounded-full"
                      :class="depStyle(healthOf(p)!.status).dot"
                    ></span>
                    <span class="text-[10px]" :class="depStyle(healthOf(p)!.status).text">
                      자동 점검: {{ depStyle(healthOf(p)!.status).label }}
                    </span>
                    <span
                      v-if="healthOf(p)!.data?.latencyMs != null"
                      class="text-muted-foreground ml-auto shrink-0 text-[10px]"
                    >
                      {{ healthOf(p)!.data!.latencyMs }}ms
                    </span>
                  </div>
                  <div
                    v-if="healthOf(p)!.description"
                    class="text-muted-foreground mt-0.5 text-[10px] leading-snug"
                  >
                    {{ healthOf(p)!.description }}
                  </div>
                </div>

                <!-- 키를 넣지 않았으면 왜 못 쓰는지 -->
                <div
                  v-if="!p.configured"
                  class="text-muted-foreground mt-1.5 text-[10px] leading-snug"
                >
                  API 키가 설정되지 않아 이 공급자를 골라도 호출하지 않습니다.
                  <code>appsettings.Local.json</code> 의
                  <code>AI:Providers:{{ p.key }}:ApiKey</code> 를 채우고 재기동하세요.
                </div>

                <!-- ── 사용량 ── -->
                <template v-if="p.usage">
                  <!-- 한도를 알려 주는 공급자(Groq)만 막대를 그린다 -->
                  <div v-if="p.usage.observedAt" class="mt-2 space-y-1.5">
                    <div v-if="p.usage.limitRequests">
                      <div class="flex items-center justify-between text-[10px]">
                        <span class="text-muted-foreground">남은 요청(일)</span>
                        <span class="font-medium">
                          {{ p.usage.remainingRequests }} / {{ p.usage.limitRequests }}
                        </span>
                      </div>
                      <div class="bg-muted mt-0.5 h-1 overflow-hidden rounded-full">
                        <div
                          class="h-full rounded-full transition-all"
                          :class="
                            remainClass(
                              remainPercent(
                                p.usage.remainingRequests,
                                p.usage.limitRequests,
                              ),
                            )
                          "
                          :style="{
                            width: `${
                              remainPercent(
                                p.usage.remainingRequests,
                                p.usage.limitRequests,
                              ) ?? 0
                            }%`,
                          }"
                        ></div>
                      </div>
                      <div
                        v-if="p.usage.resetRequests"
                        class="text-muted-foreground mt-0.5 text-[10px]"
                      >
                        {{ shortReset(p.usage.resetRequests) }} 후 초기화
                      </div>
                    </div>

                    <div v-if="p.usage.limitTokens">
                      <div class="flex items-center justify-between text-[10px]">
                        <span class="text-muted-foreground">남은 토큰(분)</span>
                        <span class="font-medium">
                          {{ p.usage.remainingTokens }} / {{ p.usage.limitTokens }}
                        </span>
                      </div>
                      <div class="bg-muted mt-0.5 h-1 overflow-hidden rounded-full">
                        <div
                          class="h-full rounded-full transition-all"
                          :class="
                            remainClass(
                              remainPercent(p.usage.remainingTokens, p.usage.limitTokens),
                            )
                          "
                          :style="{
                            width: `${
                              remainPercent(
                                p.usage.remainingTokens,
                                p.usage.limitTokens,
                              ) ?? 0
                            }%`,
                          }"
                        ></div>
                      </div>
                      <div
                        v-if="p.usage.resetTokens"
                        class="text-muted-foreground mt-0.5 text-[10px]"
                      >
                        {{ shortReset(p.usage.resetTokens) }} 후 초기화
                      </div>
                    </div>

                    <!-- 언제 기준의 숫자인지. 이것 없이는 위 값을 믿을 수 없다. -->
                    <div class="text-muted-foreground text-[10px]">
                      {{ agoText(p.usage.observedAt) }}
                    </div>
                  </div>

                  <!-- 호출 실적. 한도를 안 주는 공급자(로컬)는 이것만 뜬다. -->
                  <div class="text-muted-foreground mt-1.5 text-[10px]">
                    호출 성공 {{ p.usage.callsOk }} · 실패 {{ p.usage.callsFailed }}
                    <span v-if="p.usage.lastLatencyMs != null">
                      · 최근 {{ p.usage.lastLatencyMs }}ms
                    </span>
                  </div>
                </template>
                <!-- 공급자가 셋이라 줄 하나가 셋으로 늘어난다. 짧게 둔다. -->
                <div v-else class="text-muted-foreground mt-1.5 text-[10px]">
                  아직 호출 기록 없음
                </div>

                <!-- 우리 쪽 하루 상한을 켜 둔 경우에만 -->
                <div
                  v-if="p.maxRequestsPerDay > 0"
                  class="text-muted-foreground mt-1 text-[10px]"
                >
                  자체 하루 상한 {{ p.usedToday }} / {{ p.maxRequestsPerDay }}회
                </div>

                <!-- ── 정밀 확인 (공급자별) ── -->
                <div class="mt-2">
                  <Button
                    :disabled="!p.configured"
                    :loading="deepChecking === p.key"
                    size="small"
                    @click="runDeepCheck(p.key)"
                  >
                    <IconifyIcon class="mr-1 size-3" icon="lucide:activity" />
                    정밀 확인 (실제 생성)
                  </Button>
                  <div
                    v-if="deepResults[p.key]"
                    class="mt-1.5 text-[10px] leading-snug"
                    :class="
                      deepResults[p.key]!.ok
                        ? 'text-emerald-600 dark:text-emerald-400'
                        : deepResults[p.key]!.rateLimited
                          ? 'text-amber-600 dark:text-amber-400'
                          : 'text-rose-600 dark:text-rose-400'
                    "
                  >
                    {{ deepResults[p.key]!.message }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- 고른 것이 없을 때(서비스 목록이 비었을 때) -->
        <div
          v-else
          class="border-border text-muted-foreground hidden items-center justify-center rounded-xl border border-dashed p-6 text-center text-xs lg:flex"
        >
          왼쪽에서 서비스를 고르면 상세가 나옵니다.
        </div>
      </div>
    </div>
  </Page>
</template>

<style scoped>
/**
 * Spin 이 자식을 감쌀 때 만드는 두 겹(`ant-spin-nested-loading` ·
 * `ant-spin-container`)은 높이를 물려주지 않는다. 그래서 안쪽 타일 목록의
 * `h-full` 이 **내용 높이를 기준으로 계산되어** 245px 로 쪼그라들었다
 * (내용이 높이를 정하고, 그 높이를 다시 100% 로 쓰는 순환).
 *
 * 남은 공간을 다 쓰게 해야 서비스가 늘어도 타일이 칸을 채운다.
 */
:deep(.ant-spin-nested-loading),
:deep(.ant-spin-container) {
  height: 100%;
}
</style>
