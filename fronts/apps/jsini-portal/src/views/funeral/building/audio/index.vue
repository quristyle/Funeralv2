<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Plus, IconifyIcon } from '@vben/icons';
import { Button, message, Popconfirm, Modal, Tag, Tooltip } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource, retryThumbnail, retryAudio } from '#/api/building';
import ImagePreview from '#/components/ImagePreview.vue';
import AudioUploadModal from './modules/audio-upload-modal.vue';

const uploadModalRef = ref<InstanceType<typeof AudioUploadModal> | null>(null);

const showPlayModal = ref<boolean>(false);
const currentAudioUrl = ref<string>('');
const currentAudioName = ref<string>('');
const currentAudioThumbnail = ref<string>('');

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

function handlePlayOgg(row: any) {
  currentAudioUrl.value = row.oggUrl;
  currentAudioName.value = row.name + ' (OGG)';
  currentAudioThumbnail.value = row.thumbnailUrl || '';
  showPlayModal.value = true;
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

// 오디오 재생 팝업
function handlePlay(row: any) {
  currentAudioUrl.value = row.url;
  currentAudioName.value = row.name;
  currentAudioThumbnail.value = row.thumbnailUrl || '';
  showPlayModal.value = true;
}

function handleClosePlayer() {
  currentAudioUrl.value = '';
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
        <Button type="primary" @click="openUpload">
          <Plus class="size-5 mr-1" />
          신규 음원 등록
        </Button>
      </template>

      <template #thumbnail="{ row }">
        <ImagePreview
          :src="row.thumbnailUrl"
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
          <Button type="link" size="small" @click="handlePlay(row)" title="원본 음원 재생">
            <IconifyIcon icon="lucide:play" class="size-4" />
          </Button>
          <Button type="link" size="small" @click="handleEdit(row)" title="음원 정보 수정">
            <IconifyIcon icon="lucide:edit-3" class="size-4" />
          </Button>
          <Button v-if="row.hasOgg && row.oggUrl" type="link" size="small" @click="handlePlayOgg(row)" title="변환 음원(OGG) 청취">
            <IconifyIcon icon="lucide:music" class="size-4 text-success" />
          </Button>
          <Button v-if="row.status === 'FAILED'" type="link" size="small" @click="handleRetryCover(row)" title="커버이미지 재추출">
            <IconifyIcon icon="lucide:image" class="size-4" />
          </Button>
          <Button v-if="row.status === 'FAILED'" type="link" size="small" @click="handleRetryAudio(row)" title="음원(OGG/AAC) 재변환">
            <IconifyIcon icon="lucide:refresh-cw" class="size-4" />
          </Button>
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
        <!-- 앨범 아트워크 표출 -->
        <img
          v-if="currentAudioThumbnail"
          :src="currentAudioThumbnail"
          class="w-48 h-48 object-cover rounded-lg shadow-lg border-2 border-primary/10"
          alt="Album Cover"
        />
        <div
          v-else
          class="w-48 h-48 bg-muted rounded-lg flex items-center justify-center border text-muted-foreground text-4xl shadow"
        >
          🎵
        </div>
        <div class="text-sm font-semibold text-center truncate max-w-full text-primary mt-2">{{ currentAudioName }}</div>
        <audio
          v-if="currentAudioUrl"
          :src="currentAudioUrl"
          controls
          autoplay
          class="w-full mt-2"
        ></audio>
      </div>
    </Modal>
  </Page>
</template>
