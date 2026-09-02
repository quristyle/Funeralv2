<script lang="ts" setup>
import { computed, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon} from '@vben/icons';
import { Button, message, Popconfirm, Modal, Tag, Tooltip } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource, retryThumbnail, retryWebm } from '#/api/funeral/building';
import ImagePreview from '#/components/ImagePreview.vue';
import VideoUploadModal from './modules/video-upload-modal.vue';

const uploadModalRef = ref<InstanceType<typeof VideoUploadModal> | null>(null);

const showPlayModal = ref<boolean>(false);
const currentVideoName = ref<string>('');
const currentVideoPoster = ref<string>('');

/**
 * 재생할 URL 후보 목록. 앞의 것부터 시도하고 브라우저가 못 받거나 못 읽으면 다음으로 넘어간다.
 * 변환본(H.264)이 원본보다 훨씬 가볍기 때문에 앞에 둔다 — 원본은 4K 200MB 짜리도 있어서
 * 미리보기로 통째로 받으면 한참 검은 화면만 보인다.
 */
const playSources = ref<string[]>([]);
const playIndex = ref<number>(0);
/** 후보를 다 써도 재생하지 못했을 때 사용자에게 보일 사유 */
const playError = ref<string>('');

const currentVideoUrl = computed<string>(() => playSources.value[playIndex.value] ?? '');

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'thumbnailUrl',
        title: '썸네일',
        width: 100,
        slots: { default: 'thumbnail' }
      },
      { field: 'name', title: '동영상 명칭', minWidth: 180 },
      { field: 'shortName', title: '영상 짧은 명칭', minWidth: 120 },
      {
        field: 'status',
        title: '변환 상태',
        width: 120,
        slots: { default: 'status' }
      },
      { field: 'sortOrder', title: '순서', width: 80 },
      { field: 'url', title: '동영상 URL 경로', minWidth: 280 },
      { field: 'remark', title: '설명', minWidth: 200 },
      {
        field: 'action',
        title: '미리보기 및 관리',
        width: 200,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getMediaSources('VIDEO');
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

async function handleDelete(row: any) {
  try {
    await deleteMediaSource(row.id);
    message.success('동영상 소스가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

/**
 * 그리드 썸네일 칸에 쓸 축소본 경로.
 *
 * `thumbnailUrl` 은 원본 내려받기 경로(`/api/file/download/...`)라 64x40 자리에
 * 수 MB 짜리 JPEG 이 그대로 온다. 파일 아이디를 알면 150px 축소본을 쓴다.
 * 축소본이 없거나 실패하면 ImagePreview 가 `fallback-src` 로 원본을 다시 시도한다.
 */
function thumbnailSrc(row: any): string | null {
  return row.thumbnailFileId
    ? `/api/file/thumbnail/${row.thumbnailFileId}`
    : (row.thumbnailUrl ?? null);
}

/**
 * 플레이어 팝업을 연다.
 *
 * @param row      대상 행
 * @param sources  재생할 URL 후보 (앞에서부터 시도)
 * @param suffix   제목 뒤에 붙일 표기
 */
function openPlayer(row: any, sources: (string | null | undefined)[], suffix = '') {
  const usable = sources.filter((url): url is string => !!url);
  playSources.value = usable;
  playIndex.value = 0;
  playError.value = usable.length === 0 ? '재생할 수 있는 파일 경로가 없습니다.' : '';
  currentVideoPoster.value = row.thumbnailUrl ?? '';
  currentVideoName.value = row.name + suffix;
  showPlayModal.value = true;
}

// 플레이 시뮬레이터 팝업 (기본). 가벼운 변환본을 먼저 시도하고 없으면 원본으로 간다.
// 필드명 webmUrl 은 과거 WebM 보관 시절의 이름이며, 실제 파일은 H.264/MP4 이다.
function handlePlay(row: any) {
  openPlayer(row, [row.hasWebm ? row.webmUrl : null, row.url]);
}

// 플레이 시뮬레이터 팝업 (보관용 변환본 / H.264 MP4). 변환본만 확인할 때 쓴다.
function handlePlayWebm(row: any) {
  openPlayer(row, [row.webmUrl], ' (H.264)');
}

/**
 * 브라우저가 현재 후보를 재생하지 못했을 때. 다음 후보가 있으면 넘어가고,
 * 없으면 사유를 적어 둔다 — 검은 화면만 남는 것을 막는다.
 */
function handleVideoError() {
  if (playIndex.value < playSources.value.length - 1) {
    playIndex.value += 1;
    return;
  }
  playError.value =
    '영상을 재생하지 못했습니다. 파일이 서버에 없거나 브라우저가 지원하지 않는 코덱일 수 있습니다.';
}

// 썸네일 재추출 처리
async function handleRetryThumbnail(row: any) {
  try {
    await retryThumbnail(row.id);
    message.success('썸네일 재추출이 요청되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('재추출 요청 실패');
  }
}

// 보관용 변환본(H.264) 재변환 처리
async function handleRetryWebm(row: any) {
  try {
    await retryWebm(row.id);
    message.success('영상 재변환(H.264)이 요청되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('재변환 요청 실패');
  }
}

function handleClosePlayer() {
  playSources.value = [];
  playIndex.value = 0;
  playError.value = '';
  currentVideoPoster.value = '';
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
    <Grid table-title="DID 화면 재생용 동영상 소스 목록">
      <template #toolbar-tools>
        <GridIconButton
          icon="vxe-icon-add"
          title="신규 비디오 등록"
          @click="openUpload"
        />
      </template>

      <!-- 썸네일 컬럼 슬롯 렌더러 -->
      <template #thumbnail="{ row }">
        <ImagePreview
          :src="thumbnailSrc(row)"
          :fallback-src="row.thumbnailUrl"
          :preview-src="row.thumbnailUrl"
          :width="64"
          :height="40"
          fallback-text="No Image"
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
          <Button type="link" size="small" @click="handlePlay(row)" title="영상 재생 (변환본 우선, 없으면 원본)">
            <IconifyIcon icon="lucide:play" class="size-4" />
          </Button>
          <Button type="link" size="small" @click="handleEdit(row)" title="동영상 정보 수정">
            <IconifyIcon icon="lucide:edit-3" class="size-4" />
          </Button>
          <Button v-if="row.hasWebm && row.webmUrl" type="link" size="small" @click="handlePlayWebm(row)" title="변환 영상(H.264) 재생">
            <IconifyIcon icon="lucide:film" class="size-4 text-success" />
          </Button>
          <!--
            썸네일 재추출은 변환 실패건에서만 보였는데, 그러면 상태가 COMPLETED 인데
            썸네일만 비어 있는 행은 손댈 방법이 없었다. 상태와 무관하게 늘 보여 준다.
          -->
          <Button type="link" size="small" @click="handleRetryThumbnail(row)" title="썸네일 재추출">
            <IconifyIcon icon="lucide:image" class="size-4" />
          </Button>
          <!--
            H.264 재변환. 변환 실패건 복구뿐 아니라 기존 WebM 자산을 H.264 로
            다시 만들 때도 필요하므로 상태와 무관하게 항상 노출한다.
            재인코딩은 수 분이 걸리는 무거운 작업이라 확인 절차를 둔다.
          -->
          <Popconfirm
            title="이 영상을 H.264로 다시 변환하시겠습니까? 변환에 수 분이 걸릴 수 있습니다."
            @confirm="handleRetryWebm(row)"
          >
            <Button type="link" size="small" title="H.264로 재변환">
              <IconifyIcon icon="lucide:refresh-cw" class="size-4" />
            </Button>
          </Popconfirm>
          <Popconfirm title="해당 동영상을 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger>
              <IconifyIcon icon="lucide:trash-2" class="size-4" />
            </Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- 비디오 등록/업로드 모달 컴포넌트 -->
    <VideoUploadModal ref="uploadModalRef" @saved="gridApi.query()" />

    <!-- 비디오 플레이어 모달 -->
    <Modal
      v-model:open="showPlayModal"
      :title="`비디오 플레이어 - ${currentVideoName}`"
      :footer="null"
      destroy-on-close
      @cancel="handleClosePlayer"
      width="640px"
    >
      <div class="p-2 flex justify-center bg-black rounded min-h-[200px] items-center">
        <!--
          key 를 걸어 후보가 바뀌면 video 요소를 새로 만든다 — src 만 갈면
          브라우저가 다시 읽지 않는 경우가 있다.
          poster 로 썸네일을 깔아 두면 자동재생이 브라우저 정책에 막혀도 검은 화면 대신
          첫 장면이 보인다.
        -->
        <video
          v-if="currentVideoUrl && !playError"
          :key="currentVideoUrl"
          :src="currentVideoUrl"
          :poster="currentVideoPoster || undefined"
          controls
          autoplay
          playsinline
          preload="metadata"
          class="w-full max-h-[360px]"
          @error="handleVideoError"
        ></video>
        <p v-else class="text-sm text-white/80 text-center px-4 py-8">
          {{ playError || '재생할 영상이 없습니다.' }}
        </p>
      </div>
    </Modal>

  </Page>
</template>

