<script lang="ts" setup>
import { computed, ref } from 'vue';
import { Upload, message } from 'ant-design-vue';
import { Plus } from '@vben/icons';
import { ImageGroupManager } from '@vben/common-ui';
import { useAccessStore } from '@vben/stores';

const props = defineProps({
  modelValue: {
    type: Object,
    required: true
  }
});

const emit = defineEmits(['update:modelValue']);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
});

const isUploading = ref(false);

const accessStore = useAccessStore();

const uploadHeaders = computed(() => {
  const token = accessStore.accessToken;
  const headers: Record<string, string> = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
});

const thumbnailUrl = computed(() => {
  if (model.value?.memorialPhotoFileId) {
    return `/api/file/thumbnail/${model.value.memorialPhotoFileId}`;
  }
  return model.value?.memorialPhotoUrl || '';
});

// 영정사진 단건 업로드 핸들러
async function handlePhotoUpload(info: any) {
  const { file } = info;
  if (!file) return;

  if (file.status === 'uploading') {
    isUploading.value = true;
    return;
  }

  if (file.status === 'done') {
    isUploading.value = false;
    const res = file.response;
    if (res && (res.code === 'S000' || res.success)) {
      const rawData = res.result || res.data;
      let fileData: any = null;

      if (rawData) {
        if (Array.isArray(rawData)) {
          fileData = rawData[0];
        } else if (rawData.result && Array.isArray(rawData.result)) {
          fileData = rawData.result[0];
        } else {
          fileData = rawData;
        }
      }

      if (fileData) {
        model.value.memorialPhotoFileId = fileData.id;
        model.value.memorialPhotoUrl = fileData.downloadUrl || `/api/file/download/${fileData.id}`;
        message.success('영정사진이 등록되었습니다.');
      }
    } else {
      message.error(res?.message || '업로드 실패');
    }
  } else if (file.status === 'error') {
    isUploading.value = false;
    message.error('서버와의 통신에 실패했습니다.');
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- 1. 영정사진 단건 관리 -->
    <div class="p-4  rounded border border-gray-150">
      <h3 class="text-sm font-semibold mb-3 text-gray-700">
        고인 영정사진 등록
      </h3>

      <div class="flex gap-6 items-center">
        <div class="w-32 h-40 bg-gray-100 rounded border border-gray-200 flex items-center justify-center overflow-hidden relative">
          <img
            v-if="thumbnailUrl"
            :src="thumbnailUrl"
            class="w-full h-full object-cover"
            alt="영정사진"
          />
          <span v-else class="text-gray-400 text-xs">영정사진 미등록</span>
        </div>

        <div class="space-y-2">
          <Upload
            name="file"
            action="/api/file/upload"
            :show-upload-list="false"
            :headers="uploadHeaders"
            :data="{ bizType: 'DECEASED' }"
            @change="handlePhotoUpload"
          >
            <button class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded text-sm transition-colors flex items-center gap-1">
              <Plus class="size-4 mr-1 inline-block" /> 사진 업로드
            </button>
          </Upload>
          <p class="text-xs text-gray-500">
            * 권장 비율: 3x4 세로형 이미지 (jpg, png 형식 지원)<br />
            * 업로드 즉시 영정사진이 화면에 캐싱되어 노출됩니다.
          </p>
        </div>
      </div>
    </div>

    <!-- 2. 유족 추모용 사진 앨범 관리 -->
    <div class="p-4  rounded border border-gray-150">
      <h3 class="text-sm font-semibold mb-2 text-gray-700">
        추모 앨범 (유족 추모용 슬라이드 쇼)
      </h3>
      <p class="text-xs text-gray-500 mb-4">
        * 장례식장 입구 모니터 및 분향소 디스플레이에 순차 롤링되는 추모 앨범 이미지 그룹입니다.
      </p>

      <ImageGroupManager
        v-model="model.familyPhotoGroupId"
        :limit="20"
        biz-type="DECEASED"
      />
    </div>
  </div>
</template>
