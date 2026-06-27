<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Modal, Tag } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource } from '#/api/building';
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

async function handleDelete(row: any) {
  try {
    await deleteMediaSource(row.id);
    message.success('동영상 소스가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

// 플레이 시뮬레이터 팝업
function handlePlay(row: any) {
  currentVideoUrl.value = row.url;
  currentVideoName.value = row.name;
  showPlayModal.value = true;
}

function handleClosePlayer() {
  currentVideoUrl.value = '';
  showPlayModal.value = false;
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
        <div class="flex items-center justify-center p-1 bg-muted rounded border overflow-hidden w-16 h-10">
          <img 
            v-if="row.thumbnailUrl"
            :src="row.thumbnailUrl" 
            class="w-full h-full object-cover" 
            alt="thumb" 
            @error="(e: any) => { if (e.target) e.target.style.display = 'none'; }"
          />
          <span v-else class="text-[10px] text-muted-foreground font-mono">No Image</span>
        </div>
      </template>

      <!-- 변환 상태 컬럼 슬롯 렌더러 -->
      <template #status="{ row }">
        <Tag v-if="row.status === 'COMPLETED'" color="success">완료</Tag>
        <Tag v-else-if="row.status === 'READY'" color="ready">준비</Tag>
        <Tag v-else-if="row.status === 'PROCESSING'" color="processing">변환 중</Tag>
        <Tag v-else-if="row.status === 'FAILED'" color="error">변환 실패</Tag>
        <Tag v-else color="default">{{ row.status }}</Tag>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="handlePlay(row)">재생 미리보기</Button>
          <Popconfirm title="해당 동영상을 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
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

