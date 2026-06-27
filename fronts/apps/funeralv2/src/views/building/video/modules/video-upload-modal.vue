<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenModal } from '@vben/common-ui';
import { Button, message, Form, Input, Upload, InputNumber, Progress } from 'ant-design-vue';
import { createMediaSource } from '#/api/building';
import { upload_file } from '#/api/examples/upload';
import type { UploadChangeParam } from 'ant-design-vue';

const emit = defineEmits<{
  (e: 'saved'): void;
}>();

const isUploading = ref<boolean>(false);
const uploadPercent = ref<number>(0);
const selectedFileName = ref<string>('');

const formModel = ref({
  name: '',
  shortName: '',
  sourceType: 'VIDEO' as const,
  url: '',
  thumbnailUrl: '',
  sortOrder: 0,
  remark: ''
});

const [UploadModal, uploadModalApi] = useVbenModal({
  title: '새 비디오 리소스 등록',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

function open() {
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
  uploadModalApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.url) {
      message.warning('동영상 명칭과 동영상 파일은 필수 사항입니다.');
      return;
    }

    uploadModalApi.lock();
    await createMediaSource(formModel.value);
    message.success('동영상 소스가 성공적으로 등록되었습니다.');
    uploadModalApi.close();
    emit('saved');
  } catch (error) {
    message.error('비디오 리소스 등록 실패');
  } finally {
    uploadModalApi.unlock();
  }
}

// 파일 업로드 처리
async function customUploadRequest(options: any) {
  isUploading.value = true;
  uploadPercent.value = 0;
  selectedFileName.value = options.file.name;

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
        const fileItem = res?.result?.[0] || res?.[0] || res;
        const fileUrl = fileItem?.downloadUrl || fileItem?.url;

        if (fileUrl && typeof fileUrl === 'string') {
          formModel.value.url = fileUrl;
          
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

defineExpose({ open });
</script>

<template>
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

          <div v-if="formModel.url" class="mt-2 text-xs text-muted-foreground break-all bg-muted p-2 rounded">
            <div>업로드된 경로: {{ formModel.url }}</div>
            <div class="mt-1 text-primary font-medium">※ 비디오 썸네일 및 WebM 변환은 저장 완료 시점 이후에 비동기로 순차 진행됩니다.</div>
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
</template>
