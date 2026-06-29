import { ref, nextTick, computed, watch } from 'vue';
import type { Ref } from 'vue';
import { message } from 'ant-design-vue';
import type { UploadChangeParam } from 'ant-design-vue';
import { fabric } from 'fabric';
import Cropper from 'cropperjs';
import { requestClient } from '#/api/request';
import { getDeceasedDetail, saveDeceasedDetail } from '#/api/building';

export type EditMode = 'select' | 'crop' | 'draw' | 'text' | 'shape';
export type ShapeType = 'rect' | 'circle';

export function usePhotoEditor(params: {
  canvasRef: Ref<HTMLCanvasElement | null>;
  editorContainerRef: Ref<HTMLDivElement | null>;
  cropperImgRef: Ref<HTMLImageElement | null>;
}) {
  const { canvasRef, editorContainerRef, cropperImgRef } = params;

  const deceasedId = ref<string>('');
  const deceasedData = ref<any>(null);
  const uploadMimeType = ref<string>('image/png');

  let canvas: fabric.Canvas | null = null;
  let cropperInstance: Cropper | null = null;

  const saveLoading = ref(false);
  const pageLoading = ref(true);
  const currentMode = ref<EditMode>('select');

  // 실행 취소/다시 실행 스택
  const undoStack = ref<string[]>([]);
  const redoStack = ref<string[]>([]);
  const isHistoryProcessing = ref(false);

  // 그리기 설정
  const brushColor = ref('#ff0000');
  const brushWidth = ref(5);

  // 텍스트 설정
  const textColor = ref('#000000');
  const fontSize = ref(30);

  // 도형 설정
  const selectedShape = ref<ShapeType>('rect');
  const shapeColor = ref('#0000ff');

  // 크롭 설정
  const cropRatio = ref<number | undefined>(3 / 4);
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
    { label: '따뜻한 톤', value: 'warm' },
  ];

  // 배율 및 팬 설정
  const zoomRatio = ref(100);
  let isPanning = false;
  let lastPosX = 0;
  let lastPosY = 0;
  let isSpacePressed = false;

  const MAX_IMAGE_SIZE = 1920;

  // 히스토리 제어
  function saveHistory() {
    if (!canvas || isHistoryProcessing.value) return;

    const json = JSON.stringify(canvas.toJSON());
    if (undoStack.value.length > 0 && undoStack.value[undoStack.value.length - 1] === json) {
      return;
    }

    undoStack.value.push(json);
    if (undoStack.value.length > 30) {
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
        canvas?.requestRenderAll();
        isHistoryProcessing.value = false;
      });
    } else {
      isHistoryProcessing.value = false;
    }
  }

  function handleRedo() {
    if (!canvas || redoStack.value.length === 0 || isHistoryProcessing.value) return;

    isHistoryProcessing.value = true;
    const next = redoStack.value.pop();
    if (next) {
      undoStack.value.push(next);
      canvas.loadFromJSON(next, () => {
        canvas?.requestRenderAll();
        isHistoryProcessing.value = false;
      });
    } else {
      isHistoryProcessing.value = false;
    }
  }

  // 에디터 생성 및 정리
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

  // 이미지 로드 및 맞춤 정렬
  function loadImageToCanvas(url: string) {
    if (!canvas) return;
    pageLoading.value = true;

    fabric.Image.fromURL(url, (img) => {
      if (!canvas || !img) {
        pageLoading.value = false;
        message.error('이미지를 로드하지 못했습니다.');
        return;
      }

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
      
      fitImageToScreen();

      currentMode.value = 'select';
      currentFilter.value = 'none';

      initHistory();
      pageLoading.value = false;
    }, { crossOrigin: 'anonymous' });
  }

  // 모드 변경
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
      if (canvas.viewportTransform) {
        canvas.viewportTransform[0] = 1;
        canvas.viewportTransform[3] = 1;
        canvas.viewportTransform[4] = 0;
        canvas.viewportTransform[5] = 0;
      }
      canvas.requestRenderAll();

      // 3. 줌의 영향을 받지 않는 순수한 실측 크기 이미지 추출
      const dataUrl = canvas.toDataURL({
        format: 'png',
        quality: 1,
      });

      // 4. 데이터 추출 완료 즉시 원래의 줌과 뷰포트 오프셋으로 원상 복원
      canvas.setZoom(originalZoom);
      if (originalVpt && canvas.viewportTransform) {
        canvas.viewportTransform[0] = originalVpt[0];
        canvas.viewportTransform[1] = originalVpt[1];
        canvas.viewportTransform[2] = originalVpt[2];
        canvas.viewportTransform[3] = originalVpt[3];
        canvas.viewportTransform[4] = originalVpt[4];
        canvas.viewportTransform[5] = originalVpt[5];
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

  // 브러쉬 설정
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

  // 도형 추가
  function addShape() {
    if (!canvas) return;

    const center = canvas.getVpCenter();
    let shape: fabric.Object;

    if (selectedShape.value === 'rect') {
      shape = new fabric.Rect({
        left: center.x - 50,
        top: center.y - 50,
        width: 100,
        height: 100,
        fill: shapeColor.value,
        stroke: '#000000',
        strokeWidth: 1,
      });
    } else {
      shape = new fabric.Circle({
        left: center.x - 50,
        top: center.y - 50,
        radius: 50,
        fill: shapeColor.value,
        stroke: '#000000',
        strokeWidth: 1,
      });
    }

    canvas.add(shape);
    canvas.setActiveObject(shape);
    canvas.requestRenderAll();
    changeMode('select');
  }

  // 텍스트 추가
  function addText() {
    if (!canvas) return;

    const center = canvas.getVpCenter();
    const text = new fabric.IText('더블클릭하여 입력', {
      left: center.x - 100,
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

  // Cropper 초기화 및 실행
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
      autoCropArea: 0.8,
      restore: false,
      guides: true,
      center: true,
      highlight: false,
      cropBoxMovable: true,
      cropBoxResizable: true,
      toggleDragModeOnDblclick: false,
    });
  }

  watch(cropRatio, (newRatio) => {
    if (currentMode.value === 'crop' && cropperInstance) {
      cropperInstance.setAspectRatio(newRatio !== undefined ? newRatio : NaN);
    }
  });

  function applyCrop() {
    if (!canvas || !cropperInstance) return;

    const croppedCanvas = cropperInstance.getCroppedCanvas({
      imageSmoothingEnabled: true,
      imageSmoothingQuality: 'high',
      fillColor: 'transparent',
    });

    const croppedDataUrl = croppedCanvas.toDataURL('image/png');

    cropperInstance.destroy();
    cropperInstance = null;

    canvas.clear();
    canvas.setWidth(croppedCanvas.width);
    canvas.setHeight(croppedCanvas.height);

    pageLoading.value = true;
    fabric.Image.fromURL(croppedDataUrl, (img) => {
      if (!canvas || !img) {
        pageLoading.value = false;
        return;
      }

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
      
      fitImageToScreen();

      changeMode('select');
      saveHistory();
      pageLoading.value = false;
    }, { crossOrigin: 'anonymous' });
  }

  // 회전 / 반전 / 필터
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

  function flipCanvas(axis: 'X' | 'Y') {
    if (!canvas) return;

    const objs = canvas.getObjects();
    if (objs.length === 0) return;

    const group = new fabric.Group(objs, {
      originX: 'center',
      originY: 'center',
      left: (canvas.width ?? 800) / 2,
      top: (canvas.height ?? 600) / 2,
    });

    if (axis === 'X') {
      group.set('flipX', !group.flipX);
    } else {
      group.set('flipY', !group.flipY);
    }

    canvas.add(group);
    group.destroy();
    canvas.remove(group);

    objs.forEach((obj) => {
      canvas?.add(obj);
    });

    canvas.requestRenderAll();
    saveHistory();
  }

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
    } else if (filterName === 'warm') {
      bgImg.filters.push(new fabric.Image.filters.BlendColor({
        color: '#ffcc99',
        mode: 'multiply',
        alpha: 0.2,
      }));
    }

    bgImg.applyFilters();
    canvas.requestRenderAll();
    saveHistory();
  }

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

  // 뷰포트 피팅 및 리셋
  function fitImageToScreen() {
    if (!canvas || !editorContainerRef.value) return;

    const parentElement = editorContainerRef.value.parentElement;
    const parentWidth = parentElement ? (parentElement.clientWidth || 800) : 800;
    const parentHeight = parentElement ? (parentElement.clientHeight || 600) : 600;

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
      vpt[4] = (parentWidth - canvasWidth * fitScale) / 2;
      vpt[5] = (parentHeight - canvasHeight * fitScale) / 2;
      canvas.setViewportTransform(vpt);
    }
    canvas.requestRenderAll();
  }

  const currentZoomOptions = computed(() => {
    const defaults = [25, 50, 75, 100, 150, 200];
    const list = defaults.map(val => ({ label: `${val}%`, value: val }));
    
    if (!defaults.includes(zoomRatio.value)) {
      list.unshift({ label: `${zoomRatio.value}%`, value: zoomRatio.value });
    }
    
    list.push({ label: '화면 맞춤', value: 'fit' as any });
    return list;
  });

  function handleZoomSelectChange(value: number | 'fit') {
    if (!canvas) return;

    if (value === 'fit') {
      fitImageToScreen();
      return;
    }

    const zoomFactor = value / 100;
    const parentWidth = editorContainerRef.value?.clientWidth ?? 800;
    const parentHeight = editorContainerRef.value?.clientHeight ?? 600;

    canvas.zoomToPoint({ x: parentWidth / 2, y: parentHeight / 2 }, zoomFactor);
    zoomRatio.value = value;
    canvas.requestRenderAll();
  }

  function resetZoomAndPan() {
    fitImageToScreen();
  }

  // 서버 통신
  async function loadDeceasedInfo(idVal: string) {
    if (!idVal) return;
    deceasedId.value = idVal;

    try {
      const res = await getDeceasedDetail(idVal);
      const detail = (res as any)?.result?.[0] ?? res;
      if (!detail) {
        throw new Error('고인 상세 정보를 찾을 수 없습니다.');
      }
      deceasedData.value = detail;

      let initialImage = '';
      if (detail.memorialPhotoFileId) {
        initialImage = `/api/file/download/${detail.memorialPhotoFileId}`;
      } else if (detail.memorialPhotoUrl) {
        initialImage = detail.memorialPhotoUrl;
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
    } catch (error) {
      console.error('고인 정보 로드 실패:', error);
      message.error('고인 상세 정보를 불러오지 못했습니다.');
      pageLoading.value = false;
    }
  }

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

  const handleSave = async () => {
    if (!canvas) {
      message.warning('편집기에 로드된 이미지가 없습니다.');
      return;
    }
    if (!deceasedData.value) return;

    saveLoading.value = true;
    try {
      const dataURL = canvas.toDataURL({
        format: 'png',
        quality: 1.0,
      });

      if (!dataURL) {
        throw new Error('편집 이미지 추출 실패');
      }

      const croppedBlob = dataURLtoBlob(dataURL);
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
        memorialPhotoFileId: newFileId,
        memorialPhotoUrl: newDownloadUrl,
      };

      await saveDeceasedDetail(deceasedId.value, updateParams);

      message.success('고인 영정사진 편집 및 저장이 완료되었습니다.');
      
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

  function handleClose() {
    window.close();
  }

  // 키보드 단축키
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

  return {
    deceasedId,
    deceasedData,
    uploadMimeType,
    canvasRef,
    editorContainerRef,
    cropperImgRef,
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
    cropRatio,
    cropRatios,
    currentFilter,
    filterOptions,
    zoomRatio,
    currentZoomOptions,
    fitImageToScreen,
    handleZoomSelectChange,
    resetZoomAndPan,
    loadDeceasedInfo,
    selectImgFile,
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
    initEditor,
    destroyEditor,
    handleUndo,
    handleRedo,
  };
}
