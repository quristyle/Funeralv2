<script lang="ts" setup>
import { computed, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Button, message, Popconfirm, Modal, Tag, Tooltip } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource, retryThumbnail, retryAudio } from '#/api/funeral/building';
import ImagePreview from '#/components/ImagePreview.vue';
import AudioUploadModal from './modules/audio-upload-modal.vue';

const uploadModalRef = ref<InstanceType<typeof AudioUploadModal> | null>(null);

const showPlayModal = ref<boolean>(false);
const currentAudioName = ref<string>('');
const currentAudioThumbnail = ref<string>('');

/**
 * 들려줄 URL 후보 목록. 앞의 것부터 시도하고 브라우저가 못 받거나 못 읽으면 다음으로 넘어간다.
 * 변환본 AAC 를 앞에 두는 이유는 재생 호환성이다 — OGG/Opus 는 사파리가 못 읽고,
 * 원본은 100MB 에 가까운 것도 있다.
 */
const playSources = ref<string[]>([]);
const playIndex = ref<number>(0);
/** 후보를 다 써도 못 들려줬을 때 사용자에게 보일 사유 */
const playError = ref<string>('');

const currentAudioUrl = computed<string>(() => playSources.value[playIndex.value] ?? '');

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'thumbnailUrl', title: '커버', width: 80, slots: { default: 'thumbnail' } },
      { field: 'name', title: '음원/배경음악 명칭', minWidth: 180 },
      { field: 'shortName', title: '짧은 명칭', width: 120 },
      { field: 'status', title: '상태', width: 100, slots: { default: 'status' } },
      { field: 'sortOrder', title: '순서', width: 80 },
      { field: 'url', title: '음원 URL 경로', minWidth: 280 },
      { field: 'remark', title: '설명', minWidth: 200 },
      {
        field: 'action',
        title: '청취 및 관리',
        width: 200,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getMediaSources('AUDIO');
        },
      },
    },
  },
});

function openUpload() {
  if (uploadModalRef.value) {
    uploadModalRef.value.open();
  }
}

function handleEdit(row: any) {
  if (uploadModalRef.value) {
    uploadModalRef.value.open(row);
  }
}

/**
 * 그리드 커버 칸에 쓸 축소본 경로.
 *
 * `thumbnailUrl` 은 원본 내려받기 경로라 40x40 자리에 앨범아트 원본이 그대로 온다.
 * 파일 아이디를 알면 150px 축소본을 쓴다. 없거나 실패하면 ImagePreview 가
 * `fallback-src` 로 원본을 다시 시도하고, 그것도 안 되면 🎵 를 그린다.
 *
 * 커버가 아예 없는 행이 많은 것은 고장이 아니다 — 앨범아트는 원본 파일에 박혀 있어야
 * 뽑아낼 수 있는데, 제례 음원 대부분은 그것이 없는 OGG/AAC 다.
 */
function coverSrc(row: any): string | null {
  return row.thumbnailFileId
    ? `/api/file/thumbnail/${row.thumbnailFileId}`
    : (row.thumbnailUrl ?? null);
}

/**
 * 플레이어 팝업을 연다.
 *
 * @param row      대상 행
 * @param sources  들려줄 URL 후보 (앞에서부터 시도)
 * @param suffix   제목 뒤에 붙일 표기
 */
function openPlayer(row: any, sources: (string | null | undefined)[], suffix = '') {
  const usable = sources.filter((url): url is string => !!url);
  playSources.value = usable;
  playIndex.value = 0;
  playError.value = usable.length === 0 ? '재생할 수 있는 파일 경로가 없습니다.' : '';
  currentAudioName.value = row.name + suffix;
  currentAudioThumbnail.value = row.thumbnailUrl || '';
  showPlayModal.value = true;
}

function handlePlayOgg(row: any) {
  openPlayer(row, [row.oggUrl], ' (OGG)');
}

/**
 * 브라우저가 현재 후보를 재생하지 못했을 때. 다음 후보가 있으면 넘어가고,
 * 없으면 사유를 적어 둔다 — 아무 반응 없는 플레이어만 남는 것을 막는다.
 */
function handleAudioError() {
  if (playIndex.value < playSources.value.length - 1) {
    playIndex.value += 1;
    return;
  }
  playError.value =
    '음원을 재생하지 못했습니다. 파일이 서버에 없거나 브라우저가 지원하지 않는 코덱일 수 있습니다.';
}

async function handleRetryCover(row: any) {
  try {
    await retryThumbnail(row.id);
    message.success('커버이미지 재추출 요청이 처리되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('커버 재추출 요청 실패');
  }
}

async function handleRetryAudio(row: any) {
  try {
    await retryAudio(row.id);
    message.success('오디오 재변환(OGG/AAC) 요청이 처리되었습니다. 변환이 백그라운드에서 실행됩니다.');
    gridApi.query();
  } catch (error) {
    message.error('오디오 재변환 요청 실패');
  }
}

async function handleDelete(row: any) {
  try {
    await deleteMediaSource(row.id);
    message.success('음원 소스가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

// 오디오 재생 팝업. 호환성이 넓은 AAC 변환본 → 원본 → OGG 순으로 시도한다.
function handlePlay(row: any) {
  openPlayer(row, [row.hasAac ? row.aacUrl : null, row.url, row.hasOgg ? row.oggUrl : null]);
}

function handleClosePlayer() {
  playSources.value = [];
  playIndex.value = 0;
  playError.value = '';
  currentAudioThumbnail.value = '';
  showPlayModal.value = false;
}

function formatDate(dateStr?: string) {
  if (!dateStr) return '-';
  try {
    return new Date(dateStr).toLocaleString('ko-KR');
  } catch {
    return dateStr;
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="관내 방송 및 제례용 음원 리소스 목록">
      <template #toolbar-tools>
        <GridIconButton
          icon="vxe-icon-add"
          title="신규 음원 등록"
          @click="openUpload"
        />
      </template>

      <template #thumbnail="{ row }">
        <ImagePreview
          :src="coverSrc(row)"
          :fallback-src="row.thumbnailUrl"
          :preview-src="row.thumbnailUrl"
          :width="40"
          :height="40"
          fallback-text="🎵"
        />
      </template>

      <!-- 변환 상태 컬럼 슬롯 렌더러 -->
      <template #status="{ row }">
        <Tag v-if="row.status === 'COMPLETED'" color="success">완료</Tag>
        <Tag v-else-if="row.status === 'READY'" color="ready">준비</Tag>
        <Tag v-else-if="row.status === 'PROCESSING'" color="processing">변환 중</Tag>
        <Tooltip v-else-if="row.status === 'FAILED'">
          <template #title>
            <div class="text-xs space-y-1 p-1 max-w-[360px] break-all">
              <p><strong>오류 메시지:</strong> {{ row.errorMessage || '상세 에러 정보 없음' }}</p>
              <div v-if="row.conversionCommand" class="mt-1">
                <strong>실행 명령:</strong>
                <pre class="bg-black/20 p-1 rounded text-[10px] whitespace-pre-wrap select-all font-mono">{{ row.conversionCommand }}</pre>
              </div>
              <p v-if="row.conversionStartedAt"><strong>시작 시간:</strong> {{ formatDate(row.conversionStartedAt) }}</p>
              <p v-if="row.conversionCompletedAt"><strong>완료 시간:</strong> {{ formatDate(row.conversionCompletedAt) }}</p>
            </div>
          </template>
          <Tag color="error" class="cursor-help">변환 실패</Tag>
        </Tooltip>
        <Tag v-else color="default">{{ row.status }}</Tag>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="handlePlay(row)" title="음원 재생 (AAC 변환본 우선, 없으면 원본)">
            <IconifyIcon icon="lucide:play" class="size-4" />
          </Button>
          <Button type="link" size="small" @click="handleEdit(row)" title="음원 정보 수정">
            <IconifyIcon icon="lucide:edit-3" class="size-4" />
          </Button>
          <Button v-if="row.hasOgg && row.oggUrl" type="link" size="small" @click="handlePlayOgg(row)" title="변환 음원(OGG) 청취">
            <IconifyIcon icon="lucide:music" class="size-4 text-success" />
          </Button>
          <!--
            커버 재추출·음원 재변환은 변환 실패건에서만 보였는데, 그러면 상태가 COMPLETED 인
            행에서는 손댈 방법이 없었다. 커버가 비어 있거나 변환본을 다시 만들고 싶을 때도
            필요하므로 상태와 무관하게 늘 보여 준다. 영상 화면의 재변환 버튼과 같은 방식이다.
          -->
          <Button type="link" size="small" @click="handleRetryCover(row)" title="커버이미지 재추출 (원본에 앨범아트가 있어야 나온다)">
            <IconifyIcon icon="lucide:image" class="size-4" />
          </Button>
          <Popconfirm
            title="이 음원을 OGG/AAC 로 다시 변환하시겠습니까? 변환에 시간이 걸릴 수 있습니다."
            @confirm="handleRetryAudio(row)"
          >
            <Button type="link" size="small" title="음원(OGG/AAC) 재변환">
              <IconifyIcon icon="lucide:refresh-cw" class="size-4" />
            </Button>
          </Popconfirm>
          <Popconfirm title="해당 음원을 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger>
              <IconifyIcon icon="lucide:trash-2" class="size-4" />
            </Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- 음원 등록/업로드 모달 컴포넌트 -->
    <AudioUploadModal ref="uploadModalRef" @saved="gridApi.query()" />

    <!-- 오디오 플레이어 모달 -->
    <Modal
      v-model:open="showPlayModal"
      :title="`음원 청취 플레이어 - ${currentAudioName}`"
      :footer="null"
      destroy-on-close
      @cancel="handleClosePlayer"
      width="400px"
    >
      <div class="p-6 flex flex-col items-center gap-4 bg-accent/20 rounded border">
        <!-- 앨범 아트워크 표출. 못 받아오면 깨진 아이콘 대신 🎵 로 떨어진다. -->
        <ImagePreview :src="currentAudioThumbnail || null" :width="192" :height="192">
          <template #fallback>
            <span class="text-4xl">🎵</span>
          </template>
        </ImagePreview>
        <div class="text-sm font-semibold text-center truncate max-w-full text-primary mt-2">{{ currentAudioName }}</div>
        <!-- key 를 걸어 후보가 바뀌면 audio 요소를 새로 만든다 (src 만 갈면 다시 안 읽는 경우가 있다) -->
        <audio
          v-if="currentAudioUrl && !playError"
          :key="currentAudioUrl"
          :src="currentAudioUrl"
          controls
          autoplay
          preload="metadata"
          class="w-full mt-2"
          @error="handleAudioError"
        ></audio>
        <p v-else class="text-xs text-muted-foreground text-center px-2">
          {{ playError || '재생할 음원이 없습니다.' }}
        </p>
      </div>
    </Modal>
  </Page>
</template>
