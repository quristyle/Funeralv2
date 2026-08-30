<script lang="ts" setup>
import type { ReleaseApi } from '#/api/portal/release';

import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Empty,
  message,
  Popconfirm,
  Space,
  Spin,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  getReleaseRun,
  getReleaseRuns,
  getReleaseTargets,
  triggerRelease,
} from '#/api/portal/release';

/**
 * [배포 도구]
 *
 * 예전에는 헬프데스크가 자기 시스템 배포 화면을 들고 있었고
 * 'jin114 배포' / 'goldb 배포' 두 버튼이 화면에 박혀 있었다.
 * JSini 관리 포털이 여러 MSA 를 관장하므로 이쪽으로 옮기면서
 * 배포 대상을 서버 설정(Release:Targets)에서 받아 오도록 바꿨다.
 * 대상을 늘려도 이 화면은 고치지 않는다.
 *
 * ## 가짜 진행 표시를 지웠다
 *
 * 예전 이 화면에는 `BUILD_STEPS` 라는 7단계 배열이 있었고, `setTimeout` 으로
 * 순서대로 초록색 `[SUCCESS]` 를 찍었다. **서버에서 오는 정보가 아니었다.**
 * 그래서 배포 장비에서 스크립트가 실패해도, 큐 소비자가 아예 안 떠 있어도
 * 화면은 전부 초록이었다.
 *
 * 이제 요청 한 건이 서버에 run 으로 남고, 배포 장비의 래퍼가 스크립트의 실제
 * stdout 을 되돌려 보고한다. 화면은 그 run 을 폴링한다.
 * **아래 로그에 보이는 줄은 전부 실제로 일어난 일이다.**
 *
 * 보고를 하지 않는 대상(`reportsProgress` 가 꺼진 대상)은 "요청을 보냈다" 까지만
 * 말한다. 성공했다고 하지 않는다 — 알 수 없기 때문이다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] '최근 실행' 표를 ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로
 * 옮겼다. 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — `loadHistory` 가 최근 15건을 한 번에 받아 오고
 * 폴링이 끝날 때마다 다시 읽는다. 그래서 표는 `proxyConfig` 없이
 * `:table-data` 로 그 배열을 본다.
 * ------------------------------------------------------------
 */

/** 폴링 간격. 로그가 흐르는 것이 보일 정도면 충분하다. */
const POLL_MS = 1200;

const targets = ref<ReleaseApi.Target[]>([]);
const canRelease = ref(false);
const configWarning = ref<null | string>(null);
const loadingTargets = ref(false);

/** 지금 보고 있는 실행. 화면을 새로 열어도 진행 중인 것이 있으면 여기 붙는다. */
const run = ref<null | ReleaseApi.Run>(null);
const logs = ref<ReleaseApi.RunEvent[]>([]);
const history = ref<ReleaseApi.Run[]>([]);

/** 요청을 보내는 중인 대상. 버튼 스피너에만 쓴다. */
const triggering = ref<null | string>(null);

const logBox = ref<HTMLElement | null>(null);

/** 화면을 떠나면 폴링을 끊는다. */
let disposed = false;
let timer: null | ReturnType<typeof setTimeout> = null;

onUnmounted(() => {
  disposed = true;
  if (timer) clearTimeout(timer);
});

// ── 상태를 어떻게 말하나 ──────────────────────────────────
//
// 각 상태에 정직한 문구를 붙인다. 특히 두 가지를 감추지 않는다.
//   queued      아무도 집어가지 않았다 (소비자가 안 떠 있을 수 있다)
//   dispatched  요청만 보냈고 결과는 알 수 없다

interface StatusLook {
  color: string;
  label: string;
  spinning: boolean;
  tone: 'error' | 'info' | 'success' | 'warning';
}

/**
 * 키를 `string` 으로 둔다. 상태 문자열은 서버에서 오는 값이라
 * 타입이 약속하는 것과 다를 수 있고, 그때 화면이 비어 보이면 안 된다.
 */
const STATUS: Record<string, StatusLook> = {
  dispatched: {
    color: 'orange',
    label: '요청 전송',
    spinning: false,
    tone: 'warning',
  },
  failed: { color: 'red', label: '실패', spinning: false, tone: 'error' },
  queued: { color: 'gold', label: '대기', spinning: true, tone: 'warning' },
  running: { color: 'blue', label: '진행 중', spinning: true, tone: 'info' },
  succeeded: {
    color: 'green',
    label: '완료',
    spinning: false,
    tone: 'success',
  },
  timeout: { color: 'red', label: '중단', spinning: false, tone: 'error' },
};

/** 상태가 없거나 모르는 값일 때. */
const UNKNOWN_STATUS: StatusLook = {
  color: 'default',
  label: '-',
  spinning: false,
  tone: 'info',
};

function look(status?: null | string): StatusLook {
  return (status ? STATUS[status] : undefined) ?? UNKNOWN_STATUS;
}

/** 상태 배너에 쓸 설명. 서버가 준 message 를 우선 쓰고 없으면 상태를 풀어 쓴다. */
const runDetail = computed(() => {
  const r = run.value;
  if (!r) return '';
  if (r.message) return r.message;

  switch (r.status) {
    case 'queued': {
      return '큐에 넣었습니다. 배포 장비가 집어가면 진행 상황이 이어집니다.';
    }
    case 'running': {
      return r.currentStep
        ? `배포 장비에서 실행 중입니다 — ${r.currentStep}`
        : '배포 장비에서 실행 중입니다.';
    }
    default: {
      return '';
    }
  }
});

const isBusy = computed(() => !!run.value && !run.value.isFinal);

// ── 불러오기 ──────────────────────────────────────────────

async function loadTargets(attach = false) {
  loadingTargets.value = true;
  try {
    const res = await getReleaseTargets();
    targets.value = res.items;
    canRelease.value = res.canRelease;
    configWarning.value = res.configWarning ?? null;

    // 화면에 처음 들어왔을 때 이미 돌고 있는 배포가 있으면 그것을 이어 본다.
    // 예전에는 새로 고치면 진행 상황이 사라졌다.
    if (attach) {
      const active = res.items.find((t) => t.activeRunId);
      if (active?.activeRunId) await watchRun(active.activeRunId);
    }
  } catch {
    message.error('배포 대상 목록을 불러오지 못했습니다.');
  } finally {
    loadingTargets.value = false;
  }
}

async function loadHistory() {
  try {
    history.value = await getReleaseRuns(15);
  } catch {
    // 이력을 못 읽는 것으로 화면을 막지 않는다 — 곁들이는 정보다.
  }
}

// ── 폴링 ──────────────────────────────────────────────────

/**
 * run 하나를 끝까지 따라간다.
 *
 * 받은 `lastSeq` 를 다음 요청의 `sinceSeq` 로 돌려주므로 같은 줄을 두 번 받지 않는다.
 */
async function watchRun(runId: string) {
  if (timer) clearTimeout(timer);
  logs.value = [];
  await poll(runId, 0);
}

async function poll(runId: string, sinceSeq: number) {
  if (disposed) return;

  let next: ReleaseApi.Run | undefined;
  try {
    next = await getReleaseRun(runId, sinceSeq);
  } catch {
    // 한 번 실패했다고 포기하지 않는다. 다음 차례에 다시 물어본다.
    timer = setTimeout(() => poll(runId, sinceSeq), POLL_MS * 2);
    return;
  }

  if (disposed || !next) return;

  run.value = next;
  if (next.events.length > 0) {
    logs.value.push(...next.events);
    scrollLogToEnd();
  }

  if (next.isFinal) {
    // 끝났다. 이력과 대상 목록(최근 실행·버전)을 새로 읽는다.
    await Promise.all([loadHistory(), loadTargets()]);
    return;
  }

  const seq = next.lastSeq;
  timer = setTimeout(() => poll(runId, seq), POLL_MS);
}

function scrollLogToEnd() {
  nextTick(() => {
    const el = logBox.value;
    if (el) el.scrollTop = el.scrollHeight;
  });
}

// ── 실행 ──────────────────────────────────────────────────

async function start(target: ReleaseApi.Target) {
  triggering.value = target.key;
  try {
    const result = await triggerRelease(target.key);

    if (result?.runId) {
      await watchRun(result.runId);
      message.success(result.message || `${target.name} 요청을 보냈습니다.`);
    } else {
      message.error(result?.message || '배포 요청에 실패했습니다.');
    }

    await loadHistory();
  } catch (error: any) {
    message.error(`배포 요청에 실패했습니다: ${reason(error)}`);

    // 409(이미 돌고 있다)면 그 실행을 이어 본다. 실패 봉투에는 데이터를 담을 수
    // 없어 runId 가 오지 않으므로, 대상 목록을 다시 읽어 activeRunId 로 찾는다.
    await loadTargets(true);
  } finally {
    triggering.value = null;
  }
}

/**
 * 오류에서 사람이 읽을 한 줄을 뽑는다.
 *
 * `code` 도 함께 본다. 이 저장소의 실패 응답은 `Fail("CODE", "메시지")` 로 불려
 * **두 자리가 뒤바뀐 곳이 많다** — 그런 응답에서는 사람 말이 `code` 에 들어 있다.
 * 배포 화면만 고쳐 두어도 다른 경로에서 온 오류는 여전히 뒤바뀐 채 온다.
 */
function reason(error: any) {
  const body = error?.response?.data;
  const candidates = [body?.message, body?.code, error?.message];

  // 'RELEASE_FAILED' 같은 코드 문자열은 사용자에게 보여 줄 말이 아니다.
  const human = candidates.find(
    (v) => typeof v === 'string' && v.length > 0 && !/^[A-Z][A-Z0-9_]*$/.test(v),
  );
  return human ?? '알 수 없는 오류입니다.';
}

// ── 표시 거들기 ───────────────────────────────────────────

function lineClass(level: ReleaseApi.RunEvent['level']) {
  switch (level) {
    case 'error': {
      return 'text-red-400';
    }
    case 'info': {
      return 'text-blue-400';
    }
    case 'result': {
      return 'font-semibold text-green-400';
    }
    case 'step': {
      return 'font-semibold text-cyan-300';
    }
    case 'warn': {
      return 'text-amber-300';
    }
    default: {
      return 'text-neutral-300';
    }
  }
}

/**
 * 버튼 설명. 보고를 하지 않는 대상은 그 사실을 여기서도 말한다 —
 * 버튼만 보고 "누르면 결과를 알 수 있다" 고 오해하지 않도록.
 */
function targetHint(target: ReleaseApi.Target) {
  const base = target.description ?? '';
  if (target.reportsProgress) return base || undefined;

  const note = '이 대상은 진행 보고를 하지 않습니다 — 결과는 배포 장비에서 확인해야 합니다.';
  return base ? `${base} · ${note}` : note;
}

/** 시각은 시:분:초 까지만 보여 준다. 로그 옆에 붙는 값이라 짧아야 한다. */
function clock(at?: null | string) {
  if (!at) return '';
  const d = new Date(at);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleTimeString();
}

function stamp(at?: null | string) {
  if (!at) return '-';
  const d = new Date(at);
  return Number.isNaN(d.getTime()) ? '-' : d.toLocaleString();
}

/**
 * 걸린 시간. 이력에서 "얼마나 오래 걸렸나" 를 바로 본다.
 *
 * 표의 슬롯은 행을 `Record<string, any>` 로 넘겨 주므로 필요한 세 값만 받는다.
 */
function elapsed(r: {
  finishedAt?: null | string;
  requestedAt?: null | string;
  startedAt?: null | string;
}) {
  const from = r.startedAt ?? r.requestedAt;
  if (!from || !r.finishedAt) return '-';

  const ms = new Date(r.finishedAt).getTime() - new Date(from).getTime();
  if (Number.isNaN(ms) || ms < 0) return '-';
  return ms < 1000 ? '1초 미만' : `${Math.round(ms / 1000)}초`;
}

/** 상태 칸을 고르는 칸으로 만든다. 값이 정해져 있어 손으로 칠 이유가 없다. */
const STATUS_FILTER_OPTIONS = Object.entries(STATUS).map(([value, item]) => ({
  label: item.label,
  value,
}));

const [HistoryGrid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'targetName', title: '대상', width: 130 },
      {
        field: 'status',
        params: { filterOptions: STATUS_FILTER_OPTIONS },
        slots: { default: 'status' },
        title: '결과',
        width: 110,
      },
      { field: 'requestedBy', title: '요청자', width: 110 },
      {
        field: 'requestedAt',
        minWidth: 160,
        // 화면에 보이는 글자(사람이 읽는 시각)로 걸러야 한다. 저장된 값은 ISO 문자열이다.
        params: { filterText: (row: any) => stamp(row.requestedAt) },
        slots: { default: 'requestedAt' },
        title: '요청 시각',
      },
      {
        // 저장된 값이 아니라 두 시각의 차라 `field` 가 없다.
        // `field` 없는 칸은 공통 레이어가 정렬·필터에서 알아서 뺀다.
        slots: { default: 'elapsed' },
        title: '소요',
        width: 80,
      },
      {
        field: 'deployedVersion',
        formatter: ({ cellValue }: any) => cellValue || '-',
        title: '버전',
        width: 100,
      },
    ],
    emptyText: '실행 이력이 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 폴링이 끝날 때 부르는 것과 같은 함수를 준다.
    gridFeatures: { onRefresh: () => loadHistory() },
    // 원본의 `:scroll="{ y: 240 }"` 자리다. 머리글이 두 줄이 된 만큼 조금 키웠다.
    height: 300,
    // 최근 15건을 통째로 받는다. 켜 두면 vxe 가 배열을 봉투로 읽어 0건이 된다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

onMounted(() => {
  loadTargets(true);
  loadHistory();
});
</script>

<template>
  <Page auto-content-height>
    <Card size="small">
      <div
        class="mb-3 flex flex-col gap-2 md:flex-row md:items-center md:justify-between"
      >
        <h5 class="m-0 text-xl font-bold">배포 도구</h5>
        <div v-if="run" class="flex items-center gap-2">
          <span class="text-sm text-muted-foreground">{{ run.targetName }}</span>
          <Tag :color="look(run.status).color">
            {{ look(run.status).label }}
          </Tag>
          <Spin v-if="!run.isFinal" size="small" />
        </div>
      </div>

      <!--
        설정이 반쪽일 때만 보인다 (보고를 켰는데 콜백 주소가 없는 경우).
        조용히 동작하지 않는 것보다 왜 안 되는지 말하는 편이 낫다.
      -->
      <Alert
        v-if="configWarning"
        class="mb-3"
        :description="configWarning"
        message="배포 설정 확인 필요"
        show-icon
        type="error"
      />

      <Alert
        class="mb-3"
        description="운영 서비스가 재시작됩니다. 진행 상황과 결과는 배포 장비가 실제로 보고한 내용이며, 보고를 하지 않는 대상은 '요청 전송' 까지만 표시됩니다."
        message="운영 배포"
        show-icon
        type="warning"
      />

      <div class="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <!-- ── 왼쪽: 대상과 이력 ────────────────────────── -->
        <div class="flex flex-col gap-3">
          <Spin :spinning="loadingTargets">
            <Empty
              v-if="!loadingTargets && targets.length === 0"
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
              description="등록된 배포 대상이 없습니다. 서버 설정(Release:Targets)에 추가하세요."
            />

            <Space v-else wrap>
              <Popconfirm
                v-for="target in targets"
                :key="target.key"
                v-perm:cust1
                cancel-text="취소"
                ok-text="배포"
                :title="`${target.name} 을(를) 실행합니다. 진행할까요?`"
                @confirm="start(target)"
              >
                <Tooltip :title="targetHint(target)">
                  <Button
                    :disabled="!canRelease || isBusy"
                    :loading="triggering === target.key"
                    :type="target.key === targets[0]?.key ? 'primary' : 'default'"
                  >
                    {{ target.name }}
                    <!-- 보고를 하지 않는 대상임을 버튼에서 바로 알 수 있게 한다. -->
                    <span
                      v-if="!target.reportsProgress"
                      class="ml-1 text-xs opacity-60"
                      >(보고 없음)</span>
                  </Button>
                </Tooltip>
              </Popconfirm>
            </Space>
          </Spin>

          <!--
            권한은 서버가 판정한다. v-perm 으로 버튼을 숨기더라도 이 안내를
            함께 둔다 — 버튼이 왜 없는지 알 수 있어야 한다.
          -->
          <Alert
            v-if="!loadingTargets && !canRelease"
            description="배포 실행 권한(can_cust1)이 없어 요청을 보낼 수 없습니다. 진행 상황과 이력은 볼 수 있습니다."
            message="읽기 전용"
            show-icon
            type="info"
          />

          <div>
            <h6 class="mb-2 text-base font-semibold">최근 실행</h6>
            <HistoryGrid :table-data="history">
              <template #status="{ row }">
                <Tooltip :title="row.message || undefined">
                  <Tag :color="look(row.status).color">
                    {{ look(row.status).label }}
                    <template
                      v-if="
                        row.exitCode !== null &&
                        row.exitCode !== undefined &&
                        row.exitCode !== 0
                      "
                    >
                      ({{ row.exitCode }})
                    </template>
                  </Tag>
                </Tooltip>
              </template>
              <template #requestedAt="{ row }">
                {{ stamp(row.requestedAt) }}
              </template>
              <template #elapsed="{ row }">
                {{ elapsed(row) }}
              </template>
            </HistoryGrid>
          </div>
        </div>

        <!-- ── 오른쪽: 진행 로그 ────────────────────────── -->
        <div class="flex flex-col">
          <div class="mb-2 flex items-center justify-between">
            <h6 class="m-0 text-base font-semibold">진행 로그</h6>
            <span v-if="run" class="text-xs text-muted-foreground">
              {{ logs.length }}줄
              <template v-if="run.deployedVersion">
                · 배포된 버전 v{{ run.deployedVersion }}
              </template>
            </span>
          </div>

          <!-- 상태를 한 줄로 정직하게 말한다. -->
          <Alert
            v-if="run && runDetail"
            class="mb-2"
            :description="runDetail"
            :message="`${run.targetName} — ${look(run.status).label}`"
            show-icon
            :type="look(run.status).tone"
          />

          <div
            ref="logBox"
            class="max-h-[30rem] min-h-[18rem] flex-1 overflow-y-auto rounded-sm bg-neutral-900 p-2 font-mono text-sm text-neutral-100"
          >
            <div v-if="logs.length === 0" class="text-neutral-500">
              배포를 시작하면 배포 장비가 보고하는 로그가 여기 그대로 표시됩니다.
            </div>
            <div
              v-for="line in logs"
              :key="line.seq"
              :class="lineClass(line.level)"
              class="whitespace-pre-wrap break-all"
            >
              <span class="mr-2 select-none text-neutral-600">
                {{ clock(line.at) }}
              </span>
              <span v-if="line.step" class="mr-1">[{{ line.step }}]</span>
              {{ line.message }}
            </div>
          </div>
        </div>
      </div>
    </Card>
  </Page>
</template>
