<script lang="ts" setup>
import type { UploadChangeParam } from 'ant-design-vue';

import { ref } from 'vue';

import { Page, VCropper } from '@vben/common-ui';

import { Button, Card, Select, Upload } from 'ant-design-vue';

const options = [
  { label: '1:1', value: '1:1' },
  { label: '16:9', value: '16:9' },
  { label: '제한 없음', value: '' },
];

const cropperRef = ref<InstanceType<typeof VCropper>>();

const cropLoading = ref(false);
const validAspectRatio = ref<string | undefined>('1:1');
const imgUrl = ref('');
const cropperImg = ref();

const selectImgFile = (event: UploadChangeParam) => {
  const file = event.fileList[0]?.originFileObj;
  if (!file) return;

  if (!file.type.startsWith('image/')) {
    console.error('이미지 파일을 업로드해 주세요');
    return;
  }

  const reader = new FileReader();
  reader.addEventListener('load', (e) => {
    imgUrl.value = e.target?.result as string;
  });
  reader.addEventListener('error', () => {
    console.error('Failed to read file');
  });

  reader.readAsDataURL(file);
};

const cropImage = async () => {
  if (!cropperRef.value) return;
  cropLoading.value = true;
  try {
    cropperImg.value = await cropperRef.value.getCropImage(
      'image/jpeg',
      0.92,
      'base64',
    );
  } catch (error) {
    console.error('이미지 자르기 실패:', error);
  } finally {
    cropLoading.value = false;
  }
};

/**
 * 이미지 다운로드
 */
const downloadImage = () => {
  if (!cropperImg.value) return;

  const link = document.createElement('a');
  link.download = `cropped-image-${Date.now()}.png`;
  link.href = cropperImg.value;
  link.click();
};
</script>
<template>
  <Page
    title="VCropper 이미지 자르기"
    description="VCropper는 이미지 자르기 기능을 제공하는 컴포넌트입니다."
  >
    <Card>
      <div class="image-cropper-container">
        <div class="cropper-ratio-display">
          <label class="ratio-label">현재 자르기 비율:</label>
          <Select
            class="w-24"
            v-model:value="validAspectRatio"
            :options="options"
          />
          <Upload
            :max-count="1"
            :show-upload-list="false"
            :before-upload="() => false"
            @change="selectImgFile"
          >
            <Button>이미지 업로드</Button>
          </Upload>
        </div>

        <div v-if="imgUrl" class="cropper-main-wrapper">
          <VCropper
            ref="cropperRef"
            :img="imgUrl"
            :aspect-ratio="validAspectRatio"
            :width="600"
            :height="600"
          />

          <!-- 조작 버튼 그룹 -->
          <div class="cropper-btn-group">
            <Button :loading="cropLoading" @click="cropImage" type="primary">
              자르기
            </Button>
            <Button v-if="cropperImg" @click="downloadImage" danger>
              이미지 다운로드
            </Button>
          </div>

          <!-- 자르기 미리보기 -->
          <img
            v-if="cropperImg"
            class="h-full w-80"
            :src="cropperImg"
            alt="자르기 미리보기"
          />
        </div>
      </div>
    </Card>
  </Page>
</template>
<style scoped>
/* 비율 표시 영역 */
.cropper-ratio-display {
  @apply my-2.5 flex items-center justify-start gap-4;
}

.ratio-label {
  @apply text-sm font-medium;
}

/* 메인 자르기 영역 */
.cropper-main-wrapper {
  @apply flex items-center gap-4;
}

.cropper-btn-group {
  @apply flex flex-col gap-2;
}
</style>
