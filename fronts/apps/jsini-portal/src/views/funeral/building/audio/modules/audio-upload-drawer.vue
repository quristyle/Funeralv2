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
  sourceType: 'AUDIO' as const,
  url: '',
  thumbnailUrl: '',
  thumbnailFileId: null as string | null,
  sortOrder: 0,
  remark: ''
});

const [UploadDrawer, uploadDrawerApi] = useVbenDrawer({
  title: '새 음원 리소스 등록',
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
      sourceType: 'AUDIO',
      url: row.url || '',
      thumbnailUrl: row.thumbnailUrl || '',
      thumbnailFileId: row.thumbnailFileId || null,
      sortOrder: row.sortOrder || 0,
      remark: row.remark || ''
     };
    selectedFileName.value = '';
    uploadPercent.value = 0;
    uploadDrawerApi.setState({ title: '음원 리소스 수정' });
  } else {
    isEditMode.value = false;
    currentSourceId.value = '';
    formModel.value = {
      name: '',
      shortName: '',
      sourceType: 'AUDIO',
      url: '',
      thumbnailUrl: '',
      thumbnailFileId: null,
      sortOrder: 0,
      remark: ''
    };
    selectedFileName.value = '';
    uploadPercent.value = 0;
    uploadDrawerApi.setState({ title: '새 음원 리소스 등록' });
  }
  uploadDrawerApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.url) {
      message.warning('음원 명칭과 음원 파일은 필수 사항입니다.');
      return;
    }

    uploadDrawerApi.lock();
    if (isEditMode.value) {
      await updateMediaSource(currentSourceId.value, formModel.value);
      message.success('음원 소스가 성공적으로 수정되었습니다.');
    } else {
      await createMediaSource(formModel.value);
      message.success('음원 소스가 성공적으로 등록되었습니다.');
    }
    uploadDrawerApi.close();
    emit('saved');
  } catch (error) {
    message.error(isEditMode.value ? '음원 리소스 수정 실패' : '음원 리소스 등록 실패');
  } finally {
    uploadDrawerApi.unlock();
  }
}

// 파일 업로드 처리
async function customUploadRequest(options: any) {
  isUploading.value = true;
  uploadPercent.value = 0;
  selectedFileName.value = options.file.name;

  const fileLimit = 100 * 1024 * 1024; // 100MB 한도
  if (options.file.size > fileLimit) {
    message.error('업로드 가능한 음원 파일 최대 용량은 100MB입니다.');
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
        <Form.Item v-if="!isEditMode" label="음원 파일 업로드 (최대 100MB)" required>
          <Upload
            accept="audio/mp3,audio/wav,audio/mpeg,audio/ogg"
            :custom-request="customUploadRequest"
            :show-upload-list="false"
            @change="handleUploadChange"
          >
            <Button :loading="isUploading" type="dashed" class="w-full">
              {{ isUploading ? '서버로 업로드 중...' : '클릭하여 음원 파일 선택' }}
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
            <img v-if="formModel.thumbnailUrl" :src="formModel.thumbnailUrl" class="w-16 h-16 object-cover rounded border shadow-sm" alt="음원 커버" />
            <div v-else class="w-16 h-16 bg-black/5 rounded border flex items-center justify-center text-lg text-muted-foreground">🎵</div>
            <div class="flex-1 space-y-1">
              <div>업로드된 경로: {{ formModel.url }}</div>
              <div v-if="formModel.thumbnailUrl" class="text-primary font-medium text-[10px]">※ 음원에서 앨범아트(커버)가 추출되어 바인딩되었습니다.</div>
              <div v-else class="text-muted-foreground text-[10px]">※ 추출된 앨범아트가 없는 음원 리소스입니다. (기본 아이콘으로 설정됩니다)</div>
            </div>
          </div>
        </Form.Item>

        <!-- 수정 모드일 때 업로드 경로 정보만 조회 -->
        <Form.Item v-else label="등록된 음원 파일 정보">
          <div class="mt-2 text-xs text-muted-foreground break-all bg-muted p-3 rounded border flex gap-3 items-center">
            <img v-if="formModel.thumbnailUrl" :src="formModel.thumbnailUrl" class="w-16 h-16 object-cover rounded border shadow-sm" alt="음원 커버" />
            <div v-else class="w-16 h-16 bg-black/5 rounded border flex items-center justify-center text-lg text-muted-foreground">🎵</div>
            <div class="flex-1 space-y-1">
              <div>파일 경로: {{ formModel.url }}</div>
              <div class="text-primary font-medium text-[10px]">※ 수정 모드에서는 원본 음원 파일을 변경할 수 없습니다.</div>
            </div>
          </div>
        </Form.Item>

        <Form.Item label="음원 명칭" required>
          <Input v-model:value="formModel.name" placeholder="예: 상례 추모 음악 1번, 관내 백그라운드 재즈" />
        </Form.Item>

        <Form.Item label="짧은 명칭">
          <Input v-model:value="formModel.shortName" placeholder="예: 추모음악1, 백그라운드재즈 (장비 DID 화면 등에 노출)" />
        </Form.Item>

        <Form.Item label="순서">
          <InputNumber v-model:value="formModel.sortOrder" :min="0" class="w-full" placeholder="정렬 순서" />
        </Form.Item>

        <Form.Item label="설명/비고">
          <Input.TextArea v-model:value="formModel.remark" placeholder="음원 장르 및 상세 용도 작성" />
        </Form.Item>
      </Form>
    </div>
  </UploadDrawer>
</template>
