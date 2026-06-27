<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Modal, Image } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource } from '#/api/building';
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
        <div class="flex items-center justify-center p-0.5">
          <Image
            v-if="row.thumbnailUrl"
            :src="row.thumbnailUrl"
            :width="40"
            :height="40"
            class="object-cover rounded shadow border cursor-zoom-in"
            alt="Cover"
          />
          <div
            v-else
            class="w-10 h-10 bg-muted rounded flex items-center justify-center text-xs text-muted-foreground border"
          >
            🎵
          </div>
        </div>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="handlePlay(row)">음원 청취</Button>
          <Popconfirm title="해당 음원을 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
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
