<script lang="ts" setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Button,
  Descriptions,
  DescriptionsItem,
  Input,
  message,
  Modal,
  Spin,
  Tag,
  Textarea,
} from 'ant-design-vue';

import {
  createPlayerRelease,
  getPlayerReleaseRun,
  getPlayerReleaseStatus,
  type PlayerReleaseApi,
} from '#/api/portal/system/player-release';

/**
 * [플레이어 릴리스]
 *
 * funeralv2_player 설치 파일을 만들어 GitHub Release 로 내보내는 화면.
 * 발행하면 `/system/player-download` 화면이 그 파일을 찾아 보여 준다.
 *
 * [무엇이 방아쇠인가]
 * 버전 태그(`v1.0.0`)를 만드는 것이다. `.github/workflows/release.yml` 이
 * `push: tags: ['v*']` 로 깨어나 다섯 갈래(Windows · Debian13 arm64 ·
 * Ubuntu24 amd64 · Ubuntu24 arm64 · Android)를 빌드하고 릴리스에 첨부한다.
 *
 * 수동 실행(workflow_dispatch)으로는 **릴리스가 발행되지 않는다** — 워크플로의
 * 첨부 단계가 태그일 때만 돌기 때문이다. 그래서 이 화면은 태그를 만든다.
 *
 * [토큰은 여기 없다]
 * 태그를 만들려면 `repo` 권한 토큰이 필요한데 브라우저에 두면 꺼내 갈 수 있다.
 * 토큰은 AuthServer 에만 있고 이 화면은 그 서버만 부른다.
 */

const loading = ref(true);
const status = ref<null | PlayerReleaseApi.Status>(null);
const loadError = ref('');

const version = ref('');
const notes = ref('');
const submitting = ref(false);

/** 지켜보고 있는 태그. 발행 직후부터 폴링한다. */
const watching = ref('');
const run = ref<null | PlayerReleaseApi.Run>(null);
let timer: null | ReturnType<typeof setTimeout> = null;

/** 릴리스 발행이 가능한 상태인가 */
const ready = computed(
  () => Boolean(status.value?.configured) && Boolean(status.value?.canRelease),
);

/** 이미 있는 태그를 적었는가. 서버도 막지만 먼저 알려 준다. */
const duplicated = computed(() => {
  const v = version.value.trim().replace(/^v/i, '');
  if (!v) return false;
  return (status.value?.tags ?? []).some(
    (t) => t.replace(/^v/i, '') === v,
  );
});

/** 버전 형식이 맞는가. 서버와 같은 규칙이다. */
const versionValid = computed(() =>
  /^\d+\.\d+\.\d+(-[\w.-]+)?$/.test(version.value.trim().replace(/^v/i, '')),
);

const canSubmit = computed(
  () =>
    ready.value &&
    versionValid.value &&
    !duplicated.value &&
    !submitting.value &&
    !running.value,
);

/** 지금 빌드가 돌고 있는가 */
const running = computed(
  () => Boolean(watching.value) && run.value?.status !== 'completed',
);

async function load() {
  loading.value = true;
  loadError.value = '';
  try {
    status.value = await getPlayerReleaseStatus();
    if (!version.value && status.value?.suggestedVersion) {
      version.value = status.value.suggestedVersion;
    }
  } catch {
    loadError.value = '릴리스 정보를 가져오지 못했습니다.';
  } finally {
    loading.value = false;
  }
}

/**
 * 발행 전 확인.
 *
 * 태그와 릴리스는 지울 수는 있어도 **이미 받아 간 사람에게는 되돌릴 수 없다.**
 * 실수로 한 번 누르는 일이 없도록 한 단계를 둔다.
 */
function confirmRelease() {
  const tag = `v${version.value.trim().replace(/^v/i, '')}`;
  Modal.confirm({
    content: `${status.value?.branch} 의 최신 커밋으로 ${tag} 를 만들고 빌드를 시작합니다. 빌드는 20~40분 걸리며, 끝나면 다운로드 화면에 파일이 나타납니다.`,
    okText: '발행',
    cancelText: '취소',
    onOk: submit,
    title: `${tag} 를 발행할까요?`,
  });
}

async function submit() {
  submitting.value = true;
  try {
    const result = await createPlayerRelease(version.value.trim(), notes.value.trim());
    message.success(result.message);
    watching.value = result.tag;
    run.value = null;
    poll();
    load();
  } catch (error: any) {
    // 서버가 담아 준 이유를 그대로 보여 준다(권한 없음·중복 태그·설정 없음).
    message.error(
      error?.response?.data?.message ?? error?.message ?? '릴리스에 실패했습니다.',
    );
  } finally {
    submitting.value = false;
  }
}

/**
 * 진행 상황 폴링.
 *
 * 태그를 만든 직후에는 GitHub 이 워크플로를 큐에 넣는 데 몇 초 걸린다 —
 * 그때는 `pending` 이라 '실패'로 보이지 않는다. 끝나면 스스로 멈춘다.
 */
async function poll() {
  if (!watching.value) return;
  try {
    run.value = await getPlayerReleaseRun(watching.value);
  } catch {
    // 폴링 실패는 조용히 넘긴다. 다음 차례에 다시 묻는다.
  }

  if (run.value?.status === 'completed') {
    if (run.value.conclusion === 'success') {
      message.success(`${watching.value} 빌드가 끝났습니다.`);
    } else {
      message.error(`${watching.value} 빌드가 실패했습니다. GitHub 에서 확인하세요.`);
    }
    load();
    return;
  }

  timer = setTimeout(poll, 10_000);
}

function jobColor(job: PlayerReleaseApi.Job) {
  if (job.conclusion === 'success') return 'success';
  if (job.conclusion === 'skipped') return 'default';
  if (job.conclusion) return 'error';
  if (job.status === 'in_progress') return 'processing';
  return 'default';
}

function jobLabel(job: PlayerReleaseApi.Job) {
  if (job.conclusion === 'success') return '성공';
  if (job.conclusion === 'failure') return '실패';
  if (job.conclusion === 'cancelled') return '취소됨';
  if (job.conclusion === 'skipped') return '건너뜀';
  if (job.status === 'in_progress') return job.currentStep ?? '진행 중';
  return '대기';
}

onMounted(load);
onUnmounted(() => {
  if (timer) clearTimeout(timer);
});
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-4 overflow-auto">
      <Spin :spinning="loading">
        <Alert v-if="loadError" :message="loadError" show-icon type="error" />

        <!-- 서버에 GitHub 설정이 없다 -->
        <Alert
          v-else-if="status && !status.configured"
          :description="status.setupHint ?? ''"
          message="릴리스 설정이 아직 없습니다"
          show-icon
          type="warning"
        />

        <!-- 권한이 없다. 화면은 보여 주되 발행은 막는다 -->
        <Alert
          v-else-if="status && !status.canRelease"
          description="릴리스 발행 권한이 없습니다. 관리자에게 문의하세요. (권한은 서버가 판정하므로 이 화면을 열어도 발행되지 않습니다.)"
          message="읽기 전용"
          show-icon
          type="info"
        />

        <Alert
          v-if="status?.warning"
          :message="status.warning"
          class="mt-2"
          show-icon
          type="warning"
        />

        <!-- ===== 대상 ===== -->
        <Descriptions
          v-if="status"
          bordered
          class="mt-4"
          :column="1"
          size="small"
          title="무엇을 릴리스하나"
        >
          <DescriptionsItem label="저장소">
            {{ status.repository }}
          </DescriptionsItem>
          <DescriptionsItem label="브랜치">
            {{ status.branch }}
          </DescriptionsItem>
          <DescriptionsItem label="최신 커밋">
            <span v-if="status.headSha" class="font-mono text-xs">
              {{ status.headSha.slice(0, 7) }}
            </span>
            <span class="ml-2">{{ status.headMessage ?? '—' }}</span>
          </DescriptionsItem>
          <DescriptionsItem label="최근 릴리스">
            <Tag v-if="status.latestRelease" color="processing">
              {{ status.latestRelease }}
            </Tag>
            <span v-else class="text-muted-foreground">아직 없습니다</span>
          </DescriptionsItem>
        </Descriptions>

        <!-- ===== 입력 ===== -->
        <div v-if="status" class="mt-4 flex flex-col gap-3">
          <div>
            <div class="mb-1 text-sm font-semibold">버전</div>
            <Input
              v-model:value="version"
              :disabled="!ready || running"
              placeholder="1.0.0"
              style="max-width: 260px"
            >
              <template #addonBefore>v</template>
            </Input>
            <div class="mt-1 text-xs text-muted-foreground">
              <span v-if="version && !versionValid" class="text-red-500">
                1.0.0 형식으로 적습니다. 미리보기는 1.0.0-rc1 처럼 씁니다.
              </span>
              <span v-else-if="duplicated" class="text-red-500">
                이미 있는 태그입니다. 다른 버전을 적으세요.
              </span>
              <span v-else>
                태그는 <code>v{{ version || '1.0.0' }}</code> 로 만들어집니다.
              </span>
            </div>
          </div>

          <div>
            <div class="mb-1 text-sm font-semibold">
              릴리스 노트
              <span class="font-normal text-muted-foreground">(선택)</span>
            </div>
            <Textarea
              v-model:value="notes"
              :disabled="!ready || running"
              placeholder="비우면 커밋 목록으로 자동 생성됩니다."
              :rows="3"
              style="max-width: 640px"
            />
          </div>

          <div class="flex items-center gap-3">
            <Button
              v-perm:create
              :disabled="!canSubmit"
              :loading="submitting"
              type="primary"
              @click="confirmRelease"
            >
              <template #icon>
                <IconifyIcon
                  class="mr-1 inline-block size-4 align-text-bottom"
                  icon="lucide:rocket"
                />
              </template>
              릴리스 발행
            </Button>
            <Button :disabled="loading" @click="load">
              <template #icon>
                <IconifyIcon
                  class="mr-1 inline-block size-4 align-text-bottom"
                  icon="lucide:refresh-cw"
                />
              </template>
              새로 고침
            </Button>
          </div>
        </div>

        <!-- ===== 진행 상황 ===== -->
        <div v-if="watching" class="mt-6">
          <div class="mb-2 flex items-center gap-2">
            <span class="text-sm font-semibold">{{ watching }} 빌드</span>
            <Tag v-if="run?.runNumber" color="default">#{{ run.runNumber }}</Tag>
            <a
              v-if="run?.htmlUrl"
              class="text-xs text-primary"
              :href="run.htmlUrl"
              rel="noreferrer"
              target="_blank"
            >
              GitHub 에서 보기
            </a>
          </div>

          <Alert
            v-if="run?.pending"
            message="빌드를 준비하고 있습니다. 몇 초 걸립니다."
            show-icon
            type="info"
          />

          <div v-else class="flex flex-col gap-1">
            <div
              v-for="job in run?.jobs ?? []"
              :key="job.name"
              class="flex items-center gap-2"
            >
              <Tag :color="jobColor(job)" style="min-width: 92px; text-align: center">
                {{ jobLabel(job) }}
              </Tag>
              <span class="text-sm">{{ job.name }}</span>
            </div>
          </div>

          <Alert
            v-if="run?.releaseUrl"
            class="mt-3"
            message="릴리스가 발행되었습니다. 플레이어 다운로드 화면에서 받을 수 있습니다."
            show-icon
            type="success"
          />
        </div>

        <!-- ===== 지난 태그 ===== -->
        <div v-if="status?.tags?.length" class="mt-6">
          <div class="mb-2 text-sm font-semibold">지난 버전</div>
          <div class="flex flex-wrap gap-1">
            <Tag v-for="t in status.tags" :key="t">{{ t }}</Tag>
          </div>
        </div>
      </Spin>
    </div>
  </Page>
</template>
