<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Modal, Upload, InputNumber, Progress, Card } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, createMediaSource, deleteMediaSource } from '#/api/building';
import { upload_file } from '#/api/examples/upload';
import type { UploadChangeParam } from 'ant-design-vue';

const [UploadModal, uploadModalApi] = useVbenModal({
  title: '새 비디오 리소스 등록',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

const showPlayModal = ref<boolean>(false);
const currentVideoUrl = ref<string>('');
const currentVideoName = ref<string>('');
const isUploading = ref<boolean>(false);
const uploadPercent = ref<number>(0);
const selectedFileName = ref<string>('');
const videoThumbnailUrl = ref<string>('');

const formModel = ref({
  name: '',
  shortName: '',
  sourceType: 'VIDEO' as const,
  url: '',
  thumbnailUrl: '',
  sortOrder: 0,
  remark: ''
});

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
  formModel.value = {
    name: '',
    shortName: '',
    sourceType: 'VIDEO',
    url: '',
    thumbnailUrl: '',
    sortOrder: 0,
    remark: ''
  };
  selectedFileName.value = '';
  uploadPercent.value = 0;
  videoThumbnailUrl.value = '';
  uploadModalApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.url) {
      message.warning('동영상 명칭과 동영상 파일은 필수 사항입니다.');
      return;
    }
    // 업로드 완료 시 추출된 썸네일 URL 맵핑
    formModel.value.thumbnailUrl = videoThumbnailUrl.value;

    uploadModalApi.lock();
    await createMediaSource(formModel.value);
    message.success('동영상 소스가 성공적으로 등록되었습니다.');
    uploadModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('비디오 리소스 등록 실패');
  } finally {
    uploadModalApi.unlock();
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

// 파일 업로드 처리
async function customUploadRequest(options: any) {
  isUploading.value = true;
  uploadPercent.value = 0;
  selectedFileName.value = options.file.name;
  videoThumbnailUrl.value = '';

  const fileLimit = 500 * 1024 * 1024; // 500MB 한도
  if (options.file.size > fileLimit) {
    message.error('업로드 가능한 비디오 파일 최대 용량은 500MB입니다.');
    options.onError(new Error('File size limit exceeded'));
    isUploading.value = false;
    return;
  }

  try {
    await upload_file({
      file: options.file,
      onProgress: (event) => {
        uploadPercent.value = Math.round(event.percent);
        options.onProgress(event);
      },
      onSuccess: (res, file) => {
        // 공통 DTO 규격인 result 배열 래핑 구조 해제 (res.result?.[0] 패턴 우선 적용)
        const fileItem = res?.result?.[0] || res?.[0] || res;
        const fileUrl = fileItem?.downloadUrl || fileItem?.url;
        const thumbUrl = fileItem?.thumbnailLink || (fileUrl ? `${fileUrl}.jpg` : '');

        if (fileUrl && typeof fileUrl === 'string') {
          formModel.value.url = fileUrl;
          videoThumbnailUrl.value = thumbUrl;
          
          if (!formModel.value.name) {
            formModel.value.name = file.name.substring(0, file.name.lastIndexOf('.')) || file.name;
          }
          message.success('파일 업로드에 성공했습니다.');
          options.onSuccess(res, file);
        } else {
          message.error('파일 업로드 응답 형식이 올바르지 않습니다.');
          options.onError(new Error('Invalid response structure'));
        }
      },
      onError: (err) => {
        message.error('파일 업로드 중 오류가 발생했습니다.');
        options.onError(err);
      }
    });
  } catch (error) {
    message.error('파일 업로드 실패');
    options.onError(error);
  } finally {
    isUploading.value = false;
  }
}

function handleUploadChange(info: UploadChangeParam) {
  if (info.file.status === 'uploading') {
    isUploading.value = true;
  } else {
    isUploading.value = false;
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
        <div class="flex items-center justify-center p-1 bg-muted rounded border overflow-hidden w-16 h-10">
          <img 
            v-if="row.thumbnailUrl"
            :src="row.thumbnailUrl" 
            class="w-full h-full object-cover" 
            alt="thumb" 
            @error="(e: any) => { e.target.style.display = 'none'; }"
          />
          <span v-else class="text-[10px] text-muted-foreground font-mono">No Image</span>
        </div>
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

    <UploadModal :confirm-loading="isUploading">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="영상 파일 업로드 (최대 500MB)" required>
            <Upload
              accept="video/mp4,video/mkv,video/avi,video/webm"
              :custom-request="customUploadRequest"
              :show-upload-list="false"
              @change="handleUploadChange"
            >
              <Button :loading="isUploading" type="dashed" class="w-full">
                {{ isUploading ? '서버로 업로드 중...' : '클릭하여 동영상 파일 선택' }}
              </Button>
            </Upload>
            
            <!-- 선택 파일 정보 및 업로드 진행 상태바 -->
            <div v-if="selectedFileName" class="mt-3 p-3 bg-muted rounded border flex flex-col gap-2">
              <div class="text-xs flex justify-between font-medium text-muted-foreground">
                <span class="truncate max-w-[80%]">선택한 파일: {{ selectedFileName }}</span>
                <span>{{ uploadPercent }}%</span>
              </div>
              <Progress :percent="uploadPercent" size="small" status="active" />
            </div>

            <!-- 추출된 첫 클립 이미지 미리보기 카드 -->
            <div v-if="videoThumbnailUrl" class="mt-4">
              <span class="text-xs font-semibold text-muted-foreground block mb-2">추출된 첫 클립 (썸네일)</span>
              <Card size="small" :bordered="true" class="overflow-hidden bg-black flex justify-center items-center">
                <img 
                  :src="videoThumbnailUrl" 
                  alt="Video Thumbnail" 
                  class="max-h-[160px] object-contain rounded"
                  @error="(e: any) => { e.target.style.display = 'none'; }"
                />
              </Card>
            </div>

            <div v-if="formModel.url" class="mt-2 text-xs text-muted-foreground break-all bg-muted p-2 rounded">
              업로드된 경로: {{ formModel.url }}
            </div>
          </Form.Item>

          <Form.Item label="동영상 명칭" required>
            <Input v-model:value="formModel.name" placeholder="예: [안내] 장례식장 이용안내 영상" />
          </Form.Item>

          <Form.Item label="영상 짧은 명칭">
            <Input v-model:value="formModel.shortName" placeholder="예: 이용안내" />
          </Form.Item>

          <Form.Item label="순서">
            <InputNumber v-model:value="formModel.sortOrder" :min="0" class="w-full" placeholder="정렬 순서" />
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
