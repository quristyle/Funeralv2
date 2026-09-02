<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Alert, Button, Spin, Tag } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';

/**
 * [플레이어 다운로드]
 *
 * funeralv2_player 설치 파일을 OS 별로 내려받는 화면.
 *
 * 파일은 GitHub Releases 에 올라간다. 태그(v1.0.0 등)를 푸시하면
 * `.github/workflows/release.yml` 이 OS 별 산출물을 만들어 첨부한다.
 * 이 화면은 최신 릴리스 정보를 조회해 자산(asset)을 OS 별로 짝지어 보여준다.
 *
 * [리눅스는 배포판마다 파일이 다르다]
 * Flutter 리눅스 빌드는 빌드한 곳의 glibc 를 그대로 요구한다.
 * Debian 13 trixie(glibc 2.41) 에서 빌드한 것은 Ubuntu 24.04(2.39) 에서 실행되지 않는다.
 * 그래서 자산 이름에 배포판이 들어가고(`_debian13_` · `_ubuntu24_`),
 * 아래 matcher 도 배포판까지 함께 본다 — `.deb` 로만 고르면 엉뚱한 파일을 준다.
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

/**
 * OS 별 카드 정의. matcher 로 릴리스 자산을 골라낸다.
 *
 * matcher 는 **배포판과 아키텍처까지** 본다. 예전에는 라즈베리파이 카드가
 * `.deb` 로만 골랐는데, Ubuntu 용 .deb 가 생기면서 그렇게 두면 둘 중 아무 것이나
 * 집어 온다. 이름 규칙은 `packaging/build_deb.sh` 가 정한다.
 */
const PLATFORMS = [
  {
    key: 'windows',
    title: 'Windows',
    subtitle: '10 / 11 · 64비트 (x64)',
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
    subtitle: 'Raspberry Pi OS 64비트 · .deb',
    icon: 'lucide:cpu',
    accent: 'text-rose-500',
    bg: 'bg-rose-500/10',
    matcher: (n: string) =>
      n.includes('debian13') && n.includes('arm64') && n.endsWith('.deb'),
    requirements: [
      'Raspberry Pi OS Lite 64비트 (Debian 13 trixie)',
      'Raspberry Pi 4 이상',
    ],
    steps: [
      'sudo apt install ./funeralv2-player_<버전>_debian13_arm64.deb',
      'sudo reboot  (재부팅하면 화면에 자동 실행)',
    ],
  },
  {
    key: 'ubuntu-amd64',
    title: 'Ubuntu (x64)',
    subtitle: '24.04 LTS · 미니PC · .deb',
    icon: 'lucide:monitor-play',
    accent: 'text-orange-500',
    bg: 'bg-orange-500/10',
    matcher: (n: string) =>
      n.includes('ubuntu24') && n.includes('amd64') && n.endsWith('.deb'),
    requirements: [
      'Ubuntu 24.04 LTS 이상 (64비트)',
      '서버 · 최소 설치 권장 (데스크톱이면 디스플레이 매니저를 끈다)',
    ],
    steps: [
      'sudo apt install ./funeralv2-player_<버전>_ubuntu24_amd64.deb',
      'sudo systemctl disable --now gdm3   (데스크톱 설치인 경우)',
      'sudo reboot',
    ],
  },
  {
    key: 'ubuntu-arm64',
    title: 'Ubuntu (arm64)',
    subtitle: '24.04 LTS · Jetson 등 · .deb',
    icon: 'lucide:circuit-board',
    accent: 'text-amber-500',
    bg: 'bg-amber-500/10',
    matcher: (n: string) =>
      n.includes('ubuntu24') && n.includes('arm64') && n.endsWith('.deb'),
    requirements: [
      'Ubuntu 24.04 LTS 이상 (arm64)',
      'Ubuntu 를 올린 arm 보드 (라즈베리파이는 위 카드를 쓴다)',
    ],
    steps: [
      'sudo apt install ./funeralv2-player_<버전>_ubuntu24_arm64.deb',
      'sudo reboot',
    ],
  },
  {
    key: 'android',
    title: 'Android TV',
    subtitle: 'TV 박스 · 태블릿 · .apk',
    icon: 'lucide:tv',
    accent: 'text-lime-500',
    bg: 'bg-lime-500/10',
    matcher: (n: string) => n.endsWith('.apk'),
    requirements: [
      'Android 5.0 이상 (TV 박스 · 태블릿)',
      '알 수 없는 출처 설치 허용 필요',
    ],
    steps: [
      'adb install -r funeralv2_player-<버전>-android-*.apk',
      '또는 USB · Downloader 앱으로 설치',
      'debugsigned 는 업데이트 시 기존 앱 삭제 후 재설치',
    ],
  },
  {
    key: 'tar-arm64',
    title: '수동 설치 (arm64)',
    subtitle: 'deb 를 못 쓰는 환경 · tar.gz',
    icon: 'lucide:terminal',
    accent: 'text-emerald-500',
    bg: 'bg-emerald-500/10',
    matcher: (n: string) => n.includes('arm64') && n.endsWith('.tar.gz'),
    requirements: ['deb 를 쓸 수 없는 arm64 환경', '의존 패키지 수동 설치 필요'],
    steps: ['압축 해제 후 동봉된 README.txt 절차대로 배치'],
  },
  {
    key: 'tar-amd64',
    title: '수동 설치 (x64)',
    subtitle: 'deb 를 못 쓰는 환경 · tar.gz',
    icon: 'lucide:terminal',
    accent: 'text-teal-500',
    bg: 'bg-teal-500/10',
    matcher: (n: string) => n.includes('amd64') && n.endsWith('.tar.gz'),
    requirements: ['deb 를 쓸 수 없는 x64 환경', '의존 패키지 수동 설치 필요'],
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

/**
 * GitHub Releases 목록을 새 창으로 연다.
 * VxeButton 에는 href 가 없어서(앵커가 아니라 button 이다) 여기서 연다.
 */
function openReleasePage() {
  window.open(RELEASE_PAGE, '_blank', 'noopener');
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

        <!--
          그리드 도구줄과 같은 동그란 아이콘 단추를 쓴다. 글자 단추 둘이 머리줄 폭을
          많이 먹어서 다른 화면의 도구줄과 모양이 어긋났다.
          아이콘만 남으므로 title 을 반드시 준다 — 마우스를 올리면 무엇인지 나온다.
        -->
        <div class="flex items-center gap-2">
          <GridIconButton
            icon="vxe-table-icon-repeat"
            title="새로고침"
            :loading="loading"
            @click="fetchLatestRelease"
          />
          <!-- 바깥으로 나가는 링크라 share 아이콘을 쓴다. vxe-table 쪽 아이콘 묶음에는
               나가기에 해당하는 것이 없어 vxe-pc-ui 것을 쓴다(둘 다 올라와 있다). -->
          <GridIconButton
            icon="vxe-icon-share"
            title="전체 버전 보기 (GitHub Releases)"
            @click="openReleasePage"
          />
        </div>
      </div>

      <Alert v-if="loadError" type="warning" show-icon :message="loadError" />

    

      <!-- ===== OS 별 카드 ===== -->
      <!--
        카드가 여섯이라 넓은 화면에서도 두 줄이 된다. 화면 전체가 스크롤되지 않도록
        (준수사항 4) 카드 영역만 안에서 스크롤한다.

        Spin 으로 감싸지 않는다 — antd 가 안쪽에 감싸개를 하나 더 만들어
        높이 사슬(h-full)이 그 자리에서 끊긴다. 대신 겹쳐 띄운다.
      -->
      <div class="relative min-h-0 flex-1">
        <div
          v-if="loading"
          class="bg-background/60 absolute inset-0 z-10 flex items-center justify-center"
        >
          <Spin />
        </div>

        <div
          class="grid h-full grid-cols-1 gap-4 overflow-auto pr-1 md:grid-cols-2 xl:grid-cols-3"
        >
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
            <!--
              고정폭(`font-mono`)을 쓰지 않는다.

              명령어라서 고정폭이 어울린다고 두었는데, 그러면 환경설정에서 고른 글꼴이
              이 칸에만 적용되지 않는다(Tailwind 의 고정폭 목록이 앞을 차지한다).
              화면 안에서 글꼴이 갈려 보이는 쪽이 더 큰 문제라 사용자 글꼴을 따르게 한다.

              명령어라는 것은 글꼴이 아니라 회색 상자와 '설치 방법' 머리글로 알린다.
              여기 들어가는 것은 한 줄짜리 명령어라 자리 맞춤이 필요하지 않다.
            -->
            <div class="bg-muted/50 mt-4 rounded-lg p-3">
              <div class="text-muted-foreground mb-1.5 text-[11px] font-medium">
                설치 방법
              </div>
              <div
                v-for="(step, i) in card.steps"
                :key="i"
                class="text-[11px] leading-relaxed break-all"
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
      </div>

      <div class="text-muted-foreground shrink-0 text-xs">
        설치 파일은 저장소에 버전 태그(예: <code>v1.0.0</code>)를 푸시할 때 자동으로 빌드되어
        GitHub Releases 에 첨부됩니다. 리눅스는 <code>.deb</code> 설치를 권장합니다 —
        의존 패키지와 부팅 시 자동 실행 설정까지 함께 처리됩니다.
      </div>
    </div>
  </Page>
</template>
