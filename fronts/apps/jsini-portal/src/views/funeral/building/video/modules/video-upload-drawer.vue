<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenDrawer } from '@vben/common-ui';
import { Button, message, Form, Input, Upload, InputNumber, Progress } from 'ant-design-vue';
import { createMediaSource, updateMediaSource } from '#/api/funeral/building';
import { upload_file } from '#/api/examples/upload';
import type { UploadChangeParam } from 'ant-design-vue';

const emit = defineEmits<{
  (e: 'saved'): void;
}>();

const isUploading = ref<boolean>(false);
const uploadPercent = ref<number>(0);
const selectedFileName = ref<string>('');
const isEditMode = ref<boolean>(false);
const currentSourceId = ref<string>('');

const formModel = ref({
  name: '',
  shortName: '',
  sourceType: 'VIDEO' as const,
  url: '',
  thumbnailUrl: '',
  thumbnailFileId: null as string | null,
  sortOrder: 0,
  remark: ''
});

const [UploadDrawer, uploadDrawerApi] = useVbenDrawer({
  title: '새 비디오 리소스 등록',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

function open(row?: any) {
  if (row) {
    isEditMode.value = true;
    currentSourceId.value = row.id;
    formModel.value = {
      name: row.name || '',
      shortName: row.shortName || '',
      sourceType: 'VIDEO',
      url: row.url || '',
      thumbnailUrl: row.thumbnailUrl || '',
      thumbnailFileId: row.thumbnailFileId || null,
      sortOrder: row.sortOrder || 0,
      remark: row.remark || ''
    };
    selectedFileName.value = '';
    uploadPercent.value = 0;
    uploadDrawerApi.setState({ title: '동영상 리소스 수정' });
  } else {
    isEditMode.value = false;
    currentSourceId.value = '';
    formModel.value = {
      name: '',
      shortName: '',
      sourceType: 'VIDEO',
      url: '',
      thumbnailUrl: '',
      thumbnailFileId: null,
      sortOrder: 0,
      remark: ''
    };
    selectedFileName.value = '';
    uploadPercent.value = 0;
    uploadDrawerApi.setState({ title: '새 비디오 리소스 등록' });
  }
  uploadDrawerApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.url) {
      message.warning('동영상 명칭과 동영상 파일은 필수 사항입니다.');
      return;
    }

    uploadDrawerApi.lock();
    if (isEditMode.value) {
      await updateMediaSource(currentSourceId.value, formModel.value);
      message.success('동영상 소스가 성공적으로 수정되었습니다.');
    } else {
      await createMediaSource(formModel.value);
      message.success('동영상 소스가 성공적으로 등록되었습니다.');
    }
    uploadDrawerApi.close();
    emit('saved');
  } catch (error) {
    message.error(isEditMode.value ? '비디오 리소스 수정 실패' : '비디오 리소스 등록 실패');
  } finally {
    uploadDrawerApi.unlock();
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
      bizType: 'funeralv2',
      onProgress: (event) => {
        uploadPercent.value = Math.round(event.percent);
        options.onProgress(event);
      },
      onSuccess: (res, file) => {
        const fileItem = res?.result?.[0] || res?.[0] || res;
        const fileUrl = fileItem?.downloadUrl || fileItem?.url;
        const thumbnailFileId = fileItem?.thumbnailFileId;
        const thumbnailUrl = fileItem?.thumbnailUrl;

        if (fileUrl && typeof fileUrl === 'string') {
          formModel.value.url = fileUrl;
          if (thumbnailUrl) {
            formModel.value.thumbnailUrl = thumbnailUrl;
            formModel.value.thumbnailFileId = thumbnailFileId;
          }
          
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
  <UploadDrawer :confirm-loading="isUploading">
    <div class="p-2">
      <Form layout="vertical">
        <!-- 등록 모드일 때만 업로드 기능 허용 -->
        <Form.Item v-if="!isEditMode" label="영상 파일 업로드 (최대 500MB)" required>
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

          <div v-if="formModel.url" class="mt-2 text-xs text-muted-foreground break-all bg-muted p-3 rounded border flex gap-3 items-center">
            <img v-if="formModel.thumbnailUrl" :src="formModel.thumbnailUrl" class="w-24 h-16 object-cover rounded border shadow-sm" alt="추출된 썸네일" />
            <div v-else class="w-24 h-16 bg-black/5 rounded border flex items-center justify-center text-[10px]">썸네일 없음</div>
            <div class="flex-1 space-y-1">
              <div>업로드된 경로: {{ formModel.url }}</div>
              <div class="text-primary font-medium text-[10px]">※ 비디오 업로드 후 썸네일이 즉시 추출되었습니다. WebM 변환은 저장 후 백그라운드에서 실행됩니다.</div>
            </div>
          </div>
        </Form.Item>

        <!-- 수정 모드일 때 업로드 경로 정보만 조회 -->
        <Form.Item v-else label="등록된 영상 파일 정보">
          <div class="mt-2 text-xs text-muted-foreground break-all bg-muted p-3 rounded border flex gap-3 items-center">
            <img v-if="formModel.thumbnailUrl" :src="formModel.thumbnailUrl" class="w-24 h-16 object-cover rounded border shadow-sm" alt="추출된 썸네일" />
            <div v-else class="w-24 h-16 bg-black/5 rounded border flex items-center justify-center text-[10px]">썸네일 없음</div>
            <div class="flex-1 space-y-1">
              <div>파일 경로: {{ formModel.url }}</div>
              <div class="text-primary font-medium text-[10px]">※ 수정 모드에서는 원본 영상 파일을 변경할 수 없습니다.</div>
            </div>
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
  </UploadDrawer>
</template>
