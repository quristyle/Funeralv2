<script lang="ts" setup>
import { ref, nextTick, onUnmounted } from 'vue';
import { Modal, Button, Upload, message } from 'ant-design-vue';
import type { UploadChangeParam } from 'ant-design-vue';
import ImageEditor from 'tui-image-editor';
import { requestClient } from '#/api/request';
import { saveDeceasedDetail } from '#/api/building';

import 'tui-image-editor/dist/tui-image-editor.css';

const emit = defineEmits(['saved']);

const visible = ref(false);
const saveLoading = ref(false);
const deceasedData = ref<any>(null);
const uploadMimeType = ref<string>('image/png'); // 투명도 유지용 MIME type 기록

const editorContainerRef = ref<HTMLDivElement | null>(null);
const editorInstance = ref<ImageEditor | null>(null);

// 기존 인스턴스 파괴
function destroyEditor() {
  if (editorInstance.value) {
    editorInstance.value.destroy();
    editorInstance.value = null;
  }
}

// 모달 오픈 핸들러
function open(row: any) {
  deceasedData.value = { ...row };
  uploadMimeType.value = 'image/png'; // 기본 투명도 보장 PNG
  visible.value = true;

  // 기존 영정사진 정보 추출
  let initialImage = '';
  if (row.memorialPhotoFileId) {
    initialImage = `/api/file/download/${row.memorialPhotoFileId}`;
  } else if (row.memorialPhotoUrl) {
    initialImage = row.memorialPhotoUrl;
  }

  // PNG 타입 판단 (기존 파일명이 .png 이거나 url 에 png 가 있을 시)
  const lowerUrl = initialImage.toLowerCase();
  if (lowerUrl.endsWith('.png') || lowerUrl.includes('png')) {
    uploadMimeType.value = 'image/png';
  } else {
    uploadMimeType.value = 'image/jpeg';
  }

  // DOM 렌더링 완료 후 TUI Image Editor 초기화
  nextTick(() => {
    initEditor(initialImage);
  });
}

// TUI 에디터 초기화
function initEditor(imageSrc: string) {
  destroyEditor();
  if (!editorContainerRef.value) return;

  const options: any = {
    includeUI: {
      theme: {
        'common.bi.image': '', // 로고 영역 비우기
        'common.bisize.width': '0px',
        'common.bisize.height': '0px',
        'common.backgroundColor': '#ffffff',
        'common.border': '1px solid #e5e7eb',
        'header.backgroundImage': 'none',
        'header.backgroundColor': '#ffffff',
        'header.border': '0px',
      },
      initMenu: 'crop',
      menuBarPosition: 'bottom',
    },
    cssMaxWidth: 660,
    cssMaxHeight: 450,
    usageStatistics: false,
  };

  if (imageSrc) {
    options.includeUI.loadImage = {
      path: imageSrc,
      name: 'DeceasedPhoto',
    };
  }

  try {
    editorInstance.value = new ImageEditor(editorContainerRef.value, options);
  } catch (error) {
    console.error('TUI Image Editor 초기화 에러:', error);
  }
}

// 파일 선택 시 에디터 이미지 로드 및 타입 기억
const selectImgFile = (event: UploadChangeParam) => {
  const file = event.fileList[0]?.originFileObj;
  if (!file) return;

  if (!file.type.startsWith('image/')) {
    message.error('이미지 파일을 업로드해 주세요.');
    return;
  }

  // 사용자가 올린 원본 파일의 MIME type을 그대로 계승 (투명 PNG 유지 목적)
  uploadMimeType.value = file.type;

  const reader = new FileReader();
  reader.addEventListener('load', (e) => {
    const dataUrl = e.target?.result as string;
    if (editorInstance.value) {
      editorInstance.value.loadImageFromURL(dataUrl, file.name).then(() => {
        editorInstance.value?.clearUndoStack();
        editorInstance.value?.clearRedoStack();
      }).catch((err) => {
        console.error('이미지 로드 실패:', err);
        message.error('에디터에 이미지를 로드하지 못했습니다.');
      });
    } else {
      initEditor(dataUrl);
    }
  });
  reader.readAsDataURL(file);
};

// DataURL -> Blob 변환 헬퍼
function dataURLtoBlob(dataurl: string) {
  const arr = dataurl.split(',');
  const mime = arr[0]?.match(/:(.*?);/)?.[1] || 'image/png';
  const bstr = atob(arr[1] || '');
  let n = bstr.length;
  const u8arr = new Uint8Array(n);
  while (n--) {
    u8arr[n] = bstr.charCodeAt(n);
  }
  return new Blob([u8arr], { type: mime });
}

// 편집된 내용 최종 추출 및 업로드 저장
const handleSave = async () => {
  if (!editorInstance.value) {
    message.warning('편집기에 로드된 이미지가 없습니다.');
    return;
  }
  if (!deceasedData.value) return;

  saveLoading.value = true;
  try {
    // 1. 에디터 캔버스로부터 최종 가공된 이미지 base64 획득
    // 원본 감지 파일이 PNG 형식이면 포맷을 png로 강제하여 투명 배경 유지
    const isPng = uploadMimeType.value === 'image/png';
    const dataURL = editorInstance.value.toDataURL({
      format: isPng ? 'png' : 'jpeg',
      quality: 0.95,
    });

    if (!dataURL) {
      throw new Error('최종 편집 이미지 추출 실패');
    }

    // 2. Blob 및 File 포장
    const croppedBlob = dataURLtoBlob(dataURL);
    const fileName = isPng ? 'deceased_photo_edited.png' : 'deceased_photo_edited.jpg';
    const file = new File([croppedBlob], fileName, {
      type: uploadMimeType.value,
    });

    // 3. 파일 서버 업로드 API 호출
    const res = await requestClient.upload('/file/upload?bizType=DECEASED', {
      file,
    });

    const rawData = (res as any)?.result?.[0] ?? res;
    if (!rawData || !rawData.id) {
      throw new Error('파일 업로드 응답 ID가 존재하지 않습니다.');
    }

    const newFileId = rawData.id;
    const newDownloadUrl = rawData.downloadUrl ?? `/api/file/download/${newFileId}`;

    // 4. 고인 정보 갱신 저장 API 호출
    const updateParams = {
      ...deceasedData.value,
      memorialPhotoFileId: newFileId,
      memorialPhotoUrl: newDownloadUrl,
    };

    await saveDeceasedDetail(deceasedData.value.id, updateParams);

    message.success('고인 영정사진 편집 및 저장이 완료되었습니다.');
    visible.value = false;
    emit('saved');
  } catch (error) {
    console.error('영정사진 가공 저장 오류:', error);
    message.error('영정사진 가공 저장 중 오류가 발생했습니다.');
  } finally {
    saveLoading.value = false;
  }
};

onUnmounted(() => {
  destroyEditor();
});

defineExpose({ open });
</script>

<template>
  <Modal
    v-model:open="visible"
    title="TUI Image Editor 기반 고인 영정사진 편집"
    :width="760"
    :footer="null"
    destroy-on-close
  >
    <div class="p-2">
      <!-- 상단 파일 로드 도구 영역 -->
      <div class="flex items-center justify-between mb-4 gap-4 bg-gray-50 p-2.5 rounded border border-gray-100">
        <div class="text-xs text-gray-500">
          * 원본 파일 타입을 감지하여 **투명 배경(PNG)**을 완전 보존합니다. (현재 포맷: {{ uploadMimeType }})
        </div>
        <Upload
          :max-count="1"
          :show-upload-list="false"
          :before-upload="() => false"
          @change="selectImgFile"
        >
          <Button type="dashed" size="small">새 이미지 파일 불러오기</Button>
        </Upload>
      </div>

      <!-- TUI Image Editor 컨테이너 마운팅 포인트 -->
      <div class="border border-gray-200 rounded overflow-hidden mb-6">
        <div ref="editorContainerRef" class="tui-editor-mount-point" style="height: 520px;"></div>
      </div>

      <!-- 하단 제어 버튼 -->
      <div class="flex justify-end gap-2 border-t border-gray-100 pt-4">
        <Button @click="visible = false">취소</Button>
        <Button
          type="primary"
          :loading="saveLoading"
          @click="handleSave"
        >
          편집 내용 업로드 및 저장
        </Button>
      </div>
    </div>
  </Modal>
</template>

<style scoped>
/* TUI Image Editor의 세부 테마 오버라이드 및 레이아웃 정돈 */
.tui-editor-mount-point {
  width: 100%;
}
:deep(.tui-image-editor-container) {
  border: none !important;
}
:deep(.tui-image-editor-header) {
  display: none !important; /* 내장 헤더 제거로 커스텀 버튼에 집중 */
}
:deep(.tui-image-editor-help-menu) {
  background-color: #f3f4f6 !important;
}
</style>
