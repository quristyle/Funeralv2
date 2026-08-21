<script lang="ts" setup>
import { onMounted, onUnmounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  message,
  Popconfirm,
  Space,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { runRelease, runReleaseGithub } from '#/api/helpdesk';

/**
 * [릴리즈 도구]
 *
 * 원본(JinReception sys/ReleseTool.vue, `/buildRelease`).
 *
 * 서버(`POST /api/build/release`, `/release_ghub`)는 RabbitMQ 의 `run_script` 큐에
 * 배포 스크립트 실행 메시지를 넣고 바로 응답한다. 즉 진행 상황을 흘려보내 주지 않는다.
 * 그래서 원본도 아래 단계 목록을 정해진 시간에 맞춰 찍어 주는 방식이었고,
 * 여기서도 같은 단계·같은 시간으로 보여 준다. 실제 진행률이 아니라 예상 흐름이다.
 *
 * 원본과 달리 요청 결과를 실제로 기다렸다가 실패하면 로그와 알림에 드러낸다.
 * (원본은 호출을 await 하지 않아 서버 오류가 화면에 남지 않았다.)
 */

interface LogLine {
  message: string;
  status: 'error' | 'info' | 'success';
}

/** 배포 스크립트가 거치는 단계와 대략의 소요 시간(ms). 원본 값 그대로. */
const BUILD_STEPS = [
  { duration: 1000, name: 'source get' },
  { duration: 2500, name: 'source checking' },
  { duration: 3500, name: 'front build' },
  { duration: 2500, name: 'front publish' },
  { duration: 3500, name: 'front checking' },
  { duration: 2500, name: 'backend build' },
  { duration: 3000, name: 'backend restart' },
];

const loading = ref(false);
const releaseVersion = ref('');
const buildLog = ref<LogLine[]>([]);

/** 화면을 떠나면 남은 단계 타이머를 끊는다. */
let disposed = false;
onUnmounted(() => {
  disposed = true;
});

/**
 * 배포된 버전을 읽는다. 원본과 같은 `public/version.json` 규약으로,
 * 배포 스크립트가 서버에 떨어뜨려 놓는 파일이다.
 * 그 파일이 없는 환경(로컬 등)에서는 빌드 시 박아 둔 앱 버전으로 대신한다.
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

async function build(kind: 'ghub' | 'jin114') {
  buildLog.value = [];
  loading.value = true;

  try {
    // 요청은 먼저 보내 둔다. 서버는 큐에 넣고 바로 응답한다.
    const request = kind === 'jin114' ? runRelease() : runReleaseGithub();

    for (const step of BUILD_STEPS) {
      if (disposed) return;
      appendLog('info', `[INFO] Starting: ${step.name}...`);
      await wait(step.duration);
      if (disposed) return;
      appendLog('success', `[SUCCESS] Completed: ${step.name}`);
    }

    await request;

    message.success('새 버전이 성공적으로 배포되었습니다.');
    await loadVersion();
  } catch (error) {
    const detail = (error as Error).message ?? '알 수 없는 오류';
    appendLog('error', `[ERROR] Build failed: ${detail}`);
    message.error(`배포 중 오류가 발생했습니다: ${detail}`);
  } finally {
    loading.value = false;
  }
}

/** 버전만 다시 읽어 알려 준다(원본 reload). */
async function reload() {
  await loadVersion();
  message.info(`현재 버전은 v${releaseVersion.value} 입니다.`);
}

function lineClass(status: LogLine['status']) {
  if (status === 'success') return 'text-green-400';
  if (status === 'error') return 'text-red-400';
  return 'text-blue-400';
}

onMounted(loadVersion);
</script>

<template>
  <Page auto-content-height>
    <Card size="small">
      <!-- 제목 + 현재 버전 -->
      <div
        class="mb-3 flex flex-col gap-2 md:flex-row md:items-center md:justify-between"
      >
        <h5 class="m-0 text-xl font-bold">릴리즈 도구</h5>
        <div class="flex items-center gap-2">
          <span class="text-lg font-medium text-muted-foreground">
            현재 버전:
          </span>
          <Tag color="success">
            {{ releaseVersion ? `v${releaseVersion}` : 'Loading...' }}
          </Tag>
          <Tooltip placement="bottom" title="버전 새로고침">
            <Button shape="circle" size="small" type="text" @click="reload">
              ⟳
            </Button>
          </Tooltip>
        </div>
      </div>

      <Alert
        class="mb-3"
        description="배포 스크립트를 실행하는 요청을 서버에 보냅니다. 운영 서비스가 재시작되니 확인 후 실행하세요. 아래 진행 과정은 서버가 알려 주는 실제 진행률이 아니라 스크립트가 거치는 단계를 순서대로 보여 주는 것입니다."
        message="운영 배포"
        show-icon
        type="warning"
      />

      <!-- 배포 버튼 -->
      <Space class="mb-3" wrap>
        <Popconfirm
          cancel-text="취소"
          ok-text="배포"
          title="jin114 로 배포합니다. 진행할까요?"
          @confirm="build('jin114')"
        >
          <Button :loading="loading" type="primary">jin114 배포</Button>
        </Popconfirm>
        <Popconfirm
          cancel-text="취소"
          ok-text="배포"
          title="goldb 로 배포합니다. 진행할까요?"
          @confirm="build('ghub')"
        >
          <Button :loading="loading">goldb 배포</Button>
        </Popconfirm>
      </Space>

      <!-- 진행 과정 -->
      <h6 class="mb-3 text-xl font-semibold">진행 과정</h6>
      <div
        class="min-h-[15rem] max-h-[30rem] overflow-y-auto rounded-sm bg-neutral-900 p-2 font-mono text-sm text-neutral-100"
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
