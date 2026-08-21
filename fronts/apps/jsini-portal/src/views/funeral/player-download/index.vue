<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Alert, Button, Spin, Tag } from 'ant-design-vue';

/**
 * [플레이어 다운로드]
 *
 * funeral_player 설치 파일을 OS 별로 내려받는 화면.
 *
 * 파일은 GitHub Releases 에 올라간다. 태그(v1.0.0 등)를 푸시하면
 * .github/workflows/release.yml 이 Windows x64 와 Linux arm64 산출물을 만들어 첨부한다.
 * 이 화면은 최신 릴리스 정보를 조회해 자산(asset)을 OS 별로 짝지어 보여준다.
 */

const REPO = 'quristyle/Funeralv2';
const RELEASE_PAGE = `https://github.com/${REPO}/releases`;

interface ReleaseAsset {
  name: string;
  browser_download_url: string;
  size: number;
}

interface ReleaseInfo {
  tag_name: string;
  published_at: string;
  html_url: string;
  assets: ReleaseAsset[];
}

const loading = ref(true);
const release = ref<ReleaseInfo | null>(null);
const loadError = ref('');

/** OS 별 카드 정의. matcher 로 릴리스 자산을 골라낸다. */
const PLATFORMS = [
  {
    key: 'windows',
    title: 'Windows',
    subtitle: '64비트 (x64)',
    icon: 'lucide:monitor',
    accent: 'text-sky-500',
    bg: 'bg-sky-500/10',
    matcher: (n: string) => n.includes('windows') && n.endsWith('.zip'),
    requirements: ['Windows 10 / 11 (64비트)', 'Visual C++ 2015-2022 재배포 패키지'],
    steps: ['zip 압축을 풀고 funeralv2_player.exe 실행'],
  },
  {
    key: 'raspberry',
    title: '라즈베리파이',
    subtitle: 'Raspberry Pi OS (64비트) · .deb',
    icon: 'lucide:cpu',
    accent: 'text-rose-500',
    bg: 'bg-rose-500/10',
    matcher: (n: string) => n.endsWith('.deb'),
    requirements: ['Raspberry Pi OS Lite 64비트 (Debian 13 trixie)', 'Raspberry Pi 4 이상'],
    steps: [
      'sudo apt install ./funeralv2-player_<버전>_arm64.deb',
      'sudo reboot  (재부팅하면 화면에 자동 실행)',
    ],
  },
  {
    key: 'linux-arm64',
    title: 'Linux arm64',
    subtitle: '수동 설치용 · tar.gz',
    icon: 'lucide:terminal',
    accent: 'text-emerald-500',
    bg: 'bg-emerald-500/10',
    matcher: (n: string) => n.includes('linux-arm64') && n.endsWith('.tar.gz'),
    requirements: ['deb 를 쓸 수 없는 arm64 환경', '의존 패키지 수동 설치 필요'],
    steps: ['압축 해제 후 동봉된 README.txt 절차대로 배치'],
  },
] as const;

/** 자산 이름으로 실제 파일을 찾아 카드에 붙인다. */
const cards = computed(() =>
  PLATFORMS.map((p) => {
    const asset = release.value?.assets.find((a) => p.matcher(a.name.toLowerCase()));
    return { ...p, asset };
  }),
);

const publishedText = computed(() => {
  if (!release.value?.published_at) return '';
  return new Date(release.value.published_at).toLocaleString('ko-KR');
});

function formatSize(bytes: number) {
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024).toFixed(0)} KB`;
}

async function fetchLatestRelease() {
  loading.value = true;
  loadError.value = '';
  try {
    const res = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`);
    if (!res.ok) {
      // 아직 릴리스를 한 번도 발행하지 않으면 404 가 온다.
      loadError.value =
        res.status === 404
          ? '아직 발행된 릴리스가 없습니다. 저장소에 버전 태그(예: v1.0.0)를 푸시하면 설치 파일이 자동으로 만들어집니다.'
          : `릴리스 정보를 가져오지 못했습니다. (HTTP ${res.status})`;
      return;
    }
    release.value = await res.json();
  } catch {
    loadError.value =
      '릴리스 정보를 가져오지 못했습니다. 이 PC 가 GitHub 에 접속할 수 없는 환경일 수 있습니다.';
  } finally {
    loading.value = false;
  }
}

onMounted(fetchLatestRelease);
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-4">
      <!-- ===== 헤더 ===== -->
      <div
        class="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-border bg-card p-4"
      >
        <div class="flex items-center gap-3">
          <div class="flex size-11 items-center justify-center rounded-lg bg-primary/10">
            <IconifyIcon icon="lucide:download" class="size-6 text-primary" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-base font-semibold">사이니지 플레이어 다운로드</span>
              <Tag v-if="release" color="processing">{{ release.tag_name }}</Tag>
            </div>
            <div class="text-xs text-muted-foreground">
              <template v-if="publishedText">{{ publishedText }} 배포</template>
              <template v-else>장비 OS 에 맞는 설치 파일을 내려받으세요.</template>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <Button :loading="loading" @click="fetchLatestRelease">
            <IconifyIcon icon="lucide:refresh-cw" class="mr-1 size-4" />
            새로고침
          </Button>
          <Button type="link" :href="RELEASE_PAGE" target="_blank">
            전체 버전 보기
            <IconifyIcon icon="lucide:external-link" class="ml-1 size-3.5" />
          </Button>
        </div>
      </div>

      <Alert v-if="loadError" type="warning" show-icon :message="loadError" />

      <!-- ===== OS 별 카드 ===== -->
      <Spin :spinning="loading">
        <div class="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <div
            v-for="card in cards"
            :key="card.key"
            class="flex flex-col rounded-xl border border-border bg-card p-5 transition-all hover:-translate-y-0.5 hover:shadow-lg"
          >
            <!-- 아이콘 + 제목 -->
            <div class="flex items-center gap-3">
              <div class="flex size-12 items-center justify-center rounded-xl" :class="card.bg">
                <IconifyIcon :icon="card.icon" class="size-6" :class="card.accent" />
              </div>
              <div class="min-w-0">
                <div class="text-base font-semibold">{{ card.title }}</div>
                <div class="truncate text-xs text-muted-foreground">{{ card.subtitle }}</div>
              </div>
            </div>

            <!-- 요구 사항 -->
            <div class="mt-4 space-y-1.5">
              <div
                v-for="req in card.requirements"
                :key="req"
                class="flex items-start gap-2 text-xs text-muted-foreground"
              >
                <IconifyIcon icon="lucide:check" class="mt-0.5 size-3.5 shrink-0 text-emerald-500" />
                <span>{{ req }}</span>
              </div>
            </div>

            <!-- 설치 절차 -->
            <div class="mt-4 rounded-lg bg-muted/50 p-3">
              <div class="mb-1.5 text-[11px] font-medium text-muted-foreground">설치 방법</div>
              <div
                v-for="(step, i) in card.steps"
                :key="i"
                class="break-all font-mono text-[11px] leading-relaxed"
              >
                {{ step }}
              </div>
            </div>

            <!-- 다운로드 -->
            <div class="mt-auto pt-4">
              <template v-if="card.asset">
                <Button type="primary" block :href="card.asset.browser_download_url">
                  <IconifyIcon icon="lucide:download" class="mr-1 size-4" />
                  다운로드
                </Button>
                <div class="mt-2 truncate text-center text-[11px] text-muted-foreground">
                  {{ card.asset.name }} · {{ formatSize(card.asset.size) }}
                </div>
              </template>
              <template v-else>
                <Button block disabled>파일 없음</Button>
                <div class="mt-2 text-center text-[11px] text-muted-foreground">
                  이 버전에 해당 OS 파일이 없습니다.
                </div>
              </template>
            </div>
          </div>
        </div>
      </Spin>

      <div class="text-xs text-muted-foreground">
        설치 파일은 저장소에 버전 태그를 푸시할 때 자동으로 빌드되어 GitHub Releases 에 첨부됩니다.
        라즈베리파이는 <code>.deb</code> 설치를 권장합니다 — 의존 패키지와 자동 실행 설정까지 함께 처리됩니다.
      </div>
    </div>
  </Page>
</template>
