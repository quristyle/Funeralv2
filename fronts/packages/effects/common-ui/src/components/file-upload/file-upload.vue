<script setup lang="ts">
import { ref, computed } from 'vue';

interface Props {
  mode?: 'avatar' | 'image' | 'file';
  multiple?: boolean;
  limit?: number;
  accept?: string;
  maxSize?: number; // MB
  value?: string | string[];
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'file',
  multiple: false,
  limit: 1,
  accept: '*',
  maxSize: 10,
});

const emit = defineEmits<{
  (e: 'change', value: string | string[]): void;
  (e: 'upload-success', data: any): void;
  (e: 'upload-error', err: any): void;
}>();

const isDragging = ref(false);
const isUploading = ref(false);
const uploadProgress = ref(0);
const fileInput = ref<HTMLInputElement | null>(null);

// 로컬 업로드된 임시 프리뷰 목록
const previewFiles = ref<Array<{ name: string; url: string; size: string; progress?: number; error?: boolean }>>([]);

// 기존 값 바인딩용
const currentValues = computed(() => {
  if (!props.value) return [];
  return Array.isArray(props.value) ? props.value : [props.value];
});

// 파일 크기 텍스트 가공
const formatBytes = (bytes: number) => {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
};

const handleDragOver = (e: DragEvent) => {
  e.preventDefault();
  isDragging.value = true;
};

const handleDragLeave = () => {
  isDragging.value = false;
};

const handleDrop = (e: DragEvent) => {
  e.preventDefault();
  isDragging.value = false;
  const files = e.dataTransfer?.files;
  if (files && files.length > 0) {
    processFiles(files);
  }
};

const triggerFileInput = () => {
  if (fileInput.value) {
    fileInput.value.click();
  }
};

const handleFileChange = (e: Event) => {
  const target = e.target as HTMLInputElement;
  const files = target.files;
  if (files && files.length > 0) {
    processFiles(files);
  }
};

// 파일 검증 및 전송 프로세스
const processFiles = async (fileList: FileList) => {
  let filesToUpload = Array.from(fileList);
  
  // 단일 업로드 모드인 경우
  if (!props.multiple) {
    // 여러 개를 드롭/선택했더라도 첫 번째 파일만 취급하여 대체함
    if (filesToUpload.length > 0) {
      filesToUpload = [filesToUpload[0]!];
    }
  } else {
    // 다중 업로드 모드인 경우에만 누적 개수 제한 체크
    const totalCount = currentValues.value.length + filesToUpload.length;
    if (props.limit && totalCount > props.limit) {
      alert(`최대 ${props.limit}개 파일까지만 업로드할 수 있습니다.`);
      return;
    }
  }

  for (const file of filesToUpload) {
    // 파일 형식 및 크기 체크
    if (props.maxSize && file.size > props.maxSize * 1024 * 1024) {
      alert(`파일 크기가 ${props.maxSize}MB를 초과할 수 없습니다.`);
      continue;
    }

    if (props.mode === 'image' || props.mode === 'avatar') {
      if (!file.type.startsWith('image/')) {
        alert('이미지 파일만 업로드할 수 있습니다.');
        continue;
      }
    }

    // 업로드 실행
    await uploadFile(file);
  }
};

// 실제 API 호출로 파일 전송
const uploadFile = async (file: File) => {
  isUploading.value = true;
  uploadProgress.value = 0;

  // 로컬 스토리지에서 액세스 토큰 조회 (피니아 영속화 스토어의 키를 직접 추출)
  const storeData = localStorage.getItem('core-access');
  let token = '';
  if (storeData) {
    try {
      token = JSON.parse(storeData).accessToken || '';
    } catch (e) {
      console.error('로컬 스토리지 토큰 파싱 에러:', e);
    }
  }

  // 1. 임시 프리뷰 카드 추가
  const previewItem = {
    name: file.name,
    url: URL.createObjectURL(file),
    size: formatBytes(file.size),
    progress: 0,
    error: false
  };
  previewFiles.value.push(previewItem);

  const formData = new FormData();
  formData.append('file', file);

  try {
    const xhr = new XMLHttpRequest();
    // API Gateway를 거치는 파일 업로드 라우트 호출
    xhr.open('POST', '/api/file/upload', true);

    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    // 진척률 마이크로 애니메이션 반영
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        const percent = Math.round((event.loaded / event.total) * 100);
        uploadProgress.value = percent;
        previewItem.progress = percent;
      }
    };

    xhr.onload = () => {
      isUploading.value = false;
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const res = JSON.parse(xhr.responseText);
          // ApiResponse<T> 형식에서 결과 추출
          if (res.code === 0 || res.success || res.result) {
            const data = res.result || res.data;
            emit('upload-success', data);
            
            // 이미지 또는 파일의 다운로드 주소
            const fileUrl = data.downloadUrl || `/api/file/download/${data.id}`;
            
            let newValue: string | string[];
            if (props.multiple) {
              newValue = [...currentValues.value, fileUrl];
            } else {
              newValue = fileUrl;
            }
            emit('change', newValue);
          } else {
            throw new Error(res.message || '업로드 실패');
          }
        } catch (e: any) {
          previewItem.error = true;
          emit('upload-error', e.message);
        }
      } else {
        previewItem.error = true;
        emit('upload-error', `서버 응답 오류 (상태코드: ${xhr.status})`);
      }
    };

    xhr.onerror = () => {
      isUploading.value = false;
      previewItem.error = true;
      emit('upload-error', '네트워크 전송 오류 발생');
    };

    xhr.send(formData);
  } catch (err: any) {
    isUploading.value = false;
    previewItem.error = true;
    emit('upload-error', err.message);
  }
};

const removeFile = (index: number) => {
  if (props.multiple) {
    const values = [...currentValues.value];
    values.splice(index, 1);
    emit('change', values);
  } else {
    emit('change', '');
  }
};
</script>

<template>
  <div class="file-upload-container w-full">
    <!-- 아바타 업로드 모드 전용 UI -->
    <div v-if="mode === 'avatar'" class="flex flex-col items-center justify-center gap-2">
      <div 
        class="avatar-uploader relative size-24 cursor-pointer overflow-hidden rounded-full border-2 border-dashed border-muted-foreground/30 transition-all hover:border-primary hover:scale-105"
        @click="triggerFileInput"
        @dragover="handleDragOver"
        @dragleave="handleDragLeave"
        @drop="handleDrop"
        :class="{ 'border-primary bg-primary/10': isDragging }"
      >
        <img 
          v-if="value" 
          :src="currentValues[0]" 
          alt="Avatar Preview" 
          class="size-full object-cover"
        />
        <div v-else class="flex size-full flex-col items-center justify-center bg-muted text-muted-foreground text-[10px]">
          <svg xmlns="http://www.w3.org/2000/svg" class="size-6 mb-1" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-0h6m-6 0H6" />
          </svg>
          사진 변경
        </div>
        <!-- 마우스 호버 시의 변경 레이아웃 -->
        <div class="absolute inset-0 flex items-center justify-center bg-black/40 opacity-0 transition-opacity duration-200 hover:opacity-100 text-white text-xs font-medium">
          {{ isUploading ? `${uploadProgress}%` : '변경' }}
        </div>
      </div>
      <input 
        ref="fileInput" 
        type="file" 
        class="hidden" 
        :accept="accept === '*' ? 'image/*' : accept" 
        @change="handleFileChange" 
      />
    </div>

    <!-- 일반 이미지/파일 업로드 박스 UI -->
    <div v-else class="flex flex-col gap-4">
      <div
        class="drag-drop-zone flex flex-col items-center justify-center p-6 border-2 border-dashed rounded-lg cursor-pointer transition-all hover:border-primary hover:bg-primary/5"
        :class="{ 'border-primary bg-primary/10': isDragging, 'border-muted-foreground/30 bg-card': !isDragging }"
        @click="triggerFileInput"
        @dragover="handleDragOver"
        @dragleave="handleDragLeave"
        @drop="handleDrop"
      >
        <input 
          ref="fileInput" 
          type="file" 
          class="hidden" 
          :multiple="multiple" 
          :accept="accept" 
          @change="handleFileChange" 
        />
        
        <!-- 업로드 대기 아이콘 및 안내글 -->
        <div class="flex flex-col items-center gap-2 text-center">
          <svg xmlns="http://www.w3.org/2000/svg" class="size-10 text-muted-foreground animate-bounce-slow" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
          </svg>
          <span class="text-sm font-semibold">파일을 여기로 드래그하거나 클릭하여 업로드</span>
          <span class="text-xs text-muted-foreground">단일 파일 최대 {{ maxSize }}MB (허용규격: {{ accept }})</span>
        </div>
      </div>

      <!-- 업로드 목록 / 프리뷰 그리드 -->
      <div v-if="currentValues.length > 0 || previewFiles.length > 0" class="flex flex-col gap-2">
        <div class="text-xs font-semibold text-muted-foreground">업로드 완료 및 진행 중 파일 목록</div>
        
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <!-- 1. 기존 업로드 완료된 파일 카드 -->
          <div 
            v-for="(val, idx) in currentValues" 
            :key="idx" 
            class="flex items-center gap-3 p-3 bg-muted/50 border rounded-lg hover:shadow-md transition-shadow relative group"
          >
            <!-- 이미지 전용 썸네일 노출 -->
            <div v-if="mode === 'image' || val.match(/\.(jpg|jpeg|png|gif|webp|svg)$/i)" class="size-12 rounded overflow-hidden flex-none bg-black/10">
              <img :src="val" class="size-full object-cover" />
            </div>
            <!-- 일반 문서 아이콘 노출 -->
            <div v-else class="size-12 rounded flex items-center justify-center flex-none bg-primary/10 text-primary">
              <svg xmlns="http://www.w3.org/2000/svg" class="size-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
            </div>
            
            <div class="flex-auto min-w-0 flex flex-col justify-center">
              <span class="text-xs font-medium truncate block">{{ val.split('/').pop() }}</span>
              <span class="text-[10px] text-green-600 font-semibold flex items-center gap-1 mt-0.5">
                <svg xmlns="http://www.w3.org/2000/svg" class="size-3" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                </svg>
                전송 완료
              </span>
            </div>
            
            <!-- 파일 개별 삭제 버튼 -->
            <button 
              @click="removeFile(idx)"
              class="absolute top-2 right-2 text-muted-foreground hover:text-red-500 bg-card rounded-full p-1 border shadow-sm opacity-0 group-hover:opacity-100 transition-opacity"
            >
              <svg xmlns="http://www.w3.org/2000/svg" class="size-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </div>

          <!-- 2. 실시간 업로드 중인 임시 파일 카드 -->
          <div 
            v-for="(file, idx) in previewFiles.filter(f => !f.error)" 
            :key="'prog-' + idx" 
            class="flex items-center gap-3 p-3 bg-card border rounded-lg relative overflow-hidden"
          >
            <div v-if="mode === 'image'" class="size-12 rounded overflow-hidden flex-none bg-black/10">
              <img :src="file.url" class="size-full object-cover opacity-60" />
            </div>
            <div v-else class="size-12 rounded flex items-center justify-center flex-none bg-muted text-muted-foreground animate-pulse">
              <svg xmlns="http://www.w3.org/2000/svg" class="size-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 1121.21 8H18.75" />
              </svg>
            </div>
            
            <div class="flex-auto min-w-0 flex flex-col justify-center relative z-10">
              <span class="text-xs font-medium truncate block text-muted-foreground">{{ file.name }}</span>
              <div class="w-full bg-muted h-1 rounded-full overflow-hidden mt-1.5">
                <div class="bg-primary h-full transition-all duration-150" :style="{ width: `${file.progress}%` }"></div>
              </div>
            </div>

            <!-- 전송률 텍스트 -->
            <span class="text-[10px] text-primary font-bold self-start mt-1 relative z-10">{{ file.progress }}%</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.animate-bounce-slow {
  animation: bounce 2s infinite;
}
@keyframes bounce {
  0%, 100% {
    transform: translateY(-5%);
    animation-timing-function: cubic-bezier(0.8,0,1,1);
  }
  50% {
    transform: none;
    animation-timing-function: cubic-bezier(0,0,0.2,1);
  }
}
</style>
