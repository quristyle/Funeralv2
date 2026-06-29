<script lang="ts" setup>
import { onMounted, onUnmounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import { Button, Upload, Card, Slider, Select, Tooltip, Switch } from 'ant-design-vue';
import { usePhotoEditor } from './modules/usePhotoEditor';
import 'cropperjs/dist/cropper.css';

const route = useRoute();

// DOM Refs 주입 선언 (TS6133 미사용 오류 해결)
const canvasRef = ref<HTMLCanvasElement | null>(null);
const editorContainerRef = ref<HTMLDivElement | null>(null);
const cropperImgRef = ref<HTMLImageElement | null>(null);

const {
  deceasedData,
  saveLoading,
  pageLoading,
  currentMode,
  undoStack,
  redoStack,
  brushColor,
  brushWidth,
  textColor,
  fontSize,
  selectedShape,
  shapeColor,
  isClipEnabled,
  cropRatio,
  cropRatios,
  currentFilter,
  filterOptions,
  zoomRatio,
  currentZoomOptions,
  handleZoomSelectChange,
  resetZoomAndPan,
  loadDeceasedInfo,
  selectImgFile,
  hasOriginalPhoto,
  hasEditedPhoto,
  loadOriginalPhoto,
  loadEditedPhoto,
  handleSave,
  handleClose,
  changeMode,
  rotateCanvas,
  flipCanvas,
  applyFilter,
  deleteSelectedObject,
  applyCrop,
  addText,
  addShape,
  handleKeyDown,
  handleGlobalKeyDown,
  handleGlobalKeyUp,
  destroyEditor,
  handleUndo,
  handleRedo,
} = usePhotoEditor({ canvasRef, editorContainerRef, cropperImgRef });

onMounted(() => {
  const idVal = route.query.id as string;
  if (idVal) {
    loadDeceasedInfo(idVal);
  }
  window.addEventListener('keydown', handleKeyDown);
  window.addEventListener('keydown', handleGlobalKeyDown);
  window.addEventListener('keyup', handleGlobalKeyUp);
});

onUnmounted(() => {
  destroyEditor();
  window.removeEventListener('keydown', handleKeyDown);
  window.removeEventListener('keydown', handleGlobalKeyDown);
  window.removeEventListener('keyup', handleGlobalKeyUp);
});
</script>

<template>
  <div class="photo-editor-page-wrapper p-1 h-screen flex flex-col overflow-hidden">
    <Card class="flex-1 flex flex-col h-full overflow-hidden border border-gray-300 rounded-none" :body-style="{ padding: '8px', display: 'flex', flexDirection: 'column', height: '100%' }">
      <!-- 상단 헤더 영역 -->
      <div class="flex items-center justify-between mb-2 border-b border-gray-200 pb-2 flex-shrink-0">
        <div class="flex flex-col gap-0.5">
          <h1 class="text-sm font-bold text-gray-800 flex items-center gap-1.5 m-0 leading-none">
            <span>고인 영정사진 편집기 (Fabric.js + Cropper.js)</span>
            <span v-if="deceasedData" class="text-[10px] font-semibold text-blue-600 px-2 py-0.5 rounded-none border border-blue-200">
              대상: {{ deceasedData.name }} ({{ deceasedData.gender === 'M' ? '남' : '여' }}, {{ deceasedData.age }}세)
            </span>
          </h1>
          <div class="text-[10px] text-gray-400">
            * [Space + 드래그] 캔버스 이동, [Alt + 휠] 배율 조절
          </div>
        </div>
        <div class="flex items-center gap-2">
          <!-- Undo / Redo -->
          <div class="flex items-center border border-gray-300">
            <Button type="text" size="small" :disabled="undoStack.length <= 1" @click="handleUndo" title="실행 취소 (Ctrl+Z)" class="!rounded-none">
              <template #icon>
                <svg class="w-3.5 h-3.5 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12.066 11.2a1 1 0 000 1.6l5.334 4A1 1 0 0019 16V8a1 1 0 00-1.6-.8l-5.334 4zM4.066 11.2a1 1 0 000 1.6l5.334 4A1 1 0 0011 16V8a1 1 0 00-1.6-.8l-5.334 4z" /></svg>
              </template>
            </Button>
            <Button type="text" size="small" :disabled="redoStack.length === 0" @click="handleRedo" title="다시 실행 (Ctrl+Y)" class="!rounded-none">
              <template #icon>
                <svg class="w-3.5 h-3.5 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11.934 12.8a1 1 0 000-1.6l-5.334-4A1 1 0 005 8v8a1 1 0 001.6.8l5.334-4zM19.934 12.8a1 1 0 000-1.6l-5.334-4A1 1 0 0013 8v8a1 1 0 001.6.8l5.334-4z" /></svg>
              </template>
            </Button>
          </div>
          
          <div class="flex items-center gap-1.5 mr-1 border-r border-gray-200 pr-2">
            <Button 
              size="small" 
              class="!rounded-none" 
              :disabled="!hasOriginalPhoto" 
              @click="loadOriginalPhoto"
            >
              원본사진 불러오기
            </Button>
            <Button 
              size="small" 
              class="!rounded-none" 
              :disabled="!hasEditedPhoto" 
              @click="loadEditedPhoto"
            >
              편집사진 불러오기
            </Button>
          </div>
          
          <Upload
            :max-count="1"
            :show-upload-list="false"
            :before-upload="() => false"
            @change="selectImgFile"
          >
            <Button type="dashed" size="small" class="!rounded-none">새 이미지 불러오기</Button>
          </Upload>
          <div class="flex items-center gap-1.5 ml-1 border-l border-gray-200 pl-2 pr-1">
            <span class="text-xs text-gray-500 font-medium select-none">영역 밖 자르기:</span>
            <Switch v-model:checked="isClipEnabled" size="small" />
          </div>
          <Button size="small" @click="handleClose" class="!rounded-none">닫기</Button>
          <Button
            type="primary"
            size="small"
            :loading="saveLoading"
            :disabled="pageLoading"
            @click="handleSave"
            class="!rounded-none"
          >
            편집 완료 후 저장
          </Button>
        </div>
      </div>

      <!-- 편집 작업대 3단 구성 -->
      <div class="flex-1 flex overflow-hidden border border-gray-300 rounded-none relative">
        <!-- 1단: 좌측 메인 툴바 -->
        <div class="w-12 text-white flex flex-col items-center py-2 gap-2 flex-shrink-0 border-r border-gray-850">
          <Button
            type="text"
            class="toolbar-btn"
            :class="{ active: currentMode === 'select' }"
            @click="changeMode('select')"
            title="선택 및 이동"
          >
            <svg class="w-4.5 h-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 15l-2 5L9 9l11 4-5 2zm0 0l5 5M7.188 2.239l.777 2.897M5.136 7.965l-2.898-.777M13.95 4.05l-2.122 2.122m-5.657 5.656l-2.12 2.122" /></svg>
            <span class="text-[8px] mt-0.5 block leading-none">이동</span>
          </Button>

          <Button
            type="text"
            class="toolbar-btn"
            :class="{ active: currentMode === 'crop' }"
            @click="changeMode('crop')"
            title="이미지 자르기"
          >
            <svg class="w-4.5 h-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12.243 18.657H3m18-6V21M3 3h6m0 0v6M9 3v13a2 2 0 002 2h10M9 9H3" /></svg>
            <span class="text-[8px] mt-0.5 block leading-none">자르기</span>
          </Button>

          <Button
            type="text"
            class="toolbar-btn"
            :class="{ active: currentMode === 'draw' }"
            @click="changeMode('draw')"
            title="그리기 모드"
          >
            <svg class="w-4.5 h-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" /></svg>
            <span class="text-[8px] mt-0.5 block leading-none">그리기</span>
          </Button>

          <Button
            type="text"
            class="toolbar-btn"
            :class="{ active: currentMode === 'text' }"
            @click="changeMode('text')"
            title="텍스트 추가"
          >
            <svg class="w-4.5 h-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h7" /></svg>
            <span class="text-[8px] mt-0.5 block leading-none">글자</span>
          </Button>

          <Button
            type="text"
            class="toolbar-btn"
            :class="{ active: currentMode === 'shape' }"
            @click="changeMode('shape')"
            title="도형 추가"
          >
            <svg class="w-4.5 h-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 5a1 1 0 011-1h14a1 1 0 011 1v14a1 1 0 01-1 1H5a1 1 0 01-1-1V5z" /></svg>
            <span class="text-[8px] mt-0.5 block leading-none">도형</span>
          </Button>
        </div>

        <!-- 2. 단: 서브 세부 설정 제어창 (가로 w-12 세로형 컴팩트 레이아웃) -->
        <div class="w-12 border-r border-gray-200 py-3 px-1 flex flex-col items-center gap-4 overflow-y-auto flex-shrink-0 rounded-none">
          <!-- 선택/기본 모드 상세설정 -->
          <div v-if="currentMode === 'select'" class="flex flex-col items-center gap-3 w-full">
            <!-- 회전 제어 -->
            <Tooltip title="시계 반대 방향 90° 회전" placement="right">
              <Button size="small" class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" @click="rotateCanvas(-90)">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" /></svg>
              </Button>
            </Tooltip>
            
            <Tooltip title="시계 방향 90° 회전" placement="right">
              <Button size="small" class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" @click="rotateCanvas(90)">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 10H11a8 8 0 00-8 8v2m18-10l-6 6m6-6l-6-6" /></svg>
              </Button>
            </Tooltip>

            <!-- 반전 제어 -->
            <Tooltip title="좌우 대칭 반전" placement="right">
              <Button size="small" class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" @click="flipCanvas('X')">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" /></svg>
              </Button>
            </Tooltip>

            <Tooltip title="상하 대칭 반전" placement="right">
              <Button size="small" class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" @click="flipCanvas('Y')">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 8v12m0 0l-4-4m4 4l4-4m6 0V4m0 0l4 4m-4-4l-4 4" /></svg>
              </Button>
            </Tooltip>

            <!-- 이미지 필터 적용 (세로형 아이콘 나열) -->
            <div class="w-full h-px bg-gray-200 my-1"></div>

            <Tooltip v-for="opt in filterOptions" :key="opt.value" :title="opt.label" placement="right">
              <Button
                size="small"
                class="!w-8 !h-8 !p-0 !rounded-none"
                :type="currentFilter === opt.value ? 'primary' : 'default'"
                @click="applyFilter(opt.value)"
              >
                <span class="text-[9px] font-bold uppercase">{{ opt.value.slice(0, 2) }}</span>
              </Button>
            </Tooltip>

            <!-- 삭제 버튼 -->
            <div class="w-full h-px bg-gray-200 my-1"></div>
            <Tooltip title="선택한 오브젝트 삭제 (Delete)" placement="right">
              <Button danger type="primary" size="small" class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" @click="deleteSelectedObject">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
              </Button>
            </Tooltip>
          </div>

          <!-- 자르기 모드 상세설정 -->
          <div v-if="currentMode === 'crop'" class="flex flex-col items-center gap-3 w-full">
            <!-- 가로세로 비율 아이콘식 나열 -->
            <Tooltip v-for="opt in cropRatios" :key="opt.label" :title="opt.label" placement="right">
              <Button
                size="small"
                class="!w-8 !h-8 !p-0 !rounded-none text-[9px]"
                :type="cropRatio === opt.value ? 'primary' : 'default'"
                @click="cropRatio = opt.value"
              >
                <span>{{ opt.value ? (opt.label.includes('1:1') ? '1:1' : opt.label.split(' ')[0]) : '자유' }}</span>
              </Button>
            </Tooltip>

            <div class="w-full h-px bg-gray-200 my-1"></div>

            <Tooltip title="자르기 실행" placement="right">
              <Button type="primary" class="bg-red-500 hover:bg-red-600 border-none flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" size="small" @click="applyCrop">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
              </Button>
            </Tooltip>
            <Tooltip title="취소" placement="right">
              <Button size="small" class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" @click="changeMode('select')">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
              </Button>
            </Tooltip>
          </div>

          <!-- 그리기 모드 상세설정 -->
          <div v-if="currentMode === 'draw'" class="flex flex-col items-center gap-3 w-full">
            <!-- 브러쉬 색상 -->
            <Tooltip title="펜 색상 선택" placement="right">
              <div class="relative w-8 h-8 border border-gray-300 rounded-none overflow-hidden cursor-pointer">
                <input type="color" v-model="brushColor" class="absolute -inset-1 w-12 h-12 p-0 border-none cursor-pointer" />
              </div>
            </Tooltip>

            <div class="w-full h-px bg-gray-200 my-1"></div>

            <!-- 브러쉬 두께 (세로형 슬라이더 적용) -->
            <Tooltip :title="`펜 두께: ${brushWidth}px`" placement="right">
              <div class="flex flex-col items-center h-40 py-1">
                <Slider vertical v-model:value="brushWidth" :min="1" :max="50" class="!m-0" />
              </div>
            </Tooltip>
          </div>

          <!-- 글자 모드 상세설정 -->
          <div v-if="currentMode === 'text'" class="flex flex-col items-center gap-3 w-full">
            <!-- 글자 색상 -->
            <Tooltip title="글자 색상 선택" placement="right">
              <div class="relative w-8 h-8 border border-gray-300 rounded-none overflow-hidden cursor-pointer">
                <input type="color" v-model="textColor" class="absolute -inset-1 w-12 h-12 p-0 border-none cursor-pointer" />
              </div>
            </Tooltip>

            <div class="w-full h-px bg-gray-200 my-1"></div>

            <!-- 글자 크기 (세로형 슬라이더 적용) -->
            <Tooltip :title="`글자 크기: ${fontSize}px`" placement="right">
              <div class="flex flex-col items-center h-40 py-1">
                <Slider vertical v-model:value="fontSize" :min="10" :max="150" class="!m-0" />
              </div>
            </Tooltip>

            <div class="w-full h-px bg-gray-200 my-1"></div>

            <Tooltip title="새 텍스트 상자 추가" placement="right">
              <Button type="primary" size="small" class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" @click="addText">
                <span class="text-xs font-bold font-serif">T+</span>
              </Button>
            </Tooltip>
          </div>

          <!-- 도형 모드 상세설정 -->
          <div v-if="currentMode === 'shape'" class="flex flex-col items-center gap-3 w-full">
            <Tooltip :title="selectedShape === 'rect' ? '사각형 형태 선택됨' : '원형 형태 선택됨'" placement="right">
              <Button
                size="small"
                class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none"
                @click="selectedShape = selectedShape === 'rect' ? 'circle' : 'rect'"
              >
                <svg v-if="selectedShape === 'rect'" class="w-4.5 h-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 5a1 1 0 011-1h14a1 1 0 011 1v14a1 1 0 01-1 1H5a1 1 0 01-1-1V5z" /></svg>
                <svg v-else class="w-4.5 h-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><circle cx="12" cy="12" r="8" stroke-width="2" /></svg>
              </Button>
            </Tooltip>

            <!-- 내부 채우기 색상 -->
            <Tooltip title="도형 색상 선택" placement="right">
              <div class="relative w-8 h-8 border border-gray-300 rounded-none overflow-hidden cursor-pointer">
                <input type="color" v-model="shapeColor" class="absolute -inset-1 w-12 h-12 p-0 border-none cursor-pointer" />
              </div>
            </Tooltip>

            <div class="w-full h-px bg-gray-200 my-1"></div>

            <Tooltip title="도형 생성 후 삽입" placement="right">
              <Button type="primary" size="small" class="flex items-center justify-center !w-8 !h-8 !p-0 !rounded-none" @click="addShape">
                <span class="text-xs font-bold">+</span>
              </Button>
            </Tooltip>
          </div>
        </div>

        <!-- 3단: 중앙 캔버스 작업 공간 -->
        <div class="flex-1 flex items-center justify-center p-2 relative overflow-hidden">
          <div v-if="pageLoading" class="text-gray-500 text-xs font-semibold absolute z-20 flex flex-col items-center gap-2 p-3 rounded-none border border-gray-300">
            <div class="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600"></div>
            <span>편집 환경 구성 중...</span>
          </div>

          <!-- 배율 배지 및 뷰포트 리셋 버튼 -->
          <div class="absolute bottom-2 right-2 z-10 flex items-center gap-1 bg-gray-900 bg-opacity-85 text-white text-[10px] px-1.5 py-0.5 rounded-none shadow-sm select-none border border-gray-700">
            <span class="text-gray-400 pl-1">배율:</span>
            <Select
              v-model:value="zoomRatio"
              size="small"
              :options="currentZoomOptions"
              class="zoom-select-dropdown w-20"
              :bordered="false"
              @change="(val) => handleZoomSelectChange(val as number | 'fit')"
            />
            <span class="text-gray-500">|</span>
            <button class="hover:text-blue-400 font-medium focus:outline-none pr-1" @click="resetZoomAndPan">초기화</button>
          </div>

          <!-- 실제 캔버스 고정을 위한 컨테이너 -->
          <div ref="editorContainerRef" class="canvas-workspace-box shadow-none border border-gray-300 relative max-w-full max-h-full overflow-hidden flex items-center justify-center checkerboard-bg rounded-none">
            <div 
              :style="{ opacity: currentMode === 'crop' ? 0 : 1, pointerEvents: currentMode === 'crop' ? 'none' : 'auto' }"
              class="flex items-center justify-center max-w-full max-h-full overflow-visible"
            >
              <canvas ref="canvasRef"></canvas>
            </div>

            <!-- Cropper.js 이미지 래퍼 오버레이 -->
            <div v-show="currentMode === 'crop'" class="absolute inset-0 z-30 checkerboard-bg flex items-center justify-center">
              <img ref="cropperImgRef" style="display: block; max-width: 100%; max-height: 100%;" />
            </div>
          </div>
        </div>
      </div>
    </Card>
  </div>
</template>

<style scoped>
.photo-editor-page-wrapper {
  width: 100vw;
  box-sizing: border-box;
}

/* 툴바 커스텀 스타일 */
.toolbar-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 0px !important;
  color: #9ca3af;
  padding: 0;
  transition: all 0.2s;
}

.toolbar-btn:hover {
  color: #ffffff;
  background-color: #1f2937;
}

.toolbar-btn.active {
  color: #3b82f6;
  border: 1px solid #3b82f6;
}

.bg-gray-250 {
  background-color: #eef1f6;
}

/* 모든 내부 UI 요소의 모서리 라운딩 완전 제거 (Flat) */
:deep(*) {
  border-radius: 0px !important;
}

/* 줌 셀렉트 스타일 커스텀 */
:deep(.zoom-select-dropdown .ant-select-selector) {
  color: #ffffff !important;
  background-color: transparent !important;
  font-size: 12px !important;
  padding: 0 4px !important;
}
:deep(.zoom-select-dropdown .ant-select-arrow) {
  color: #9ca3af !important;
}

/* 투명 영역 시각화용 격자(Checkerboard) 패턴 */
.checkerboard-bg {
  background-color: #f3f4f6;
  background-image: linear-gradient(45deg, #e5e7eb 25%, transparent 25%), 
                    linear-gradient(-45deg, #e5e7eb 25%, transparent 25%), 
                    linear-gradient(45deg, transparent 75%, #e5e7eb 75%), 
                    linear-gradient(-45deg, transparent 75%, #e5e7eb 75%);
  background-size: 16px 16px;
  background-position: 0 0, 0 8px, 8px -8px, -8px 0px;
}

/* Fabric 캔버스 및 Cropper.js 반응형 대응 */
:deep(.canvas-container) {
  max-width: 100% !important;
  max-height: 100% !important;
  display: flex !important;
  align-items: center !important;
  justify-content: center !important;
}

:deep(.canvas-container canvas) {
  max-width: 100% !important;
  max-height: 100% !important;
  object-fit: contain !important;
}

/* Cropper 컨테이너 래퍼 오버라이드 */
:deep(.cropper-container) {
  max-width: 100% !important;
  max-height: 100% !important;
}
</style>
