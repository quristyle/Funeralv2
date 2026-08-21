<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Plus ,IconifyIcon} from '@vben/icons';
import { Button, message, Popconfirm, Modal, Tag, Tooltip } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource, retryThumbnail, retryWebm } from '#/api/funeral/building';
import ImagePreview from '#/components/ImagePreview.vue';
import VideoUploadModal from './modules/video-upload-modal.vue';

const uploadModalRef = ref<InstanceType<typeof VideoUploadModal> | null>(null);

const showPlayModal = ref<boolean>(false);
const currentVideoUrl = ref<string>('');
const currentVideoName = ref<string>('');

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

// 플레이 시뮬레이터 팝업 (원본)
function handlePlay(row: any) {
  currentVideoUrl.value = row.url;
  currentVideoName.value = row.name;
  showPlayModal.value = true;
}

// 플레이 시뮬레이터 팝업 (보관용 변환본 / H.264 MP4)
// 필드명 webmUrl 은 과거 WebM 보관 시절의 이름이며, 실제 파일은 H.264/MP4 이다.
function handlePlayWebm(row: any) {
  currentVideoUrl.value = row.webmUrl;
  currentVideoName.value = row.name + ' (H.264)';
  showPlayModal.value = true;
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
  currentVideoUrl.value = '';
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
        <Button type="primary" @click="openUpload">
          <Plus class="size-5 mr-1" />
          신규 비디오 등록
        </Button>
      </template>

      <!-- 썸네일 컬럼 슬롯 렌더러 -->
      <template #thumbnail="{ row }">
        <ImagePreview
          :src="row.thumbnailUrl"
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
          <Button type="link" size="small" @click="handlePlay(row)" title="원본 영상 재생">
            <IconifyIcon icon="lucide:play" class="size-4" />
          </Button>
          <Button type="link" size="small" @click="handleEdit(row)" title="동영상 정보 수정">
            <IconifyIcon icon="lucide:edit-3" class="size-4" />
          </Button>
          <Button v-if="row.hasWebm && row.webmUrl" type="link" size="small" @click="handlePlayWebm(row)" title="변환 영상(H.264) 재생">
            <IconifyIcon icon="lucide:film" class="size-4 text-success" />
          </Button>
          <Button v-if="row.status === 'FAILED'" type="link" size="small" @click="handleRetryThumbnail(row)" title="썸네일 재추출">
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
      <div class="p-2 flex justify-center bg-black rounded">
        <video
          v-if="currentVideoUrl"
          :src="currentVideoUrl"
          controls
          autoplay
          class="w-full max-h-[360px]"
        ></video>
      </div>
    </Modal>

  </Page>
</template>

