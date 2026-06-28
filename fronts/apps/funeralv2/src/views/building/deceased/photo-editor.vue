<script lang="ts" setup>
import { ref, onMounted, onUnmounted, nextTick } from 'vue';
import { useRoute } from 'vue-router';
import { Button, Upload, message, Card } from 'ant-design-vue';
import type { UploadChangeParam } from 'ant-design-vue';
import ImageEditor from 'tui-image-editor';
import { requestClient } from '#/api/request';
import { getDeceasedDetail, saveDeceasedDetail } from '#/api/building';

import 'tui-image-editor/dist/tui-image-editor.css';

const route = useRoute();
const deceasedId = ref<string>('');
const deceasedData = ref<any>(null);
const uploadMimeType = ref<string>('image/png'); // 투명도 유지용 MIME type 기록

const editorContainerRef = ref<HTMLDivElement | null>(null);
const editorInstance = ref<ImageEditor | null>(null);
const saveLoading = ref(false);
const pageLoading = ref(true);
// TUI Image Editor 하단 서브메뉴에 직접 세로 크롭 비율 preset 버튼들을 주입
function injectCustomCropRatios() {
  if (!editorContainerRef.value || !editorInstance.value) return;

  const cropMenu = editorContainerRef.value.querySelector('.tui-image-editor-menu-crop');
  if (!cropMenu) {
    // includeUI 하단 툴바 메뉴 DOM이 생성되기 전이면 지연 대기 후 재실행
    setTimeout(injectCustomCropRatios, 150);
    return;
  }

  const presetContainer = cropMenu.querySelector('.tui-image-editor-submenu-item') 
    || cropMenu.querySelector('.tui-image-editor-submenu-align');

  if (!presetContainer) return;

  // 중복 삽입 차단
  if (presetContainer.querySelector('.custom-vertical-ratio')) return;

  const verticalRatios = [
    { label: '3:4 (영정)', ratio: 3 / 4 },
    { label: '2:3 (세로)', ratio: 2 / 3 },
    { label: '5:7 (세로)', ratio: 5 / 7 },
    { label: '9:16 (세로)', ratio: 9 / 16 },
  ];

  verticalRatios.forEach((item) => {
    const isLi = presetContainer.tagName.toLowerCase() === 'ul';
    const btn = document.createElement(isLi ? 'li' : 'div');
    btn.className = 'tui-image-editor-button preset custom-vertical-ratio';
    btn.setAttribute('data-ratio', String(item.ratio));
    
    // TUI Image Editor UI의 고유 레이아웃에 통합 어우러지도록 레이블 설정
    const label = document.createElement('label');
    label.style.cursor = 'pointer';
    label.innerText = item.label;
    btn.appendChild(label);

    btn.addEventListener('click', (e) => {
      e.preventDefault();
      // 기존 비율 프리셋들의 active 해제
      presetContainer.querySelectorAll('.tui-image-editor-button, .preset, li').forEach((el) => {
        el.classList.remove('active', 'checked');
      });
      btn.classList.add('active');

      // TUI 이미지 에디터 크롭 셋 비율 동적 부여
      const editor = editorInstance.value as any;
      editor.startDrawingMode('CROPPER');
      editor.setCropzoneAspectRatio(item.ratio);
    });

    presetContainer.appendChild(btn);
  });

  // TUI 기본 가로 비율 버튼 클릭 시, 우리가 인젝션한 세로 비율 버튼들의 active를 소멸시킴
  const originalBtns = presetContainer.querySelectorAll('.tui-image-editor-button.preset:not(.custom-vertical-ratio), li:not(.custom-vertical-ratio)');
  originalBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      presetContainer.querySelectorAll('.custom-vertical-ratio').forEach((cb) => {
        cb.classList.remove('active');
      });
    });
  });
}

// 에디터 정리
function destroyEditor() {
  if (editorInstance.value) {
    editorInstance.value.destroy();
    editorInstance.value = null;
  }
}

// TUI 에디터 초기화
function initEditor(imageSrc: string) {
  destroyEditor();
  if (!editorContainerRef.value) return;

  const options: any = {
    includeUI: {
      theme: {
        'common.bi.image': '', // 로고 제거
        'common.bisize.width': '0px',
        'common.bisize.height': '0px',
        'common.backgroundColor': '#ffffff',
        'common.border': '1px solid #e5e7eb',
        'header.backgroundImage': 'none',
        'header.backgroundColor': '#ffffff',
        'header.border': '0px',
      },
      initMenu: 'crop',
      menuBarPosition: 'left',
    },
    cssMaxWidth: 1600,
    cssMaxHeight: 900,
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
    
    // UI 마운팅 이후 하단 Crop 툴바 영역에 커스텀 세로 비율 버튼 강제 주입
    setTimeout(injectCustomCropRatios, 200);
  } catch (error) {
    console.error('TUI Image Editor 초기화 에러:', error);
  }
}

// 데이터 로드 및 초기화
async function loadDeceasedInfo() {
  const idVal = route.query.id as string;
  if (!idVal) {
    message.error('고인 정보 식별자(ID)가 누락되었습니다.');
    pageLoading.value = false;
    return;
  }
  deceasedId.value = idVal;

  try {
    const res = await getDeceasedDetail(idVal);
    const detail = (res as any)?.result?.[0] ?? res;
    if (!detail) {
      throw new Error('고인 상세 정보를 찾을 수 없습니다.');
    }
    deceasedData.value = detail;

    // 기존 영정사진 체크
    let initialImage = '';
    if (detail.memorialPhotoFileId) {
      initialImage = `/api/file/download/${detail.memorialPhotoFileId}`;
    } else if (detail.memorialPhotoUrl) {
      initialImage = detail.memorialPhotoUrl;
    }

    // 파일 타입 판단
    const lowerUrl = initialImage.toLowerCase();
    if (lowerUrl.endsWith('.png') || lowerUrl.includes('png')) {
      uploadMimeType.value = 'image/png';
    } else {
      uploadMimeType.value = 'image/jpeg';
    }

    nextTick(() => {
      initEditor(initialImage);
    });
  } catch (error) {
    console.error('고인 정보 로드 실패:', error);
    message.error('고인 상세 정보를 불러오지 못했습니다.');
  } finally {
    pageLoading.value = false;
  }
}

// 새 파일 선택 시 에디터 로드
const selectImgFile = (event: UploadChangeParam) => {
  const file = event.fileList[0]?.originFileObj;
  if (!file) return;

  if (!file.type.startsWith('image/')) {
    message.error('이미지 파일을 업로드해 주세요.');
    return;
  }

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

// DataURL -> Blob
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

// 편집 내용 업로드 및 저장
const handleSave = async () => {
  if (!editorInstance.value) {
    message.warning('편집기에 로드된 이미지가 없습니다.');
    return;
  }
  if (!deceasedData.value) return;

  saveLoading.value = true;
  try {
    const isPng = uploadMimeType.value === 'image/png';
    const dataURL = editorInstance.value.toDataURL({
      format: isPng ? 'png' : 'jpeg',
      quality: 0.95,
    });

    if (!dataURL) {
      throw new Error('편집 이미지 추출 실패');
    }

    const croppedBlob = dataURLtoBlob(dataURL);
    const fileName = isPng ? 'deceased_photo_edited.png' : 'deceased_photo_edited.jpg';
    const file = new File([croppedBlob], fileName, {
      type: uploadMimeType.value,
    });

    const res = await requestClient.upload('/file/upload?bizType=DECEASED', {
      file,
    });

    const rawData = (res as any)?.result?.[0] ?? res;
    if (!rawData || !rawData.id) {
      throw new Error('파일 업로드 응답 ID가 존재하지 않습니다.');
    }

    const newFileId = rawData.id;
    const newDownloadUrl = rawData.downloadUrl ?? `/api/file/download/${newFileId}`;

    const updateParams = {
      ...deceasedData.value,
      memorialPhotoFileId: newFileId,
      memorialPhotoUrl: newDownloadUrl,
    };

    await saveDeceasedDetail(deceasedId.value, updateParams);

    message.success({
      content: '고인 영정사진 편집 및 저장이 완료되었습니다.',
      duration: 2,
    });
    
    // 부모창 새로고침 통보 (window.opener 가 있을 시)
    if (window.opener) {
      try {
        window.opener.postMessage('deceased-photo-saved', '*');
      } catch (err) {
        console.warn('부모창 메시지 송신 실패:', err);
      }
    }

    setTimeout(() => {
      window.close();
    }, 1000);
  } catch (error) {
    console.error('영정사진 가공 저장 오류:', error);
    message.error('영정사진 가공 저장 중 오류가 발생했습니다.');
  } finally {
    saveLoading.value = false;
  }
};

// 창 닫기
function handleClose() {
  window.close();
}

onMounted(() => {
  loadDeceasedInfo();
});

onUnmounted(() => {
  destroyEditor();
});
</script>

<template>
  <div class="photo-editor-page-wrapper p-2 bg-gray-100 h-screen flex flex-col overflow-hidden">
    <Card class="flex-1 flex flex-col h-full overflow-hidden shadow-sm" :body-style="{ padding: '12px', display: 'flex', flexDirection: 'column', height: '100%' }">
      <!-- 헤더 툴바 영역 -->
      <div class="flex items-center justify-between mb-2 border-b border-gray-100 pb-2">
        <div class="flex flex-col gap-0.5">
          <h1 class="text-base font-bold text-gray-800 flex items-center gap-2 m-0">
            <span>고인 영정사진 고급 편집기</span>
            <span v-if="deceasedData" class="text-xs font-normal text-blue-600 bg-blue-50 px-2 py-0.5 rounded-full">
              대상 고인: {{ deceasedData.name }} ({{ deceasedData.gender === 'M' ? '남' : '여' }}, {{ deceasedData.age }}세)
            </span>
          </h1>
          <div class="text-xxs text-gray-400">
            * 원본 이미지의 투명도(PNG)를 감지해 보존합니다. (현재 자동 감지: {{ uploadMimeType }})
          </div>
        </div>
        <div class="flex items-center gap-2">
          <Upload
            :max-count="1"
            :show-upload-list="false"
            :before-upload="() => false"
            @change="selectImgFile"
          >
            <Button type="dashed" size="small">새 이미지 불러오기</Button>
          </Upload>
          <Button size="small" @click="handleClose">닫기</Button>
          <Button
            type="primary"
            size="small"
            :loading="saveLoading"
            :disabled="pageLoading"
            @click="handleSave"
          >
            편집 완료 후 저장
          </Button>
        </div>
      </div>

      <!-- 편집 영역 -->
      <div class="editor-workspace flex-1 w-full border border-gray-200 rounded overflow-hidden bg-gray-50 flex items-center justify-center relative">
        <div v-if="pageLoading" class="text-gray-400 text-sm absolute z-10">
          고인 정보 로딩 및 편집 환경 초기화 중...
        </div>
        <div ref="editorContainerRef" class="tui-editor-mount-point" style="height: calc(100vh - 100px); width: 100%;"></div>
      </div>
    </Card>
  </div>
</template>

<style scoped>
.photo-editor-page-wrapper {
  width: 100vw;
  box-sizing: border-box;
}
:deep(.tui-image-editor-container) {
  border: none !important;
  height: 100% !important;
}
:deep(.tui-image-editor-main-container) {
  height: 100% !important;
}
:deep(.tui-image-editor-header) {
  display: none !important; /* 상단 불필요 헤더 생략 */
}
:deep(.tui-image-editor-help-menu) {
  background-color: #f9fafb !important;
}
</style>
