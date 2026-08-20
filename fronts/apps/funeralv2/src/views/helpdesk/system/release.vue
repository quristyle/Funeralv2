<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Input,
  message,
  Space,
  Tag,
} from 'ant-design-vue';

import { runRelease, runReleaseGithub } from '#/api/helpdesk';

/**
 * [릴리즈 도구]
 *
 * 원본(ReleseTool.vue). 서버의 빌드 스크립트를 실행하고 결과를 로그로 보여준다.
 * 원본에는 실제 호출 대신 진행 단계를 흉내 내는 코드가 함께 있었는데,
 * 여기서는 서버 응답만 그대로 로그에 싣는다.
 */

interface LogLine {
  message: string;
  status: 'error' | 'info' | 'success';
  time: string;
}

const running = ref(false);
const releaseVersion = ref('');
const buildLog = ref<LogLine[]>([]);

function appendLog(status: LogLine['status'], text: string) {
  buildLog.value.push({
    message: text,
    status,
    time: new Date().toLocaleTimeString('ko-KR'),
  });
}

async function run(kind: 'ghub' | 'local') {
  running.value = true;
  buildLog.value = [];
  appendLog(
    'info',
    `${kind === 'local' ? '로컬' : 'GitHub'} 릴리즈 빌드를 시작합니다...`,
  );

  try {
    const payload = releaseVersion.value.trim()
      ? { version: releaseVersion.value.trim() }
      : {};
    const result =
      kind === 'local'
        ? await runRelease(payload)
        : await runReleaseGithub(payload);

    // 서버가 문자열/객체 어느 쪽으로 응답해도 읽히도록 그대로 찍는다.
    appendLog(
      'success',
      typeof result === 'string' ? result : JSON.stringify(result, null, 2),
    );
    message.success('빌드 요청을 완료했습니다.');
  } catch (error) {
    appendLog('error', (error as Error).message ?? '빌드에 실패했습니다.');
  } finally {
    running.value = false;
  }
}

function statusColor(status: LogLine['status']) {
  if (status === 'success') return 'success';
  if (status === 'error') return 'error';
  return 'default';
}
</script>

<template>
  <Page auto-content-height>
    <Alert
      class="mb-3"
      description="서버에서 빌드 스크립트를 실행합니다. 운영 배포에 영향을 줄 수 있으니 확인 후 실행하세요."
      message="릴리즈 빌드"
      show-icon
      type="warning"
    />

    <Card class="mb-3" size="small">
      <Space wrap>
        <Input
          v-model:value="releaseVersion"
          placeholder="버전 (선택)"
          style="width: 200px"
        />
        <Button :loading="running" type="primary" @click="run('local')">
          빌드 및 배포
        </Button>
        <Button :loading="running" @click="run('ghub')">
          GitHub 릴리즈
        </Button>
      </Space>
    </Card>

    <Card size="small" title="빌드 로그">
      <div
        class="max-h-[420px] overflow-auto rounded bg-muted/40 p-3 font-mono text-xs"
      >
        <div v-if="buildLog.length === 0" class="text-muted-foreground">
          아직 실행한 빌드가 없습니다.
        </div>
        <div
          v-for="(line, index) in buildLog"
          :key="index"
          class="mb-1 flex gap-2"
        >
          <span class="shrink-0 text-muted-foreground">{{ line.time }}</span>
          <Tag :color="statusColor(line.status)">{{ line.status }}</Tag>
          <pre class="m-0 whitespace-pre-wrap">{{ line.message }}</pre>
        </div>
      </div>
    </Card>
  </Page>
</template>
