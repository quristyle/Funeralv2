<script lang="ts" setup>
import { ref, nextTick, onUnmounted, watch, computed } from 'vue';
import { Modal, Button, Upload, message, Select, Slider, Switch } from 'ant-design-vue';
import type { UploadChangeParam } from 'ant-design-vue';
import { fabric } from 'fabric';
import Cropper from 'cropperjs';
import { requestClient } from '#/api/request';
import { saveDeceasedDetail } from '#/api/funeral/building';

import 'cropperjs/dist/cropper.css';

type EditMode = 'select' | 'crop' | 'draw' | 'text';

const emit = defineEmits(['saved']);

const visible = ref(false);
const saveLoading = ref(false);
const deceasedData = ref<any>(null);
const uploadMimeType = ref<string>('image/png'); // 투명도 유지용 MIME type 기록

const canvasRef = ref<HTMLCanvasElement | null>(null);
const canvasMountRef = ref<HTMLDivElement | null>(null);
let canvas: fabric.Canvas | null = null;
const currentMode = ref<EditMode>('select');

// 영역 밖 자르기(클리핑) 스위치 상태
const isClipEnabled = ref(true);

// 히스토리 관리
const undoStack = ref<string[]>([]);
const redoStack = ref<string[]>([]);
const isHistoryProcessing = ref(false);

// 그리기 설정
const brushColor = ref('#ff0000');
const brushWidth = ref(5);

// 텍스트 설정
const textColor = ref('#000000');
const fontSize = ref(28);

// 크롭(Cropper.js) 설정
const cropperImgRef = ref<HTMLImageElement | null>(null);
let cropperInstance: Cropper | null = null;
const cropRatio = ref<number | undefined>(3 / 4); // 영정사진 기본 3:4
const cropRatios = [
  { label: '자유 비율', value: undefined },
  { label: '3:4 (영정)', value: 3 / 4 },
  { label: '2:3 (세로)', value: 2 / 3 },
  { label: '5:7 (세로)', value: 5 / 7 },
  { label: '9:16 (세로)', value: 9 / 16 },
  { label: '10:16 (세로)', value: 10 / 16 },
  { label: '1:1 (정방)', value: 1 },
];

// 필터 설정
const currentFilter = ref<string>('none');
const filterOptions = [
  { label: '필터 없음', value: 'none' },
  { label: '흑백', value: 'grayscale' },
  { label: '세피아', value: 'sepia' },
  { label: '색상 반전', value: 'invert' },
];

const zoomRatio = ref(100);
let isPanning = false;
let lastPosX = 0;
let lastPosY = 0;
let isSpacePressed = false;

const MAX_IMAGE_SIZE = 1920;

// 히스토리 저장
function saveHistory() {
  if (!canvas || isHistoryProcessing.value) return;

  const json = JSON.stringify(canvas.toJSON());
  if (undoStack.value.length > 0 && undoStack.value[undoStack.value.length - 1] === json) {
    return;
  }

  undoStack.value.push(json);
  if (undoStack.value.length > 20) {
    undoStack.value.shift();
  }
  redoStack.value = [];
}

function initHistory() {
  undoStack.value = [];
  redoStack.value = [];
  if (canvas) {
    undoStack.value.push(JSON.stringify(canvas.toJSON()));
  }
}

function handleUndo() {
  if (!canvas || undoStack.value.length <= 1 || isHistoryProcessing.value) return;

  isHistoryProcessing.value = true;
  const current = undoStack.value.pop();
  if (current) {
    redoStack.value.push(current);
  }

  const previous = undoStack.value[undoStack.value.length - 1];
  if (previous) {
    canvas.loadFromJSON(previous, () => {
      updateClipPath(); // 캔버스 복원 시 클리핑 복구
      canvas?.requestRenderAll();
      isHistoryProcessing.value = false;
    });
  } else {
    isHistoryProcessing.value = false;
  }
}

// Redo
function handleRedo() {
  if (!canvas || redoStack.value.length === 0 || isHistoryProcessing.value) return;

  isHistoryProcessing.value = true;
  const next = redoStack.value.pop();
  if (next) {
    undoStack.value.push(next);
    canvas.loadFromJSON(next, () => {
      updateClipPath(); // 캔버스 복원 시 클리핑 복구
      canvas?.requestRenderAll();
      isHistoryProcessing.value = false;
    });
  } else {
    isHistoryProcessing.value = false;
  }
}

// 캔버스 초기화
function initEditor(imageSrc: string) {
  destroyEditor();
  if (!canvasRef.value) return;

  canvas = new fabric.Canvas(canvasRef.value, {
    backgroundColor: 'transparent',
    preserveObjectStacking: true,
  });

  canvas.on('object:added', () => saveHistory());
  canvas.on('object:modified', () => saveHistory());
  canvas.on('object:removed', () => saveHistory());

  // Alt + 마우스 휠 줌
  canvas.on('mouse:wheel', (opt) => {
    if (!canvas) return;
    const evt = opt.e;
    if (evt.altKey) {
      const delta = evt.deltaY;
      let zoom = canvas.getZoom();
      zoom *= 0.999 ** delta;
      if (zoom > 20) zoom = 20;
      if (zoom < 0.05) zoom = 0.05;
      canvas.zoomToPoint({ x: evt.offsetX, y: evt.offsetY }, zoom);
      zoomRatio.value = Math.round(zoom * 100);
      evt.preventDefault();
      evt.stopPropagation();
    }
  });

  // Space + 드래그 패닝
  canvas.on('mouse:down', (opt) => {
    const evt = opt.e;
    if (isSpacePressed) {
      isPanning = true;
      lastPosX = evt.clientX;
      lastPosY = evt.clientY;
      if (canvas) canvas.defaultCursor = 'grabbing';
    }
  });

  canvas.on('mouse:move', (opt) => {
    if (isPanning && canvas) {
      const e = opt.e;
      if (!e) return;
      const vpt = canvas.viewportTransform;
      if (vpt && typeof vpt[4] === 'number' && typeof vpt[5] === 'number') {
        vpt[4] += e.clientX - lastPosX;
        vpt[5] += e.clientY - lastPosY;
        canvas.requestRenderAll();
      }
      lastPosX = e.clientX;
      lastPosY = e.clientY;
    }
  });

  canvas.on('mouse:up', () => {
    isPanning = false;
    if (canvas) canvas.defaultCursor = isSpacePressed ? 'grab' : 'default';
  });

  if (imageSrc) {
    loadImageToCanvas(imageSrc);
  }
}

function destroyEditor() {
  if (canvas) {
    canvas.dispose();
    canvas = null;
  }
  if (cropperInstance) {
    cropperInstance.destroy();
    cropperInstance = null;
  }
}

// 이미지 배치
function loadImageToCanvas(url: string) {
  if (!canvas) return;

  fabric.Image.fromURL(url, (img) => {
    if (!canvas || !img) return;

    canvas.clear();

    const originalWidth = img.width ?? 800;
    const originalHeight = img.height ?? 600;

    let targetWidth = originalWidth;
    let targetHeight = originalHeight;
    if (targetWidth > MAX_IMAGE_SIZE || targetHeight > MAX_IMAGE_SIZE) {
      if (targetWidth > targetHeight) {
        targetHeight = Math.round((targetHeight * MAX_IMAGE_SIZE) / targetWidth);
        targetWidth = MAX_IMAGE_SIZE;
      } else {
        targetWidth = Math.round((targetWidth * MAX_IMAGE_SIZE) / targetHeight);
        targetHeight = MAX_IMAGE_SIZE;
      }
    }

    canvas.setWidth(targetWidth);
    canvas.setHeight(targetHeight);

    img.set({
      left: 0,
      top: 0,
      scaleX: targetWidth / originalWidth,
      scaleY: targetHeight / originalHeight,
      selectable: false,
      evented: false,
      lockMovementX: true,
      lockMovementY: true,
      lockScalingX: true,
      lockScalingY: true,
      lockRotation: true,
    });

    (img as any).isDeceasedBackground = true;

    canvas.add(img);
    canvas.sendToBack(img);
    
    updateClipPath(); // 이미지 로드 직후 클리핑 상태 적용
    // 화면에 맞춰 자동 축소 줌 및 중앙 배치 실행
    fitImageToScreen();

    currentMode.value = 'select';
    currentFilter.value = 'none';

    initHistory();

    // 모달 팝업이 활성화 모션을 마치고 에디터 가용 크기가 완전히 고정된 후 정확한 측정을 위한 2차 딜레이 피팅
    nextTick(() => {
      setTimeout(() => {
        fitImageToScreen();
      }, 150);
    });
  }, { crossOrigin: 'anonymous' });
}

// 모드 제어
function changeMode(mode: EditMode) {
  currentMode.value = mode;
  if (!canvas) return;

  canvas.isDrawingMode = false;

  if (cropperInstance) {
    cropperInstance.destroy();
    cropperInstance = null;
  }

  canvas.forEachObject((obj) => {
    if ((obj as any).isDeceasedBackground) return;
    obj.set({
      selectable: mode === 'select',
      evented: mode === 'select',
    });
  });

  if (mode === 'draw') {
    canvas.isDrawingMode = true;
    setupBrush();
  } else if (mode === 'crop') {
    // 1. 현재의 줌과 뷰포트 오프셋 임시 백업 (줌 왜곡 원천 차단)
    const originalZoom = canvas.getZoom();
    const originalVpt = canvas.viewportTransform ? [...canvas.viewportTransform] : null;

    // 2. 캔버스를 100% 줌(1) 및 원점(0,0)으로 리셋하여 순수 원본 비율로 이미지 추출
    canvas.setZoom(1);
    canvas.setViewportTransform([1, 0, 0, 1, 0, 0]);
    canvas.requestRenderAll();

    // 3. 줌의 영향을 받지 않는 순수한 실측 크기 이미지 추출
    const dataUrl = canvas.toDataURL({
      format: 'png',
      quality: 1,
    });

    // 4. 데이터 추출 완료 즉시 원래의 줌과 뷰포트 오프셋으로 원상 복원
    canvas.setZoom(originalZoom);
    if (originalVpt) {
      canvas.setViewportTransform(originalVpt);
    }
    canvas.requestRenderAll();

    if (cropperImgRef.value) {
      // 캐시 로드 타이밍을 놓치지 않기 위해 onload를 src 지정보다 먼저 선언
      cropperImgRef.value.onload = () => {
        // display: none -> block 오버레이 변환 레이아웃 갱신 완료 후 Cropper를 생성하도록 안전 딜레이 적용
        setTimeout(() => {
          initCropper();
        }, 50);
      };
      cropperImgRef.value.src = dataUrl;
    }
  }

  canvas.requestRenderAll();
}

// 자유 그리기 붓 설정
function setupBrush() {
  if (!canvas) return;

  const brush = new fabric.PencilBrush(canvas);
  brush.color = brushColor.value;
  brush.width = brushWidth.value;
  canvas.freeDrawingBrush = brush;
}

watch([brushColor, brushWidth], () => {
  if (currentMode.value === 'draw') {
    setupBrush();
  }
});

// 크롭 조절박스를 이미지의 가로/세로 한쪽 면이 꽉 차는 최대 크기로 정밀 핏팅
function fitCropBoxToImage() {
  if (!cropperInstance) return;

  const imageData = cropperInstance.getImageData();
  const imgWidth = imageData.naturalWidth;
  const imgHeight = imageData.naturalHeight;
  const ratio = cropRatio.value;

  if (ratio === undefined) {
    // 자유 비율일 때는 이미지 전체 영역 100% 할당
    cropperInstance.setData({
      x: 0,
      y: 0,
      width: imgWidth,
      height: imgHeight,
    });
    return;
  }

  const heightIfWidthMax = imgWidth / ratio;
  const widthIfHeightMax = imgHeight * ratio;

  let targetWidth = imgWidth;
  let targetHeight = imgHeight;
  let targetX = 0;
  let targetY = 0;

  if (heightIfWidthMax <= imgHeight) {
    // 가로가 100% 꽉 차고 세로에 여유가 생기는 상태
    targetWidth = imgWidth;
    targetHeight = heightIfWidthMax;
    targetX = 0;
    targetY = (imgHeight - heightIfWidthMax) / 2;
  } else {
    // 세로가 100% 꽉 차고 가로에 여유가 생기는 상태
    targetWidth = widthIfHeightMax;
    targetHeight = imgHeight;
    targetX = (imgWidth - widthIfHeightMax) / 2;
    targetY = 0;
  }

  cropperInstance.setData({
    x: targetX,
    y: targetY,
    width: targetWidth,
    height: targetHeight,
  });
}

// 크롭(Cropper.js) 제어
function initCropper() {
  if (cropperInstance) {
    cropperInstance.destroy();
  }
  if (!cropperImgRef.value) return;

  cropperInstance = new Cropper(cropperImgRef.value, {
    aspectRatio: cropRatio.value !== undefined ? cropRatio.value : undefined,
    viewMode: 2, // 이미지 전체 노출 보장 및 경계 이탈 방지
    dragMode: 'none', // 이미지 밀림 방지
    background: false, // 이미지 영역 밖 격자 노출 방지
    autoCropArea: 1, // 최대 영역으로 오토 크롭 설정
    restore: false,
    guides: true,
    center: true,
    highlight: false,
    cropBoxMovable: true,
    cropBoxResizable: true,
    toggleDragModeOnDblclick: false,
    ready() {
      fitCropBoxToImage();
    }
  });
}

watch(cropRatio, (newRatio) => {
  if (currentMode.value === 'crop' && cropperInstance) {
    cropperInstance.setAspectRatio(newRatio !== undefined ? newRatio : NaN);
    fitCropBoxToImage();
  }
});

// 크롭 실행
function applyCrop() {
  if (!canvas || !cropperInstance) return;

  const croppedCanvas = cropperInstance.getCroppedCanvas({
    imageSmoothingEnabled: true,
    imageSmoothingQuality: 'high',
    fillColor: 'transparent', // 투명도 유지 강제 지정
  });

  // 항상 png로 추출하여 투명도 보존
  const croppedDataUrl = croppedCanvas.toDataURL('image/png');

  cropperInstance.destroy();
  cropperInstance = null;

  canvas.clear();
  canvas.setWidth(croppedCanvas.width);
  canvas.setHeight(croppedCanvas.height);

  fabric.Image.fromURL(croppedDataUrl, (img) => {
    if (!canvas || !img) return;

    img.set({
      left: 0,
      top: 0,
      selectable: false,
      evented: false,
      lockMovementX: true,
      lockMovementY: true,
      lockScalingX: true,
      lockScalingY: true,
      lockRotation: true,
    });

    (img as any).isDeceasedBackground = true;

    canvas.add(img);
    canvas.sendToBack(img);
    
    updateClipPath(); // 크롭 완료 후 캔버스 변경에 의한 클리핑 재조정
    // 화면 맞춤 및 중앙 배치
    fitImageToScreen();

    changeMode('select');
    saveHistory();
  }, { crossOrigin: 'anonymous' });
}

// 텍스트 추가
function addText() {
  if (!canvas) return;

  const center = canvas.getVpCenter();
  const text = new fabric.IText('텍스트 입력', {
    left: center.x - 70,
    top: center.y - 20,
    fontSize: fontSize.value,
    fill: textColor.value,
    fontFamily: 'sans-serif',
  });

  canvas.add(text);
  canvas.setActiveObject(text);
  canvas.requestRenderAll();
  changeMode('select');
}

// 선택 텍스트 갱신
function updateSelectedText() {
  if (!canvas) return;
  const activeObj = canvas.getActiveObject() as fabric.IText | null;
  if (activeObj && (activeObj.type === 'i-text' || activeObj.type === 'text')) {
    activeObj.set({
      fill: textColor.value,
      fontSize: fontSize.value,
    });
    canvas.requestRenderAll();
    saveHistory();
  }
}

watch([textColor, fontSize], () => {
  updateSelectedText();
});

// 회전/대칭
function rotateCanvas(angle: number) {
  if (!canvas) return;

  isHistoryProcessing.value = true;
  const objs = canvas.getObjects();
  if (objs.length === 0) {
    isHistoryProcessing.value = false;
    return;
  }

  const group = new fabric.Group(objs, {
    originX: 'center',
    originY: 'center',
  });

  const oldWidth = canvas.width ?? 800;
  const oldHeight = canvas.height ?? 600;

  canvas.setWidth(oldHeight);
  canvas.setHeight(oldWidth);

  group.rotate((group.angle ?? 0) + angle);
  group.set({
    left: oldHeight / 2,
    top: oldWidth / 2,
  });

  canvas.add(group);
  group.destroy();
  canvas.remove(group);

  objs.forEach((obj) => {
    canvas?.add(obj);
  });

  canvas.requestRenderAll();
  isHistoryProcessing.value = false;

  saveHistory();
}

function flipCanvas() {
  if (!canvas) return;

  const objs = canvas.getObjects();
  if (objs.length === 0) return;

  const group = new fabric.Group(objs, {
    originX: 'center',
    originY: 'center',
    left: (canvas.width ?? 800) / 2,
    top: (canvas.height ?? 600) / 2,
  });

  group.set('flipX', !group.flipX);

  canvas.add(group);
  group.destroy();
  canvas.remove(group);

  objs.forEach((obj) => {
    canvas?.add(obj);
  });

  canvas.requestRenderAll();
  saveHistory();
}

// 필터 효과
function applyFilter(filterName: string) {
  if (!canvas) return;
  currentFilter.value = filterName;

  const bgImg = canvas.getObjects().find(obj => obj.type === 'image' && (obj as any).isDeceasedBackground) as fabric.Image;
  if (!bgImg) return;

  bgImg.filters = [];

  if (filterName === 'grayscale') {
    bgImg.filters.push(new fabric.Image.filters.Grayscale());
  } else if (filterName === 'sepia') {
    bgImg.filters.push(new fabric.Image.filters.Sepia());
  } else if (filterName === 'invert') {
    bgImg.filters.push(new fabric.Image.filters.Invert());
  }

  bgImg.applyFilters();
  canvas.requestRenderAll();
  saveHistory();
}

// 삭제
function deleteSelectedObject() {
  if (!canvas) return;
  const activeObj = canvas.getActiveObject();
  if (activeObj) {
    if ((activeObj as any).isDeceasedBackground) return;

    canvas.remove(activeObj);
    canvas.discardActiveObject();
    canvas.requestRenderAll();
    saveHistory();
  }
}

// 12. 화면 가용 영역에 맞춰 줌/팬 자동 피팅 (중앙 정렬)
function fitImageToScreen() {
  if (!canvas) return;
  
  const parentWidth = canvasMountRef.value ? (canvasMountRef.value.clientWidth || 740) : 740;
  const parentHeight = canvasMountRef.value ? (canvasMountRef.value.clientHeight || 440) : 440;

  const canvasWidth = canvas.width ?? 800;
  const canvasHeight = canvas.height ?? 600;

  const scaleX = (parentWidth * 0.95) / canvasWidth;
  const scaleY = (parentHeight * 0.95) / canvasHeight;
  // 배율이 0%로 찌그러지는 현상 방지를 위해 최소 스케일 가드(0.05) 설정
  const fitScale = Math.max(Math.min(scaleX, scaleY, 1), 0.05);

  canvas.setZoom(fitScale);
  zoomRatio.value = Math.round(fitScale * 100);

  const vpt = canvas.viewportTransform;
  if (vpt) {
    // Flexbox 배치로 캔버스 돔의 중앙이 이미 부모 중앙에 있으므로, 캔버스 돔 내부 드로잉 오프셋만 중앙 핏팅
    vpt[4] = (canvasWidth - canvasWidth * fitScale) / 2;
    vpt[5] = (canvasHeight - canvasHeight * fitScale) / 2;
    canvas.setViewportTransform(vpt); // 뷰포트 트랜스폼 설정 명시적 강제 반영
  }
  canvas.requestRenderAll();
}

// 캔버스 클리핑(이미지 영역 밖 마스킹) 처리
function updateClipPath() {
  if (!canvas) return;

  if (isClipEnabled.value) {
    const canvasWidth = canvas.width ?? 800;
    const canvasHeight = canvas.height ?? 600;

    canvas.clipPath = new fabric.Rect({
      left: 0,
      top: 0,
      width: canvasWidth,
      height: canvasHeight,
      absolutePositioned: true,
    });
  } else {
    canvas.clipPath = undefined;
  }
  canvas.requestRenderAll();
}

watch(isClipEnabled, () => {
  updateClipPath();
});

// 줌 배율 드롭다운 목록 구성
const currentZoomOptions = computed(() => {
  const defaults = [25, 50, 75, 100, 150, 200];
  const list = defaults.map(val => ({ label: `${val}%`, value: val }));
  
  if (!defaults.includes(zoomRatio.value)) {
    list.unshift({ label: `${zoomRatio.value}%`, value: zoomRatio.value });
  }
  
  list.push({ label: '화면 맞춤', value: 'fit' as any });
  return list;
});

// 줌 드롭다운 선택 이벤트 핸들러
function handleZoomSelectChange(value: number | 'fit') {
  if (!canvas) return;

  if (value === 'fit') {
    fitImageToScreen();
    return;
  }

  const zoomFactor = value / 100;
  const canvasWidth = canvas.width ?? 800;
  const canvasHeight = canvas.height ?? 600;

  // 캔버스 자체의 물리적 기하 중심점을 줌의 축으로 지정
  canvas.zoomToPoint({ x: canvasWidth / 2, y: canvasHeight / 2 }, zoomFactor);
  zoomRatio.value = value;
  canvas.requestRenderAll();
}

function resetZoomAndPan() {
  fitImageToScreen();
}

function resizeImageIfNeeded(dataUrl: string, maxDimension: number): Promise<string> {
  return new Promise((resolve) => {
    const img = new Image();
    img.src = dataUrl;
    img.onload = () => {
      const width = img.width;
      const height = img.height;

      if (width <= maxDimension && height <= maxDimension) {
        resolve(dataUrl);
        return;
      }

      let targetWidth = width;
      let targetHeight = height;

      if (width > height) {
        if (width > maxDimension) {
          targetHeight = Math.round((height * maxDimension) / width);
          targetWidth = maxDimension;
        }
      } else {
        if (height > maxDimension) {
          targetWidth = Math.round((width * maxDimension) / height);
          targetHeight = maxDimension;
        }
      }

      const canvasElement = document.createElement('canvas');
      canvasElement.width = targetWidth;
      canvasElement.height = targetHeight;

      const ctx = canvasElement.getContext('2d');
      if (ctx) {
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';
        ctx.drawImage(img, 0, 0, targetWidth, targetHeight);
        resolve(canvasElement.toDataURL('image/png'));
      } else {
        resolve(dataUrl);
      }
    };
    img.onerror = () => {
      resolve(dataUrl);
    };
  });
}

// 모달 조작 핸들러
function open(row: any) {
  deceasedData.value = { ...row };
  uploadMimeType.value = 'image/png';
  visible.value = true;

  let initialImage = '';
  if (row.memorialEditedPhotoFileId) {
    initialImage = `/api/file/download/${row.memorialEditedPhotoFileId}`;
  } else if (row.memorialEditedPhotoUrl) {
    initialImage = row.memorialEditedPhotoUrl;
  } else if (row.memorialPhotoFileId) {
    initialImage = `/api/file/download/${row.memorialPhotoFileId}`;
  } else if (row.memorialPhotoUrl) {
    initialImage = row.memorialPhotoUrl;
  }

  const lowerUrl = initialImage.toLowerCase();
  if (lowerUrl.endsWith('.png') || lowerUrl.includes('png')) {
    uploadMimeType.value = 'image/png';
  } else {
    uploadMimeType.value = 'image/jpeg';
  }

  nextTick(() => {
    initEditor(initialImage);
  });
}

const hasOriginalPhoto = computed(() => !!(deceasedData.value?.memorialPhotoFileId || deceasedData.value?.memorialPhotoUrl));
const hasEditedPhoto = computed(() => !!(deceasedData.value?.memorialEditedPhotoFileId || deceasedData.value?.memorialEditedPhotoUrl));

function loadOriginalPhoto() {
  if (!deceasedData.value) return;
  let url = '';
  if (deceasedData.value.memorialPhotoFileId) {
    url = `/api/file/download/${deceasedData.value.memorialPhotoFileId}`;
  } else if (deceasedData.value.memorialPhotoUrl) {
    url = deceasedData.value.memorialPhotoUrl;
  }
  if (url) {
    loadImageToCanvas(url);
    message.success('원본 영정사진을 불러왔습니다.');
  } else {
    message.warning('등록된 원본 영정사진이 없습니다.');
  }
}

function loadEditedPhoto() {
  if (!deceasedData.value) return;
  let url = '';
  if (deceasedData.value.memorialEditedPhotoFileId) {
    url = `/api/file/download/${deceasedData.value.memorialEditedPhotoFileId}`;
  } else if (deceasedData.value.memorialEditedPhotoUrl) {
    url = deceasedData.value.memorialEditedPhotoUrl;
  }
  if (url) {
    loadImageToCanvas(url);
    message.success('편집 완료된 영정사진을 불러왔습니다.');
  } else {
    message.warning('등록된 편집 영정사진이 없습니다.');
  }
}

// 외부 파일 로드
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
    if (canvas) {
      loadImageToCanvas(dataUrl);
    } else {
      initEditor(dataUrl);
    }
  });
  reader.readAsDataURL(file);
};

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

// 편집 내용 저장
const handleSave = async () => {
  if (!canvas) {
    message.warning('편집기에 로드된 이미지가 없습니다.');
    return;
  }
  if (!deceasedData.value) return;

  saveLoading.value = true;
  try {
    // 1. 현재의 줌과 뷰포트 오프셋 임시 백업 (저장 시 줌 왜곡 여백 원천 차단)
    const originalZoom = canvas.getZoom();
    const originalVpt = canvas.viewportTransform ? [...canvas.viewportTransform] : null;

    // 2. 캔버스를 100% 줌(1) 및 원점(0,0)으로 리셋하여 순수 실측 크기로 이미지 추출
    canvas.setZoom(1);
    canvas.setViewportTransform([1, 0, 0, 1, 0, 0]);
    canvas.requestRenderAll();

    // 3. 영역밖자르기(isClipEnabled) 활성화 여부에 따른 toDataURL 옵션 차등 적용
    const toDataUrlOptions: any = {
      format: 'image/png',
      quality: 1.0,
    };

    if (isClipEnabled.value) {
      toDataUrlOptions.left = 0;
      toDataUrlOptions.top = 0;
      toDataUrlOptions.width = canvas.width;
      toDataUrlOptions.height = canvas.height;
    }

    const dataURL = canvas.toDataURL(toDataUrlOptions);

    // 4. 데이터 추출 완료 즉시 원래의 줌과 뷰포트 오프셋으로 원상 복원
    canvas.setZoom(originalZoom);
    if (originalVpt) {
      canvas.setViewportTransform(originalVpt);
    }
    canvas.requestRenderAll();

    if (!dataURL) {
      throw new Error('편집 이미지 추출 실패');
    }

    // 최대 가로/세로 1200px 제한 리사이징 픽셀 다운샘플링
    const finalDataURL = await resizeImageIfNeeded(dataURL, 1200);

    const croppedBlob = dataURLtoBlob(finalDataURL);
    const fileName = 'deceased_photo_edited.png';
    const file = new File([croppedBlob], fileName, {
      type: 'image/png',
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
      memorialEditedPhotoFileId: newFileId,
      memorialEditedPhotoUrl: newDownloadUrl,
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

// 키보드 리스너
function handleKeyDown(e: KeyboardEvent) {
  const activeObj = canvas?.getActiveObject();
  if (activeObj && (activeObj as any).isEditing) return;

  if (e.key === 'Delete' || e.key === 'Backspace') {
    deleteSelectedObject();
  } else if (e.ctrlKey && e.key.toLowerCase() === 'z') {
    e.preventDefault();
    handleUndo();
  } else if (e.ctrlKey && e.key.toLowerCase() === 'y') {
    e.preventDefault();
    handleRedo();
  }
}

// Space panning 스위치
function handleGlobalKeyDown(e: KeyboardEvent) {
  if (e.code === 'Space') {
    const activeElement = document.activeElement;
    if (activeElement && (activeElement.tagName === 'INPUT' || activeElement.tagName === 'TEXTAREA' || activeElement.getAttribute('contenteditable'))) {
      return;
    }
    e.preventDefault();
    isSpacePressed = true;
    if (canvas) canvas.defaultCursor = 'grab';
  }
}

function handleGlobalKeyUp(e: KeyboardEvent) {
  if (e.code === 'Space') {
    isSpacePressed = false;
    if (canvas) canvas.defaultCursor = 'default';
  }
}

watch(visible, (newVal) => {
  if (newVal) {
    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keydown', handleGlobalKeyDown);
    window.addEventListener('keyup', handleGlobalKeyUp);
  } else {
    destroyEditor();
    window.removeEventListener('keydown', handleKeyDown);
    window.removeEventListener('keydown', handleGlobalKeyDown);
    window.removeEventListener('keyup', handleGlobalKeyUp);
  }
});

onUnmounted(() => {
  destroyEditor();
  window.removeEventListener('keydown', handleKeyDown);
  window.removeEventListener('keydown', handleGlobalKeyDown);
  window.removeEventListener('keyup', handleGlobalKeyUp);
});

defineExpose({ open });
</script>

<template>
  <Modal
    v-model:open="visible"
    title="고인 영정사진 편집 (Fabric.js + Cropper.js)"
    :width="780"
    :footer="null"
    destroy-on-close
    class="flat-modal"
  >
    <div class="modal-workspace-container flex flex-col p-0.5 rounded-none">
      <!-- 1. 툴바 제어 영역 (가로 배치 컴팩트 레이아웃) -->
      <div class="flex flex-wrap items-center justify-between gap-2 mb-1.5 p-1 rounded-none border border-gray-200">
        <!-- 모드 전환 -->
        <div class="flex items-center gap-1">
          <Button size="small" :type="currentMode === 'select' ? 'primary' : 'default'" @click="changeMode('select')" class="!rounded-none">이동</Button>
          <Button size="small" :type="currentMode === 'crop' ? 'primary' : 'default'" @click="changeMode('crop')" class="!rounded-none">자르기</Button>
          <Button size="small" :type="currentMode === 'draw' ? 'primary' : 'default'" @click="changeMode('draw')" class="!rounded-none">그리기</Button>
          <Button size="small" :type="currentMode === 'text' ? 'primary' : 'default'" @click="changeMode('text')" class="!rounded-none">글자 추가</Button>
          <!-- 영역 밖 자르기 스위치 -->
          <div class="flex items-center gap-1 ml-1 border-l border-gray-200 pl-2">
            <span class="text-xs text-gray-500 font-medium select-none">영역 밖 자르기:</span>
            <Switch v-model:checked="isClipEnabled" size="small" />
          </div>
        </div>

        <!-- 세부 옵션 상황판 -->
        <div class="flex items-center gap-2 text-xs">
          <!-- 자르기 옵션 -->
          <template v-if="currentMode === 'crop'">
            <span class="text-gray-500 font-medium">비율:</span>
            <Select v-model:value="cropRatio" size="small" class="w-24 !rounded-none">
              <Select.Option v-for="opt in cropRatios" :key="opt.label" :value="opt.value">{{ opt.label }}</Select.Option>
            </Select>
            <Button type="primary" danger size="small" class="px-3 !rounded-none" @click="applyCrop">실행</Button>
          </template>

          <!-- 그리기 옵션 -->
          <template v-if="currentMode === 'draw'">
            <span class="text-gray-500 font-medium">색상:</span>
            <input type="color" v-model="brushColor" class="w-6 h-5 p-0 border border-gray-300 rounded-none cursor-pointer" />
            <span class="text-gray-500 font-medium ml-1">두께:</span>
            <Slider v-model:value="brushWidth" :min="1" :max="30" class="w-16 m-0" />
          </template>

          <!-- 글자 옵션 -->
          <template v-if="currentMode === 'text'">
            <span class="text-gray-500 font-medium">색상:</span>
            <input type="color" v-model="textColor" class="w-6 h-5 p-0 border border-gray-300 rounded-none cursor-pointer" />
            <span class="text-gray-500 font-medium ml-1">크기:</span>
            <Slider v-model:value="fontSize" :min="10" :max="80" class="w-16 m-0" />
            <Button type="primary" size="small" @click="addText" class="!rounded-none">글자 상자 추가</Button>
          </template>

          <!-- 선택/이동 기본 옵션 -->
          <template v-if="currentMode === 'select'">
            <Button size="small" @click="rotateCanvas(90)" class="!rounded-none">회전</Button>
            <Button size="small" @click="flipCanvas" class="!rounded-none">대칭</Button>
            <Select v-model:value="currentFilter" size="small" class="w-20 !rounded-none" @change="(val) => applyFilter(String(val))">
              <Select.Option v-for="opt in filterOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</Select.Option>
            </Select>
            <Button size="small" danger @click="deleteSelectedObject" class="!rounded-none">삭제</Button>
          </template>
        </div>
      </div>

      <!-- 2. 중앙 캔버스 작업 공간 -->
      <div ref="canvasMountRef" class="canvas-mount-wrapper relative border border-gray-200 rounded-none flex items-center justify-center p-2 overflow-hidden" style="height: 440px;">
        <div class="absolute bottom-2 right-2 z-10 flex items-center gap-1 bg-gray-900 bg-opacity-80 text-white text-[10px] px-1.5 py-0.5 rounded-none select-none border border-gray-700">
          <span class="text-gray-400 pl-1">배율:</span>
          <Select
            v-model:value="zoomRatio"
            size="small"
            :options="currentZoomOptions"
            class="zoom-select-dropdown w-20"
            :bordered="false"
            @change="(val) => handleZoomSelectChange(val as number | 'fit')"
          />
          <span class="text-gray-600">|</span>
          <button class="hover:text-blue-400 focus:outline-none pr-1" @click="resetZoomAndPan">초기화</button>
        </div>

        <div class="canvas-border-box shadow-none border border-gray-300 relative max-w-full max-h-full overflow-hidden flex items-center justify-center checkerboard-bg rounded-none">
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

      <!-- 3. 모달 하단 액션 바 -->
      <div class="flex items-center justify-between border-t border-gray-100 pt-2 mt-2">
        <!-- 파일 교체 및 Undo/Redo 컴팩트 툴바 -->
        <div class="flex items-center gap-2">
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

          <div class="flex items-center border border-gray-200 p-0.5 ml-2 rounded-none">
            <Button type="text" size="small" :disabled="undoStack.length <= 1" @click="handleUndo" class="flex items-center justify-center p-0.5 !rounded-none">
              <svg class="w-3.5 h-3.5 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12.066 11.2a1 1 0 000 1.6l5.334 4A1 1 0 0019 16V8a1 1 0 00-1.6-.8l-5.334 4zM4.066 11.2a1 1 0 000 1.6l5.334 4A1 1 0 0011 16V8a1 1 0 00-1.6-.8l-5.334 4z" /></svg>
            </Button>
            <Button type="text" size="small" :disabled="redoStack.length === 0" @click="handleRedo" class="flex items-center justify-center p-0.5 !rounded-none">
              <svg class="w-3.5 h-3.5 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11.934 12.8a1 1 0 000-1.6l-5.334-4A1 1 0 005 8v8a1 1 0 001.6.8l5.334-4zM19.934 12.8a1 1 0 000-1.6l-5.334-4A1 1 0 0013 8v8a1 1 0 001.6.8l5.334-4z" /></svg>
            </Button>
          </div>
        </div>

        <div class="flex gap-2">
          <Button size="small" @click="visible = false">취소</Button>
          <Button
            type="primary"
            size="small"
            :loading="saveLoading"
            @click="handleSave"
          >
            저장
          </Button>
        </div>
      </div>
    </div>
  </Modal>
</template>

<style scoped>
.modal-workspace-container {
  width: 100%;
  box-sizing: border-box;
}

/* 모달 및 내부 모든 컴포넌트 모서리 완전 제거 (Flat) */
:deep(*) {
  border-radius: 0px !important;
}

/* 줌 셀렉트 스타일 커스텀 */
:deep(.zoom-select-dropdown .ant-select-selector) {
  color: #ffffff !important;
  background-color: transparent !important;
  font-size: 11px !important;
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

:deep(.cropper-container) {
  max-width: 100% !important;
  max-height: 100% !important;
}
</style>
