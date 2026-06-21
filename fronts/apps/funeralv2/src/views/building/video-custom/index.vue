<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Modal } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, createMediaSource, deleteMediaSource } from '#/api/building';

const [UploadModal, uploadModalApi] = useVbenModal({
  title: '새 비디오 리소스 등록',
  destroyOnClose: true,
});

const showPlayModal = ref<boolean>(false);
const currentVideoUrl = ref<string>('');
const currentVideoName = ref<string>('');
const formModel = ref({
  name: '',
  sourceType: 'VIDEO' as const,
  url: '',
  remark: ''
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '동영상 명칭', minWidth: 180 },
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
  formModel.value = {
    name: '',
    sourceType: 'VIDEO',
    url: '',
    remark: ''
  };
  uploadModalApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.url) {
      message.warning('동영상 명칭과 동영상 URL 경로는 필수 기입 사항입니다.');
      return;
    }
    await createMediaSource(formModel.value);
    message.success('동영상 소스가 성공적으로 등록되었습니다.');
    uploadModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('비디오 리소스 등록 실패');
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

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="handlePlay(row)">재생 미리보기</Button>
          <Popconfirm title="해당 동영상을 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <UploadModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="동영상 명칭" required>
            <Input v-model:value="formModel.name" placeholder="예: [안내] 장례식장 이용안내 영상" />
          </Form.Item>
          <Form.Item label="동영상 URL 경로" required>
            <Input v-model:value="formModel.url" placeholder="예: https://cdn.funeralv2.com/video/guide.mp4" />
          </Form.Item>
          <Form.Item label="설명/비고">
            <Input.TextArea v-model:value="formModel.remark" placeholder="동영상 내용 및 타겟 DID 위치 등 적재" />
          </Form.Item>
        </Form>
      </div>
    </UploadModal>

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
