<script lang="ts" setup>
import type { ReleaseApi } from '#/api/portal/release';

import { onMounted, onUnmounted, ref } from 'vue';

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

import { getReleaseTargets, triggerRelease } from '#/api/portal/release';

/**
 * [배포 도구]
 *
 * 예전에는 헬프데스크가 자기 시스템 배포 화면을 들고 있었고
 * 'jin114 배포' / 'goldb 배포' 두 버튼이 화면에 박혀 있었다.
 * JSini 관리 포털이 여러 MSA 를 관장하므로 이쪽으로 옮기면서
 * 배포 대상을 서버 설정(Release:Targets)에서 받아 오도록 바꿨다.
 * 대상을 늘려도 이 화면은 고치지 않는다.
 *
 * 서버는 "이 스크립트를 돌려 달라"는 메시지를 큐에 넣기만 한다.
 * 실제 실행은 배포 장비의 큐 소비자가 맡으므로 진행률을 알 수 없다.
 * 아래 진행 표시는 실제 진행률이 아니라 스크립트가 거치는 단계를 순서대로 보여 주는 것이다.
 */

interface LogLine {
  message: string;
  status: 'error' | 'info' | 'success';
}

/** 배포 스크립트가 거치는 단계. 실제 진행률이 아니라 안내용이다. */
const BUILD_STEPS = [
  'source get',
  'source checking',
  'front build',
  'front publish',
  'front checking',
  'backend build',
  'backend restart',
];

const targets = ref<ReleaseApi.Target[]>([]);
const loadingTargets = ref(false);
const running = ref(false);
/** 지금 배포 중인 대상 */
const runningKey = ref<null | string>(null);
const buildLog = ref<LogLine[]>([]);
const releaseVersion = ref('');

/** 화면을 떠나면 남은 단계 타이머를 끊는다. */
let disposed = false;
onUnmounted(() => {
  disposed = true;
});

async function loadTargets() {
  loadingTargets.value = true;
  try {
    targets.value = await getReleaseTargets();
  } catch {
    message.error('배포 대상 목록을 불러오지 못했습니다.');
  } finally {
    loadingTargets.value = false;
  }
}

/**
 * 배포된 버전을 읽는다. 배포 스크립트가 서버에 떨어뜨리는 `public/version.json` 규약이다.
 * 그 파일이 없는 환경에서는 빌드 시 박아 둔 앱 버전으로 대신한다.
 */
async function loadVersion() {
  try {
    const res = await fetch(`/version.json?_=${Date.now()}`);
    const data = await res.json();
    releaseVersion.value = data.version ?? 'N/A';
  } catch {
    releaseVersion.value = import.meta.env.VITE_APP_VERSION ?? 'N/A';
  }
}

function appendLog(status: LogLine['status'], text: string) {
  buildLog.value.push({ message: text, status });
}

const wait = (ms: number) =>
  new Promise((resolve) => {
    setTimeout(resolve, ms);
  });

async function run(target: ReleaseApi.Target) {
  buildLog.value = [];
  running.value = true;
  runningKey.value = target.key;

  try {
    // 요청은 먼저 보낸다. 서버는 큐에 넣고 바로 응답한다.
    const request = triggerRelease(target.key);

    // 응답을 기다리는 동안 어떤 단계를 거치는지 알려 준다.
    const per = Math.max(
      600,
      Math.round((target.estimatedSeconds * 1000) / BUILD_STEPS.length),
    );
    for (const step of BUILD_STEPS) {
      if (disposed) return;
      appendLog('info', `[INFO] Starting: ${step}...`);
      await wait(per);
      if (disposed) return;
      appendLog('success', `[SUCCESS] Completed: ${step}`);
    }

    const result = (await request) as any;
    const detail = result?.message ?? result?.result?.message ?? '';
    appendLog('success', `[QUEUED] ${detail || '배포 요청을 보냈습니다.'}`);
    message.success(detail || `${target.name} 요청을 보냈습니다.`);
    await loadVersion();
  } catch (error) {
    const detail = (error as Error).message ?? '알 수 없는 오류';
    appendLog('error', `[ERROR] ${detail}`);
    message.error(`배포 요청에 실패했습니다: ${detail}`);
  } finally {
    running.value = false;
    runningKey.value = null;
  }
}

/** 버전만 다시 읽어 알려 준다. */
async function reloadVersion() {
  await loadVersion();
  message.info(`현재 버전은 v${releaseVersion.value} 입니다.`);
}

function lineClass(status: LogLine['status']) {
  if (status === 'success') return 'text-green-400';
  if (status === 'error') return 'text-red-400';
  return 'text-blue-400';
}

onMounted(() => {
  loadTargets();
  loadVersion();
});
</script>

<template>
  <Page auto-content-height>
    <Card size="small">
      <!-- 제목 + 현재 버전 -->
      <div
        class="mb-3 flex flex-col gap-2 md:flex-row md:items-center md:justify-between"
      >
        <h5 class="m-0 text-xl font-bold">배포 도구</h5>
        <div class="flex items-center gap-2">
          <span class="text-lg font-medium text-muted-foreground">
            현재 버전:
          </span>
          <Tag color="success">
            {{ releaseVersion ? `v${releaseVersion}` : 'Loading...' }}
          </Tag>
          <Tooltip placement="bottom" title="버전 새로고침">
            <Button shape="circle" size="small" type="text" @click="reloadVersion">
              ⟳
            </Button>
          </Tooltip>
        </div>
      </div>

      <Alert
        class="mb-3"
        description="배포 스크립트 실행 요청을 메시지 큐에 넣습니다. 운영 서비스가 재시작되니 확인 후 실행하세요. 아래 진행 과정은 서버가 알려 주는 실제 진행률이 아니라 스크립트가 거치는 단계를 순서대로 보여 주는 것입니다."
        message="운영 배포"
        show-icon
        type="warning"
      />

      <!-- 배포 대상. 서버 설정에서 받아 온다. -->
      <Spin :spinning="loadingTargets">
        <Empty
          v-if="!loadingTargets && targets.length === 0"
          :image="Empty.PRESENTED_IMAGE_SIMPLE"
          description="등록된 배포 대상이 없습니다. 서버 설정(Release:Targets)에 추가하세요."
        />

        <Space v-else class="mb-3" wrap>
          <Popconfirm
            v-for="target in targets"
            :key="target.key"
            v-perm:cust1
            cancel-text="취소"
            ok-text="배포"
            :title="`${target.name} 을(를) 실행합니다. 진행할까요?`"
            @confirm="run(target)"
          >
            <Tooltip :title="target.description || undefined">
              <Button
                :loading="running && runningKey === target.key"
                :disabled="running && runningKey !== target.key"
                :type="target.key === targets[0]?.key ? 'primary' : 'default'"
              >
                {{ target.name }}
              </Button>
            </Tooltip>
          </Popconfirm>
        </Space>
      </Spin>

      <!-- 진행 과정 -->
      <h6 class="mb-3 text-xl font-semibold">진행 과정</h6>
      <div
        class="max-h-[30rem] min-h-[15rem] overflow-y-auto rounded-sm bg-neutral-900 p-2 font-mono text-sm text-neutral-100"
      >
        <div v-if="buildLog.length === 0" class="text-neutral-500">
          배포를 시작하면 로그가 표시됩니다.
        </div>
        <div
          v-for="(log, index) in buildLog"
          :key="index"
          :class="lineClass(log.status)"
        >
          {{ log.message }}
        </div>
      </div>
    </Card>
  </Page>
</template>
