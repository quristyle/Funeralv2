<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Modal } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, createMediaSource, deleteMediaSource } from '#/api/building';

const [UploadModal, uploadModalApi] = useVbenModal({
  title: '새 음원 리소스 등록',
  destroyOnClose: true,
});

const showPlayModal = ref<boolean>(false);
const currentAudioUrl = ref<string>('');
const currentAudioName = ref<string>('');
const formModel = ref({
  name: '',
  sourceType: 'AUDIO' as const,
  url: '',
  remark: ''
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '음원/배경음악 명칭', minWidth: 180 },
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
  formModel.value = {
    name: '',
    sourceType: 'AUDIO',
    url: '',
    remark: ''
  };
  uploadModalApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.url) {
      message.warning('음원 명칭과 음원 URL 경로는 필수 기입 사항입니다.');
      return;
    }
    await createMediaSource(formModel.value);
    message.success('음원 소스가 성공적으로 등록되었습니다.');
    uploadModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('음원 리소스 등록 실패');
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
  showPlayModal.value = true;
}

function handleClosePlayer() {
  currentAudioUrl.value = '';
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

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="handlePlay(row)">음원 청취</Button>
          <Popconfirm title="해당 음원을 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <UploadModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="음원 명칭" required>
            <Input v-model:value="formModel.name" placeholder="예: 상례 추모 음악 1번, 관내 백그라운드 재즈" />
          </Form.Item>
          <Form.Item label="음원 URL 경로" required>
            <Input v-model:value="formModel.url" placeholder="예: https://cdn.funeralv2.com/audio/mourn01.mp3" />
          </Form.Item>
          <Form.Item label="설명/비고">
            <Input.TextArea v-model:value="formModel.remark" placeholder="음원 장르 및 상세 용도 작성" />
          </Form.Item>
        </Form>
      </div>
    </UploadModal>

    <!-- 오디오 플레이어 모달 -->
    <Modal
      v-model:open="showPlayModal"
      :title="`음원 청취 플레이어 - ${currentAudioName}`"
      :footer="null"
      destroy-on-close
      @cancel="handleClosePlayer"
      width="400px"
    >
      <div class="p-6 flex flex-col items-center gap-4 bg-accent rounded">
        <div class="text-sm font-semibold text-center truncate max-w-full text-primary">{{ currentAudioName }}</div>
        <audio
          v-if="currentAudioUrl"
          :src="currentAudioUrl"
          controls
          autoplay
          class="w-full"
        ></audio>
      </div>
    </Modal>
  </Page>
</template>
