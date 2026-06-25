<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';

// 컴포넌트 Props 정의
const props = defineProps<{
  /** 자르기 비율 형식 예: '1:1', '16:9', '3:4' 등 (선택 사항) */
  aspectRatio?: string;
  /** 컨테이너 높이 (기본값 400) */
  height?: number;
  /** 이미지 경로 */
  img: string;
  /** 컨테이너 너비 (기본값 500) */
  width?: number;
}>();

const CROPPER_CONSTANTS = {
  MIN_WIDTH: 60 as const,
  MIN_HEIGHT: 60 as const,
  DEFAULT_WIDTH: 500 as const,
  DEFAULT_HEIGHT: 400 as const,
  PADDING_RATIO: 0.1 as const,
  MAX_PADDING: 50 as const,
} as const;

type Point = [number, number]; // [clientX, clientY]
type Dimension = [number, number, number, number]; // [top, right, bottom, left]

// 드래그 포인트 타입
type DragAction =
  | 'bottom'
  | 'bottom-left'
  | 'bottom-right'
  | 'left'
  | 'move'
  | 'right'
  | 'top'
  | 'top-left'
  | 'top-right';

// DOM 참조
const containerRef = ref<HTMLDivElement | null>(null);
const bgImageRef = ref<HTMLImageElement | null>(null);
// const maskRef = ref<HTMLDivElement | null>(null);
const maskViewRef = ref<HTMLDivElement | null>(null);
const cropperRef = ref<HTMLDivElement | null>(null);
// const cropperViewRef = ref<HTMLDivElement | null>(null);

// 반응형 데이터
const isCropperVisible = ref<boolean>(false);
const validAspectRatio = ref<null | number>(null); // 유효한 비율 값 (null은 고정 비율 없음)
const containerWidth = ref<number>(
  props.width ?? CROPPER_CONSTANTS.DEFAULT_WIDTH,
);
const containerHeight = ref<number>(
  props.height ?? CROPPER_CONSTANTS.DEFAULT_HEIGHT,
);

// 자르기 영역 크기 (top, right, bottom, left)
const currentDimension = ref<Dimension>([50, 50, 50, 50]);
const initDimension = ref<Dimension>([50, 50, 50, 50]);

// 드래그 상태
const dragging = ref<boolean>(false);
const startPoint = ref<Point>([0, 0]);
const startDimension = ref<Dimension>([0, 0, 0, 0]);
const direction = ref<Dimension>([0, 0, 0, 0]);
const moving = ref<boolean>(false);

/**
 * 이미지의 적합한 크기를 계산하여 전체가 표시되고 최대 너비/높이 제한을 초과하지 않도록 함
 */
const calculateImageFitSize = () => {
  if (!bgImageRef.value) return;

  // 이미지 원본 크기 가져오기
  const imgWidth = bgImageRef.value.naturalWidth;
  const imgHeight = bgImageRef.value.naturalHeight;

  if (imgWidth === 0 || imgHeight === 0) return;

  // 스케일 비율 계산 (입력된 width/height 사용, 기본 500/400)
  const widthRatio =
    (props.width ?? CROPPER_CONSTANTS.DEFAULT_WIDTH) / imgWidth;
  const heightRatio =
    (props.height ?? CROPPER_CONSTANTS.DEFAULT_HEIGHT) / imgHeight;
  const scaleRatio = Math.min(widthRatio, heightRatio, 1); // 이미지를 확대하지 않고 축소만 함

  // 조정된 컨테이너 크기 계산
  const fitWidth = Math.floor(imgWidth * scaleRatio);
  const fitHeight = Math.floor(imgHeight * scaleRatio);

  containerWidth.value = fitWidth;
  containerHeight.value = fitHeight;

  // 자르기 상자 초기 크기 재설정 (새 컨테이너 크기 기준)
  const padding = Math.min(
    CROPPER_CONSTANTS.MAX_PADDING,
    Math.floor(fitWidth * CROPPER_CONSTANTS.PADDING_RATIO),
    Math.floor(fitHeight * CROPPER_CONSTANTS.PADDING_RATIO),
  );

  initDimension.value = [padding, padding, padding, padding];
  currentDimension.value = [padding, padding, padding, padding];
};

/**
 * 비율 문자열 검증 및 파싱
 * @returns {number|null} 비율 값 (width/height), 파싱 실패 시 null 반환
 */
const parseAndValidateAspectRatio = (): null | number => {
  // 비율 매개변수가 전달되지 않은 경우 null 반환
  if (!props.aspectRatio) {
    return null;
  }

  // 비율 형식 검증
  const ratioRegex = /^[1-9]\d*:[1-9]\d*$/;
  if (!ratioRegex.test(props.aspectRatio)) {
    console.warn('자르기 비율 형식이 잘못되었습니다. "숫자:숫자" 형식이어야 합니다. 예: "16:9"');
    return null;
  }

  // 비율 파싱
  const [width, height] = props.aspectRatio.split(':').map(Number);

  // 파싱 결과 유효성 검증
  if (Number.isNaN(width) || Number.isNaN(height) || !width || !height) {
    console.warn('자르기 비율 파싱에 실패했습니다. 너비와 높이는 양의 정수여야 합니다.');
    return null;
  }

  return width / height;
};

/**
 * 자르기 영역 크기 설정
 * @param {Dimension} dimension - [top, right, bottom, left]
 */
const setDimension = (dimension: Dimension) => {
  currentDimension.value = [...dimension];
  if (maskViewRef.value) {
    maskViewRef.value.style.clipPath = `inset(${dimension[0]}px ${dimension[1]}px ${dimension[2]}px ${dimension[3]}px)`;
  }
};

/**
 * 자르기 영역을 지정된 비율로 조정
 */
const adjustCropperToAspectRatio = () => {
  if (!cropperRef.value) return;

  // 비율 검증 및 파싱
  validAspectRatio.value = parseAndValidateAspectRatio();

  // 유효한 비율이 없으면 초기 크기를 사용하며 고정 비율을 강제하지 않음
  if (validAspectRatio.value === null) {
    setDimension(initDimension.value);
    return;
  }

  // 유효한 비율이 있으면 비율에 따라 자르기 상자 조정
  const ratio = validAspectRatio.value;
  const containerWidthVal = containerWidth.value;
  const containerHeightVal = containerHeight.value;

  // 비율에 따라 자르기 상자 크기 계산
  let newHeight: number, newWidth: number;

  // 너비 우선 계산
  newWidth = containerWidthVal;
  newHeight = newWidth / ratio;

  // 높이가 컨테이너를 초과하면 높이 우선 계산
  if (newHeight > containerHeightVal) {
    newHeight = containerHeightVal;
    newWidth = newHeight * ratio;
  }

  // 중앙 표시
  const leftRight = (containerWidthVal - newWidth) / 2;
  const topBottom = (containerHeightVal - newHeight) / 2;

  const newDimension: Dimension = [topBottom, leftRight, topBottom, leftRight];

  setDimension(newDimension);
};

/**
 * 크로퍼 생성
 */
const createCropper = () => {
  // 이미지 적합 크기 계산
  calculateImageFitSize();

  isCropperVisible.value = true;
  adjustCropperToAspectRatio();
};

/**
 * 마우스 다운 이벤트 처리
 * @param {MouseEvent} e - 마우스 이벤트
 * @param {DragAction} action - 작업 타입
 */
const handleMouseDown = (e: MouseEvent, action: DragAction) => {
  dragging.value = true;
  startPoint.value = [e.clientX, e.clientY];
  startDimension.value = [...currentDimension.value];
  direction.value = [0, 0, 0, 0];
  moving.value = false;

  // 이동 처리
  if (action === 'move') {
    direction.value[0] = 1;
    direction.value[2] = -1;
    direction.value[3] = 1;
    direction.value[1] = -1;
    moving.value = true;
    return;
  }

  // 드래그 방향 처리
  switch (action) {
    case 'bottom': {
      direction.value[2] = -1;
      break;
    }
    case 'bottom-left': {
      direction.value[2] = -1;
      direction.value[3] = 1;
      break;
    }
    case 'bottom-right': {
      direction.value[2] = -1;
      direction.value[1] = -1;
      break;
    }
    case 'left': {
      direction.value[3] = 1;
      break;
    }
    case 'right': {
      direction.value[1] = -1;
      break;
    }
    case 'top': {
      direction.value[0] = 1;
      break;
    }
    case 'top-left': {
      direction.value[0] = 1;
      direction.value[3] = 1;
      break;
    }
    case 'top-right': {
      direction.value[0] = 1;
      direction.value[1] = -1;
      break;
    }
  }
};

/**
 * 마우스 이동 이벤트 처리
 * @param {MouseEvent} e - 마우스 이벤트
 */
const handleMouseMove = (e: MouseEvent) => {
  if (!dragging.value || !cropperRef.value) return;

  const { clientX, clientY } = e;
  const diffX = clientX - startPoint.value[0];
  const diffY = clientY - startPoint.value[1];

  // 자르기 상자 이동 처리
  if (moving.value) {
    handleMoveCropBox(diffX, diffY);
    return;
  }

  // 유효한 비율 없음
  if (validAspectRatio.value === null) {
    handleFreeAspectResize(diffX, diffY);
  } else {
    handleFixedAspectResize(diffX, diffY);
  }
};

const handleMoveCropBox = (diffX: number, diffY: number) => {
  const newDimension = [...startDimension.value] as Dimension;

  // 임시 오프셋 후 위치 계산
  const tempTop = startDimension.value[0] + diffY;
  const tempLeft = startDimension.value[3] + diffX;

  // 자르기 상자의 고정 크기 계산
  const cropWidth =
    containerWidth.value - startDimension.value[3] - startDimension.value[1];
  const cropHeight =
    containerHeight.value - startDimension.value[0] - startDimension.value[2];

  // 경계 제한: 자르기 상자가 컨테이너 내부에 완전히 있고 크기가 변하지 않도록 함
  // 상단 경계: top >= 0, bottom = 컨테이너 높이 - top - 자르기 높이 >= 0
  newDimension[0] = Math.max(
    0,
    Math.min(tempTop, containerHeight.value - cropHeight),
  );
  // 하단 경계: bottom = 컨테이너 높이 - top - 자르기 높이 (top에서 유도됨, 추가 계산 불필요)
  newDimension[2] = containerHeight.value - newDimension[0] - cropHeight;
  // 좌측 경계: left >= 0, right = 컨테이너 너비 - left - 자르기 너비 >= 0
  newDimension[3] = Math.max(
    0,
    Math.min(tempLeft, containerWidth.value - cropWidth),
  );
  // 우측 경계: right = 컨테이너 너비 - left - 자르기 너비 (left에서 유도됨, 추가 계산 불필요)
  newDimension[1] = containerWidth.value - newDimension[3] - cropWidth;

  // 크기 불변 강제 보장 (백업)
  const finalWidth = containerWidth.value - newDimension[3] - newDimension[1];
  const finalHeight = containerHeight.value - newDimension[0] - newDimension[2];

  if (finalWidth !== cropWidth) {
    newDimension[1] = containerWidth.value - newDimension[3] - cropWidth;
  }

  if (finalHeight !== cropHeight) {
    newDimension[2] = containerHeight.value - newDimension[0] - cropHeight;
  }

  // 자르기 영역 업데이트 (위치만 변경, 크기/비율은 불변)
  setDimension(newDimension);
};

const handleFreeAspectResize = (diffX: number, diffY: number) => {
  const cropperWidth = containerWidth.value;
  const cropperHeight = containerHeight.value;
  const currentDimensionNew: Dimension = [0, 0, 0, 0];

  // 최소값보다 작지 않도록 새로운 크기 계산
  currentDimensionNew[0] = Math.min(
    Math.max(startDimension.value[0] + direction.value[0] * diffY, 0),
    cropperHeight - CROPPER_CONSTANTS.MIN_HEIGHT,
  );

  currentDimensionNew[1] = Math.min(
    Math.max(startDimension.value[1] + direction.value[1] * diffX, 0),
    cropperWidth - CROPPER_CONSTANTS.MIN_WIDTH,
  );

  currentDimensionNew[2] = Math.min(
    Math.max(startDimension.value[2] + direction.value[2] * diffY, 0),
    cropperHeight - CROPPER_CONSTANTS.MIN_HEIGHT,
  );

  currentDimensionNew[3] = Math.min(
    Math.max(startDimension.value[3] + direction.value[3] * diffX, 0),
    cropperWidth - CROPPER_CONSTANTS.MIN_WIDTH,
  );

  // 자르기 영역의 너비와 높이가 최소값보다 작지 않도록 함
  const newWidth =
    cropperWidth - currentDimensionNew[3] - currentDimensionNew[1];
  const newHeight =
    cropperHeight - currentDimensionNew[0] - currentDimensionNew[2];

  if (newWidth < CROPPER_CONSTANTS.MIN_WIDTH) {
    if (direction.value[3] === 1) {
      currentDimensionNew[3] =
        cropperWidth - currentDimensionNew[1] - CROPPER_CONSTANTS.MIN_WIDTH;
    } else {
      currentDimensionNew[1] =
        cropperWidth - currentDimensionNew[3] - CROPPER_CONSTANTS.MIN_WIDTH;
    }
  }

  if (newHeight < CROPPER_CONSTANTS.MIN_HEIGHT) {
    if (direction.value[0] === 1) {
      currentDimensionNew[0] =
        cropperHeight - currentDimensionNew[2] - CROPPER_CONSTANTS.MIN_HEIGHT;
    } else {
      currentDimensionNew[2] =
        cropperHeight - currentDimensionNew[0] - CROPPER_CONSTANTS.MIN_HEIGHT;
    }
  }

  setDimension(currentDimensionNew);
};

const handleFixedAspectResize = (diffX: number, diffY: number) => {
  if (validAspectRatio.value === null) return;
  const cropperWidth = containerWidth.value;
  const cropperHeight = containerHeight.value;
  // 유효 비율 있음 - 고정 비율 자르기
  const ratio = validAspectRatio.value;
  const currentWidth =
    cropperWidth - startDimension.value[3] - startDimension.value[1];
  const currentHeight =
    cropperHeight - startDimension.value[0] - startDimension.value[2];

  let newHeight: number, newWidth: number;
  let widthChange = 0;
  let heightChange = 0;

  // 너비/높이 변화량 계산
  if (direction.value[3] === 1) widthChange = -diffX;
  else if (direction.value[1] === -1) widthChange = diffX;

  if (direction.value[0] === 1) heightChange = -diffY;
  else if (direction.value[2] === -1) heightChange = diffY;

  const isCornerDrag =
    (direction.value[3] === 1 || direction.value[1] === -1) &&
    (direction.value[0] === 1 || direction.value[2] === -1);

  // 새로운 크기 계산
  if (isCornerDrag) {
    if (Math.abs(widthChange) > Math.abs(heightChange)) {
      newWidth = Math.max(
        CROPPER_CONSTANTS.MIN_WIDTH,
        currentWidth + widthChange,
      );
      newHeight = newWidth / ratio;
    } else {
      newHeight = Math.max(
        CROPPER_CONSTANTS.MIN_HEIGHT,
        currentHeight + heightChange,
      );
      newWidth = newHeight * ratio;
    }
  } else {
    if (direction.value[3] === 1 || direction.value[1] === -1) {
      newWidth = Math.max(
        CROPPER_CONSTANTS.MIN_WIDTH,
        currentWidth + widthChange,
      );
      newHeight = newWidth / ratio;
    } else {
      newHeight = Math.max(
        CROPPER_CONSTANTS.MIN_HEIGHT,
        currentHeight + heightChange,
      );
      newWidth = newHeight * ratio;
    }
  }

  // 최대 크기 제한
  const maxWidth = cropperWidth;
  const maxHeight = cropperHeight;

  if (newWidth > maxWidth) {
    newWidth = maxWidth;
    newHeight = newWidth / ratio;
  }

  if (newHeight > maxHeight) {
    newHeight = maxHeight;
    newWidth = newHeight * ratio;
  }

  // 새로운 위치 계산
  let newLeft = startDimension.value[3];
  let newTop = startDimension.value[0];
  let newRight = startDimension.value[1];
  let newBottom = startDimension.value[2];

  // 드래그 방향에 따라 위치 조정
  if (direction.value[3] === 1) {
    newLeft = cropperWidth - newWidth - startDimension.value[1];
  } else if (direction.value[1] === -1) {
    newRight = cropperWidth - newWidth - startDimension.value[3];
  } else if (!isCornerDrag) {
    // 중앙 조정
    const currentHorizontalCenter = startDimension.value[3] + currentWidth / 2;
    newLeft = Math.max(
      0,
      Math.min(cropperWidth - newWidth, currentHorizontalCenter - newWidth / 2),
    );
    newRight = cropperWidth - newWidth - newLeft;
  }

  if (direction.value[0] === 1) {
    newTop = cropperHeight - newHeight - startDimension.value[2];
  } else if (direction.value[2] === -1) {
    newBottom = cropperHeight - newHeight - startDimension.value[0];
  } else if (!isCornerDrag) {
    // 중앙 조정
    const currentVerticalCenter = startDimension.value[0] + currentHeight / 2;
    newTop = Math.max(
      0,
      Math.min(
        cropperHeight - newHeight,
        currentVerticalCenter - newHeight / 2,
      ),
    );
    newBottom = cropperHeight - newHeight - newTop;
  }

  // 경계 검사
  newLeft = Math.max(0, newLeft);
  newTop = Math.max(0, newTop);
  newRight = Math.max(0, newRight);
  newBottom = Math.max(0, newBottom);

  const newDimension: Dimension = [newTop, newRight, newBottom, newLeft];
  setDimension(newDimension);
};

/**
 * 마우스 업 이벤트 처리
 */
const handleMouseUp = () => {
  dragging.value = false;
  moving.value = false;
  direction.value = [0, 0, 0, 0];
};

/**
 * 이미지 로드 완료 처리
 */
const handleImageLoad = () => {
  createCropper();
};

/**
 * 이미지 자르기
 * @param {'image/jpeg' | 'image/png'} format - 출력 이미지 형식
 * @param {number} quality - 압축 품질 (0-1)
 * @param {'blob' | 'base64'} outputType - 출력 타입
 * @param {number} targetWidth - 대상 너비 (선택 사항, 없으면 원본 자르기 너비)
 * @param {number} targetHeight - 대상 높이 (선택 사항, 없으면 원본 자르기 높이)
 */
const getCropImage = async (
  format: 'image/jpeg' | 'image/png' = 'image/png',
  quality: number = 0.92,
  outputType: 'base64' | 'blob' = 'blob',
  targetWidth?: number,
  targetHeight?: number,
): Promise<Blob | string | undefined> => {
  if (!props.img || !bgImageRef.value || !containerRef.value) return;

  // 품질 매개변수 경계 수정: 0-1 구간으로 강제 제한하여 잘못된 값 전달 시 오류 방지
  const validQuality = Math.max(0, Math.min(1, quality));

  // 원본 크기를 가져오기 위해 임시 이미지 객체 생성
  const tempImg = new Image();
  // CORS 이미지 처리: 출처가 다른 네트워크 이미지에 대해서만 익명 설정
  if (props.img.startsWith('http://') || props.img.startsWith('https://')) {
    try {
      const url = new URL(props.img);
      if (url.origin !== location.origin) {
        tempImg.crossOrigin = 'anonymous';
      }
    } catch {
      // Invalid URL, 무시
    }
  }

  // 이미지 로드 대기
  await new Promise<void>((resolve, reject) => {
    const timeout = setTimeout(() => {
      tempImg.removeEventListener('load', handleLoad);
      tempImg.removeEventListener('error', handleError);
      reject(new Error('이미지 로드 시간 초과 (10초)'));
    }, 10_000);
    const handleLoad = () => {
      clearTimeout(timeout);
      tempImg.removeEventListener('load', handleLoad);
      tempImg.removeEventListener('error', handleError);
      resolve();
    };

    const handleError = (err: ErrorEvent) => {
      clearTimeout(timeout);
      tempImg.removeEventListener('load', handleLoad);
      tempImg.removeEventListener('error', handleError);
      reject(new Error(`이미지 로드 실패: ${err.message}`));
    };

    tempImg.addEventListener('load', handleLoad);
    tempImg.addEventListener('error', handleError);
    tempImg.src = props.img;
  });

  const containerRect = containerRef.value.getBoundingClientRect();
  const imgRect = bgImageRef.value.getBoundingClientRect();

  // 1. 컨테이너 내 이미지 렌더링 매개변수 계산
  const containerWidth = containerRect.width;
  const containerHeight = containerRect.height;
  const renderedImgWidth = imgRect.width;
  const renderedImgHeight = imgRect.height;
  const imgOffsetX = (containerWidth - renderedImgWidth) / 2;
  const imgOffsetY = (containerHeight - renderedImgHeight) / 2;

  // 2. 컨테이너 내 자르기 상자의 실제 좌표 계산
  const [cropTop, cropRight, cropBottom, cropLeft] = currentDimension.value;
  const cropBoxWidth = containerWidth - cropLeft - cropRight;
  const cropBoxHeight = containerHeight - cropTop - cropBottom;

  // 3. 자르기 상자 좌표를 이미지 좌표로 변환 (이미지 오프셋 고려)
  const cropOnImgX = cropLeft - imgOffsetX;
  const cropOnImgY = cropTop - imgOffsetY;

  // 4. 렌더링된 이미지에서 원본 이미지로의 스케일 비율 계산 (원본 픽셀 유지)
  const scaleX = tempImg.width / renderedImgWidth;
  const scaleY = tempImg.height / renderedImgHeight;

  // 5. 원본 이미지의 자르기 영역 매핑 (원본 픽셀 정밀도, 경계 이탈 방지)
  const originalCropX = Math.max(0, Math.floor(cropOnImgX * scaleX));
  const originalCropY = Math.max(0, Math.floor(cropOnImgY * scaleY));
  const originalCropWidth = Math.min(
    Math.floor(cropBoxWidth * scaleX),
    tempImg.width - originalCropX,
  );
  const originalCropHeight = Math.min(
    Math.floor(cropBoxHeight * scaleY),
    tempImg.height - originalCropY,
  );

  // 경계 검증: 자르기 크기가 유효하지 않으면 반환
  if (originalCropWidth <= 0 || originalCropHeight <= 0) return;

  // 6. 고해상도 화면 최적화 (Retina 화면 흐림 해결)
  const dpr = window.devicePixelRatio || 1;

  // 최종 캔버스 크기 (대상 크기 우선, 없으면 원본 자르기 크기)
  const finalWidth = targetWidth ? Math.max(1, targetWidth) : originalCropWidth;
  const finalHeight = targetHeight
    ? Math.max(1, targetHeight)
    : originalCropHeight;

  // 캔버스 생성 및 드로잉 컨텍스트 가져오기
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  // 캔버스 물리적 크기 (장치 픽셀 비율을 곱하여 고해상도 유지)
  canvas.width = finalWidth * dpr;
  canvas.height = finalHeight * dpr;

  // 캔버스 표시 크기 (시각적 크기, 최종 전시와 일치)
  canvas.style.width = `${finalWidth}px`;
  canvas.style.height = `${finalHeight}px`;

  // 고해상도 DPR에 맞춰 캔버스 컨텍스트 스케일링
  ctx.scale(dpr, dpr);

  // 7. 자른 이미지 그리기 (원본 픽셀을 사용하여 선명도 보장)
  ctx.drawImage(
    tempImg,
    originalCropX, // 원본 이미지 자르기 시작 X
    originalCropY, // 원본 이미지 자르기 시작 Y
    originalCropWidth, // 원본 이미지 자르기 너비
    originalCropHeight, // 원본 이미지 자르기 높이
    0, // 캔버스 드로잉 시작 X
    0, // 캔버스 드로잉 시작 Y
    finalWidth, // 캔버스 드로잉 너비
    finalHeight, // 캔버스 드로잉 높이
  );

  try {
    return outputType === 'base64'
      ? canvas.toDataURL(format, validQuality)
      : new Promise<Blob>((resolve) => {
          canvas.toBlob(
            (blob) => {
              // 백업: 만약 blob 생성이 실패하면 빈 Blob 반환
              resolve(blob || new Blob([], { type: format }));
            },
            format,
            validQuality,
          );
        });
  } catch (error) {
    console.error('이미지 내보내기 실패:', error);
  }
};

// 비율 변경 감시, 자르기 상자 재조정
watch(() => props.aspectRatio, adjustCropperToAspectRatio);

// 너비/높이 변경 감시, 크기 재계산
watch([() => props.width, () => props.height], () => {
  calculateImageFitSize();
  adjustCropperToAspectRatio();
});

// 컴포넌트 마운트 시 글로벌 이벤트 등록
onMounted(() => {
  document.addEventListener('mousemove', handleMouseMove);
  document.addEventListener('mouseup', handleMouseUp);

  // 이미지가 이미 로드된 경우 수동으로 크로퍼 생성 트리거
  if (
    bgImageRef.value &&
    bgImageRef.value.complete &&
    bgImageRef.value.naturalWidth > 0
  ) {
    createCropper();
  }
});

// 컴포넌트 언마운트 시 정리
onUnmounted(() => {
  document.removeEventListener('mousemove', handleMouseMove);
  document.removeEventListener('mouseup', handleMouseUp);
});

defineExpose({ getCropImage });
</script>

<template>
  <div
    :style="{
      width: `${width || CROPPER_CONSTANTS.DEFAULT_WIDTH}px`,
      height: `${height || CROPPER_CONSTANTS.DEFAULT_HEIGHT}px`,
    }"
    class="cropper-action-wrapper"
  >
    <div
      ref="containerRef"
      class="cropper-container"
      :style="{
        width: `${containerWidth}px`,
        height: `${containerHeight}px`,
      }"
    >
      <!-- 원본 이미지 전시 - 자동 크기 조정 -->
      <img
        ref="bgImageRef"
        class="cropper-image"
        :src="img"
        @load="handleImageLoad"
        :style="{
          maxWidth: '100%',
          maxHeight: '100%',
          objectFit: 'contain',
        }"
        alt="이미지 자르기 원본"
      />

      <!-- 遮罩层 -->
      <div
        class="cropper-mask"
        :style="{
          display: isCropperVisible ? 'block' : 'none',
          width: '100%',
          height: '100%',
        }"
      >
        <div
          ref="maskViewRef"
          class="cropper-mask-view"
          :style="{
            backgroundImage: `url(${img})`,
            backgroundSize: 'contain',
            backgroundPosition: 'center',
            backgroundRepeat: 'no-repeat',
            clipPath: `inset(${currentDimension[0]}px ${currentDimension[1]}px ${currentDimension[2]}px ${currentDimension[3]}px)`,
            width: '100%',
            height: '100%',
          }"
        ></div>
      </div>

      <!-- 자르기 상자 -->
      <div
        ref="cropperRef"
        class="cropper-box"
        :style="{
          display: isCropperVisible ? 'block' : 'none',
          width: '100%',
          height: '100%',
        }"
      >
        <div
          class="cropper-view"
          :style="{
            inset: `${currentDimension[0]}px ${currentDimension[1]}px ${currentDimension[2]}px ${currentDimension[3]}px`,
          }"
        >
          <!-- 자르기 상자 가이드라인 -->
          <span class="cropper-dashed-h"></span>
          <span class="cropper-dashed-v"></span>

          <!-- 자르기 상자 드래그 영역 -->
          <span
            class="cropper-move-area"
            @mousedown="handleMouseDown($event, 'move')"
          ></span>

          <!-- 테두리 선 -->
          <span class="cropper-line-e"></span>
          <span class="cropper-line-n"></span>
          <span class="cropper-line-w"></span>
          <span class="cropper-line-s"></span>

          <!-- 모서리 드래그 포인트 -->
          <span
            class="cropper-point cropper-point-ne"
            @mousedown="handleMouseDown($event, 'top-right')"
          >
            <span class="cropper-point-inner"></span>
          </span>
          <span
            class="cropper-point cropper-point-nw"
            @mousedown="handleMouseDown($event, 'top-left')"
          >
            <span class="cropper-point-inner"></span>
          </span>
          <span
            class="cropper-point cropper-point-sw"
            @mousedown="handleMouseDown($event, 'bottom-left')"
          >
            <span class="cropper-point-inner"></span>
          </span>
          <span
            class="cropper-point cropper-point-se"
            @mousedown="handleMouseDown($event, 'bottom-right')"
          >
            <span class="cropper-point-inner"></span>
          </span>

          <!-- 변 중앙 드래그 포인트 -->
          <span
            class="cropper-point cropper-point-e"
            @mousedown="handleMouseDown($event, 'right')"
          >
            <span class="cropper-point-inner"></span>
          </span>
          <span
            class="cropper-point cropper-point-n"
            @mousedown="handleMouseDown($event, 'top')"
          >
            <span class="cropper-point-inner"></span>
          </span>
          <span
            class="cropper-point cropper-point-w"
            @mousedown="handleMouseDown($event, 'left')"
          >
            <span class="cropper-point-inner"></span>
          </span>
          <span
            class="cropper-point cropper-point-s"
            @mousedown="handleMouseDown($event, 'bottom')"
          >
            <span class="cropper-point-inner"></span>
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@reference "@vben/tailwind-config/theme";

.cropper-action-wrapper {
  @apply box-border flex items-center justify-center;

  background-color: transparent;

  /* 모자이크 배경 */
  background-image:
    linear-gradient(45deg, #ccc 25%, transparent 25%),
    linear-gradient(-45deg, #ccc 25%, transparent 25%),
    linear-gradient(45deg, transparent 75%, #ccc 75%),
    linear-gradient(-45deg, transparent 75%, #ccc 75%);
  background-position:
    0 0,
    0 10px,
    10px -10px,
    -10px 0;
  background-size: 20px 20px;
}

.cropper-container {
  @apply relative;
}

.cropper-image {
  @apply block;
}

/* 마스크 레이어 */
.cropper-mask {
  @apply absolute top-0 left-0 bg-black/50;
}

.cropper-mask-view {
  @apply absolute top-0 left-0;
}

/* 자르기 상자 */
.cropper-box {
  @apply absolute top-0 left-0 z-10;
}

.cropper-view {
  @apply absolute top-0 right-0 bottom-0 left-0 outline-1 outline-blue-500 select-none;
}

/* 자르기 상자 가이드라인 */
.cropper-dashed-h {
  @apply absolute top-1/3 left-0 block h-1/3 w-full border-t border-b border-dashed border-gray-200/50;
}

.cropper-dashed-v {
  @apply absolute top-0 left-1/3 block h-full w-1/3 border-r border-l border-dashed border-gray-200/50;
}

/* 자르기 상자 드래그 영역 */
.cropper-move-area {
  @apply absolute top-0 left-0 block h-full w-full cursor-move ;
}

/* 테두리 드래그 선 */
.cropper-line-e,
.cropper-line-n,
.cropper-line-w,
.cropper-line-s {
  @apply absolute block bg-blue-500/10;
}

.cropper-line-e {
  @apply top-0 -right-0.75 h-full w-1;
}

.cropper-line-n {
  @apply -top-0.75 left-0 h-1 w-full;
}

.cropper-line-w {
  @apply top-0 -left-0.75 h-full w-1;
}

.cropper-line-s {
  @apply -bottom-0.75 left-0 h-1 w-full;
}

/* 드래그 포인트 */
.cropper-point {
  @apply absolute flex h-2 w-2 items-center justify-center bg-blue-500;
}

.cropper-point-inner {
  @apply block h-1.5 w-1.5 ;
}

/* 모서리 드래그 포인트 위치 및 커서 */
.cropper-point-ne {
  @apply -top-1.25 -right-1.25 cursor-ne-resize;
}

.cropper-point-nw {
  @apply -top-1.25 -left-1.25 cursor-nw-resize;
}

.cropper-point-sw {
  @apply -bottom-1.25 -left-1.25 cursor-sw-resize;
}

.cropper-point-se {
  @apply -right-1.25 -bottom-1.25 cursor-se-resize;
}

/* 변 중앙 드래그 포인트 위치 및 커서 */
.cropper-point-e {
  @apply top-1/2 -right-1.25 -mt-1 cursor-e-resize;
}

.cropper-point-n {
  @apply -top-1.25 left-1/2 -ml-1 cursor-n-resize;
}

.cropper-point-w {
  @apply top-1/2 -left-1.25 -mt-1 cursor-w-resize;
}

.cropper-point-s {
  @apply -bottom-1.25 left-1/2 -ml-1 cursor-s-resize;
}
</style>
