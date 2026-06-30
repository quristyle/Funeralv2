<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenModal } from '@vben/common-ui';
import { Button, message, Form, Input, Upload, Progress } from 'ant-design-vue';
import { createMediaSource, updateMediaSource } from '#/api/building';
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
  sourceType: 'IMAGE' as const, // IMAGE 고정
  url: '',
  thumbnailUrl: '',
  thumbnailFileId: null as string | null,
  sortOrder: 0,
  remark: ''
});

const [UploadModal, uploadModalApi] = useVbenModal({
  title: '새 장식 리소스 등록',
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
      sourceType: 'IMAGE',
      url: row.url || '',
      thumbnailUrl: row.thumbnailUrl || '',
      thumbnailFileId: row.thumbnailFileId || null,
      sortOrder: row.sortOrder || 0,
      remark: row.remark || ''
     };
    selectedFileName.value = '';
    uploadPercent.value = 0;
    uploadModalApi.setState({ title: '장식 리소스 수정' });
  } else {
    isEditMode.value = false;
    currentSourceId.value = '';
    formModel.value = {
      name: '',
      shortName: '',
      sourceType: 'IMAGE',
      url: '',
      thumbnailUrl: '',
      thumbnailFileId: null,
      sortOrder: 0,
      remark: ''
    };
    selectedFileName.value = '';
    uploadPercent.value = 0;
    uploadModalApi.setState({ title: '새 장식 리소스 등록' });
  }
  uploadModalApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.url) {
      message.warning('장식 명칭과 이미지 파일은 필수 사항입니다.');
      return;
    }

    uploadModalApi.lock();
    if (isEditMode.value) {
      await updateMediaSource(currentSourceId.value, formModel.value);
      message.success('장식 소스가 성공적으로 수정되었습니다.');
    } else {
      await createMediaSource(formModel.value);
      message.success('장식 소스가 성공적으로 등록되었습니다.');
    }
    uploadModalApi.close();
    emit('saved');
  } catch (error) {
    message.error(isEditMode.value ? '장식 리소스 수정 실패' : '장식 리소스 등록 실패');
  } finally {
    uploadModalApi.unlock();
  }
}

// 파일 업로드 처리
async function customUploadRequest(options: any) {
  isUploading.value = true;
  uploadPercent.value = 0;
  selectedFileName.value = options.file.name;



  try {
    await upload_file({
      file: options.file,
      bizType: 'decoration',
      onProgress: (event) => {
        uploadPercent.value = Math.round(event.percent);
        options.onProgress(event);
      },
      onSuccess: (res, file) => {
        const fileItem = res?.result?.[0] || res?.[0] || res;
        const fileUrl = fileItem?.downloadUrl || fileItem?.url;
        const fileId = fileItem?.id;

        if (fileUrl && typeof fileUrl === 'string') {
          formModel.value.url = fileUrl;
          if (fileId) {
            formModel.value.thumbnailUrl = `/api/file/thumbnail/${fileId}`;
            formModel.value.thumbnailFileId = fileId;
          } else {
            formModel.value.thumbnailUrl = fileUrl;
            formModel.value.thumbnailFileId = null;
          }
          
          if (!formModel.value.name) {
            formModel.value.name = file.name.substring(0, file.name.lastIndexOf('.')) || file.name;
          }
          message.success('장식 이미지 업로드 성공');
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
        <!-- 장식 이미지 파일 업로드 -->
        <Form.Item label="장식 이미지 파일 업로드 (PNG 투명 파일 권장)" required>
          <Upload
            accept="image/png"
            :custom-request="customUploadRequest"
            :show-upload-list="false"
            @change="handleUploadChange"
          >
            <Button :loading="isUploading" type="dashed" class="w-full">
              {{ isUploading ? '서버로 업로드 중...' : (isEditMode ? '클릭하여 이미지 파일 교체' : '클릭하여 PNG 장식 이미지 선택') }}
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
            <div class="size-16 bg-[url('data:image/svg+xml;utf8,<svg xmlns=%22http://www.w3.org/2000/svg%22 width=%228%22 height=%228%22 viewBox=%220 0 8 8%22><rect width=%224%22 height=%224%22 fill=%22%23ccc%22/><rect x=%224%22 y=%224%22 width=%224%22 height=%224%22 fill=%22%23ccc%22/></svg>')] bg-white rounded border overflow-hidden flex items-center justify-center">
              <img :src="formModel.thumbnailUrl || formModel.url" class="max-w-[56px] max-h-[56px] object-contain" alt="장식 미리보기" />
            </div>
            <div class="flex-1 space-y-1">
              <div>현재 이미지 경로: {{ formModel.url }}</div>
              <div class="text-primary font-medium text-[10px]">※ 영정사진용 투명도 오버레이에 사용될 이미지입니다.</div>
            </div>
          </div>
        </Form.Item>

        <!-- 장식 명칭 -->
        <Form.Item label="장식 명칭" required>
          <Input v-model:value="formModel.name" placeholder="장식 리소스 명칭을 입력해 주십시오." />
        </Form.Item>

        <!-- 짧은 명칭 -->
        <Form.Item label="짧은 명칭">
          <Input v-model:value="formModel.shortName" placeholder="목록 및 DID에서 선택 시 표시할 짧은 별칭입니다." />
        </Form.Item>

        <!-- 순서 -->
        <Form.Item label="정렬 순서">
          <Input v-model:value="formModel.sortOrder" type="number" placeholder="목록 정렬 가중치입니다." />
        </Form.Item>

        <!-- 설명 -->
        <Form.Item label="설명 및 비고">
          <Input.TextArea v-model:value="formModel.remark" :rows="3" placeholder="리소스의 용도나 특징을 간략히 메모합니다." />
        </Form.Item>
      </Form>
    </div>
  </UploadModal>
</template>
