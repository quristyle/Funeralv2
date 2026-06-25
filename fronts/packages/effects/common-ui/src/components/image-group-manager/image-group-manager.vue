<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import SecureLS from 'secure-ls';

interface ImageFile {
  id: string;
  originalName: string;
  size: number;
  contentType: string;
  isImage: boolean;
  isRepresentative: boolean;
  sortOrder: number;
  downloadUrl: string;
  progress?: number;
  error?: boolean;
}

interface Props {
  /** 파일 그룹 ID (양방향 바인딩) */
  modelValue?: string | null;
  /** 최대 업로드 개수 */
  limit?: number;
  /** 파일 크기 제한 (MB) */
  maxSize?: number;
  /** 비즈니스 구분명 (기본값: GENERAL) */
  bizType?: string;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  limit: 5,
  maxSize: 10,
  bizType: 'GENERAL',
});

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
  (e: 'change-representative', url: string): void;
  (e: 'error', errorMsg: string): void;
}>();

const fileList = ref<ImageFile[]>([]);
const isUploading = ref(false);
const fileInput = ref<HTMLInputElement | null>(null);
const isDragging = ref(false);

// 로컬 스토리지에서 인증 토큰 조회 헬퍼
const getAuthToken = (): string => {
  let token = '';
  try {
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key && (key === 'core-access' || key.endsWith('-core-access'))) {
        const rawValue = localStorage.getItem(key);
        if (rawValue) {
          if (import.meta.env.DEV) {
            try {
              const parsed = JSON.parse(rawValue);
              token = parsed.accessToken || '';
            } catch {}
          } else {
            try {
              const ls = new SecureLS({
                encodingType: 'aes',
                encryptionSecret: import.meta.env.VITE_APP_STORE_SECURE_KEY,
                isCompression: true,
              });
              const decrypted = ls.get(key);
              token = decrypted?.accessToken || '';
            } catch (secErr) {
              console.error('SecureLS 복호화 에러:', secErr);
            }
          }
          if (token) break;
        }
      }
    }
  } catch (e) {
    console.error('인증 토큰 로드 중 오류 발생:', e);
  }
  return token;
};

// 파일 그룹 데이터 조회
const fetchGroupFiles = async (groupId: string) => {
  if (!groupId) return;
  
  const token = getAuthToken();
  try {
    const response = await fetch(`/api/file/group/${groupId}`, {
      method: 'GET',
      headers: {
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        'Accept': 'application/json',
      }
    });

    if (response.ok) {
      const res = await response.json();
      if (res.code === 'S000' || res.success) {
        // ApiResponse 구조에 대응하여 data.result 또는 data 파싱
        const rawData = res.result || res.data;
        const list: ImageFile[] = Array.isArray(rawData) ? rawData : (rawData?.result || []);
        fileList.value = list.map(item => ({
          ...item,
          isRepresentative: !!item.isRepresentative
        }));
        
        // 현재 대표 이미지 주소를 찾아서 이벤트 발생
        const rep = fileList.value.find(f => f.isRepresentative);
        if (rep) {
          emit('change-representative', rep.downloadUrl);
        }
      }
    }
  } catch (err: any) {
    console.error('그룹 파일 목록 조회 실패:', err);
    emit('error', '파일 목록을 가져오는 데 실패했습니다.');
  }
};

// 다중 파일 업로드 실행
const uploadFiles = async (files: File[]) => {
  if (files.length === 0) return;

  const totalCount = fileList.value.length + files.length;
  if (totalCount > props.limit) {
    alert(`최대 ${props.limit}개 이미지까지만 업로드할 수 있습니다.`);
    return;
  }

  isUploading.value = true;
  const token = getAuthToken();
  const formData = new FormData();
  
  files.forEach(file => {
    formData.append('files', file);
  });
  
  if (props.modelValue) {
    formData.append('groupId', props.modelValue);
  }
  formData.append('bizType', props.bizType);

  try {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', '/api/file/group/upload', true);
    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    xhr.onload = () => {
      isUploading.value = false;
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const res = JSON.parse(xhr.responseText);
          if (res.code === 'S000' || res.success || res.result) {
            let resData = res.result || res.data;
            
            // 만약 resData.result가 배열인 경우 단일 객체 데이터 추출 (백엔드 ApiResponse 래핑 대응)
            if (resData && Array.isArray(resData.result) && resData.result.length > 0) {
              resData = resData.result[0];
            }
            
            const newGroupId = resData?.groupId;
            console.log('[ImageGroupManager Debug] upload success. newGroupId:', newGroupId, 'props.modelValue:', props.modelValue);
            
            // 신규 발급된 그룹 ID 적용
            if (newGroupId && newGroupId !== props.modelValue) {
              console.log('[ImageGroupManager Debug] Emitting update:modelValue with newGroupId:', newGroupId);
              emit('update:modelValue', newGroupId);
            } else {
              console.log('[ImageGroupManager Debug] GroupId did not change or is null. Skip emit.');
            }

            // 파일 목록 리로드
            fetchGroupFiles(newGroupId || props.modelValue);
          } else {
            throw new Error(res.message || '업로드 실패');
          }
        } catch (e: any) {
          emit('error', e.message);
        }
      } else {
        emit('error', `서버 응답 오류 (상태코드: ${xhr.status})`);
      }
    };

    xhr.onerror = () => {
      isUploading.value = false;
      emit('error', '네트워크 전송 오류가 발생했습니다.');
    };

    xhr.send(formData);
  } catch (err: any) {
    isUploading.value = false;
    emit('error', err.message);
  }
};

// 대표 지정 API 호출
const setRepresentative = async (fileId: string) => {
  if (!props.modelValue) return;

  const token = getAuthToken();
  try {
    const response = await fetch(`/api/file/group/${props.modelValue}/representative/${fileId}`, {
      method: 'PUT',
      headers: {
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        'Accept': 'application/json',
      }
    });

    if (response.ok) {
      const res = await response.json();
      if (res.success || res.code === 'S000') {
        // 내부 캐시 목록 업데이트
        fileList.value.forEach(f => {
          f.isRepresentative = (f.id === fileId);
        });
        
        // 대표 이미지 변경 이벤트 통보
        const rep = fileList.value.find(f => f.isRepresentative);
        if (rep) {
          emit('change-representative', rep.downloadUrl);
        }
      }
    } else {
      emit('error', '대표 이미지 설정에 실패했습니다.');
    }
  } catch (err: any) {
    emit('error', err.message);
  }
};

// 파일 개별 삭제
const deleteFile = async (fileId: string) => {
  const token = getAuthToken();
  try {
    const response = await fetch(`/api/file/${fileId}`, {
      method: 'DELETE',
      headers: {
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        'Accept': 'application/json',
      }
    });

    if (response.ok) {
      // 삭제 성공 시 로컬 목록 갱신
      const deletedWasRep = fileList.value.find(f => f.id === fileId)?.isRepresentative;
      fileList.value = fileList.value.filter(f => f.id !== fileId);
      
      // 대표 사진이 삭제된 경우 다음 사진을 대표로 지정
      if (deletedWasRep && fileList.value.length > 0 && fileList.value[0]) {
        await setRepresentative(fileList.value[0].id);
      } else if (fileList.value.length === 0) {
        emit('change-representative', '');
      }
    } else {
      emit('error', '파일 삭제에 실패했습니다.');
    }
  } catch (err: any) {
    emit('error', err.message);
  }
};

// 드래그 앤 드롭 및 인풋 핸들링
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
    processUploadedFiles(files);
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
    processUploadedFiles(files);
  }
};

// 이미지 미리보기 모달 상태
const previewVisible = ref(false);
const previewUrl = ref('');
const previewTitle = ref('');

const handlePreview = (url: string, title: string) => {
  previewUrl.value = url;
  previewTitle.value = title;
  previewVisible.value = true;
};


const processUploadedFiles = (fileList: FileList) => {
  const filesToUpload: File[] = [];
  for (let i = 0; i < fileList.length; i++) {
    const file = fileList[i];
    if (file) {
      if (!file.type.startsWith('image/')) {
        alert('이미지 파일만 업로드할 수 있습니다.');
        continue;
      }
      if (file.size > props.maxSize * 1024 * 1024) {
        alert(`파일 크기가 ${props.maxSize}MB를 초과할 수 없습니다.`);
        continue;
      }
      filesToUpload.push(file);
    }
  }
  uploadFiles(filesToUpload);
};

// watch props.modelValue
watch(() => props.modelValue, (newGroupId) => {
  if (newGroupId) {
    fetchGroupFiles(newGroupId);
  } else {
    fileList.value = [];
  }
}, { immediate: true });

onMounted(() => {
  if (props.modelValue) {
    fetchGroupFiles(props.modelValue);
  }
});
</script>

<template>
  <div class="image-group-manager w-full flex flex-col gap-4">
    <!-- 그리드 카드 갤러리 영역 -->
    <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
      
      <!-- 기존 파일 카드 리스트 -->
      <div 
        v-for="file in fileList" 
        :key="file.id"
        class="group relative aspect-square border rounded-xl overflow-hidden bg-card hover:shadow-lg transition-all duration-300 transform hover:-translate-y-1"
        :class="{ 'border-primary ring-2 ring-primary/20': file.isRepresentative, 'border-muted': !file.isRepresentative }"
      >
        <!-- 실제 이미지 표시 -->
        <img :src="file.downloadUrl" class="size-full object-cover" />
        
        <!-- 대표 이미지 배지 -->
        <div 
          v-if="file.isRepresentative"
          class="absolute top-2 left-2 px-2 py-0.5 text-[10px] font-bold text-white bg-primary rounded-full shadow-md z-10"
        >
          대표 사진
        </div>

        <!-- 카드 마우스오버 제어 오버레이 -->
        <div class="absolute inset-0 bg-black/60 opacity-0 group-hover:opacity-100 transition-opacity duration-200 flex flex-col items-center justify-center gap-2">
          <div class="flex items-center gap-1.5">
            <!-- 미리보기 버튼 -->
            <button 
              @click="handlePreview(file.downloadUrl, file.originalName)"
              class="px-2.5 py-1 text-xs font-semibold text-white bg-slate-600 hover:bg-slate-700 rounded-md transition-all scale-90 group-hover:scale-100 shadow"
            >
              미리보기
            </button>
            <!-- 대표 지정 버튼 -->
            <button 
              v-if="!file.isRepresentative"
              @click="setRepresentative(file.id)"
              class="px-2.5 py-1 text-xs font-semibold text-white bg-primary hover:bg-primary-hover rounded-md transition-all scale-90 group-hover:scale-100 shadow"
            >
              대표 지정
            </button>
          </div>
          
          <!-- 이미지명 가공 노출 -->
          <span class="text-[10px] text-white/80 max-w-[90%] truncate px-1">{{ file.originalName }}</span>
        </div>

        <!-- 카드 우측 상단 삭제 버튼 -->
        <button 
          @click="deleteFile(file.id)"
          class="absolute top-2 right-2 size-6 rounded-full  hover:bg-red-500 hover:text-white border flex items-center justify-center text-muted-foreground shadow-sm transition-all duration-200 z-10"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="size-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- 업로드 슬롯 카드 (개수 제한 미달 시 노출) -->
      <div 
        v-if="fileList.length < limit"
        @click="triggerFileInput"
        @dragover="handleDragOver"
        @dragleave="handleDragLeave"
        @drop="handleDrop"
        class="aspect-square border-2 border-dashed rounded-xl cursor-pointer flex flex-col items-center justify-center gap-2 transition-all duration-300 hover:border-primary hover:bg-primary/5"
        :class="{ 'border-primary bg-primary/10': isDragging, 'border-muted-foreground/30 bg-muted/20': !isDragging }"
      >
        <input 
          ref="fileInput" 
          type="file" 
          class="hidden" 
          multiple
          accept="image/*" 
          @change="handleFileChange" 
        />
        
        <div v-if="isUploading" class="flex flex-col items-center gap-1.5 text-muted-foreground">
          <svg class="animate-spin h-6 w-6 text-primary" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <span class="text-[10px] font-medium">업로드 중...</span>
        </div>
        
        <div v-else class="flex flex-col items-center gap-1 text-center">
          <svg xmlns="http://www.w3.org/2000/svg" class="size-6 text-muted-foreground group-hover:text-primary transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          <span class="text-xs font-semibold text-muted-foreground">사진 추가</span>
          <span class="text-[9px] text-muted-foreground/80">최대 {{ limit - fileList.length }}장 가능</span>
        </div>
      </div>

    </div>

    <!-- 이미지 미리보기 모달 -->
    <div 
      v-if="previewVisible" 
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4 transition-all duration-300 animate-in fade-in"
      @click="previewVisible = false"
    >
      <div 
        class="relative max-w-4xl max-h-[90vh] bg-card rounded-xl overflow-hidden shadow-2xl flex flex-col border border-border"
        @click.stop
      >
        <!-- 모달 헤더 -->
        <div class="flex items-center justify-between px-4 py-3 border-b border-border">
          <span class="text-sm font-semibold text-foreground truncate max-w-[80%]">{{ previewTitle }}</span>
          <button 
            @click="previewVisible = false"
            class="text-muted-foreground hover:text-foreground transition-colors p-1"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="size-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        
        <!-- 모달 콘텐츠 (이미지) -->
        <div class="overflow-auto flex-auto flex items-center justify-center p-4 bg-muted/10">
          <img 
            :src="previewUrl" 
            alt="Preview Image" 
            class="max-w-full max-h-[70vh] object-contain rounded-md"
          />
        </div>
      </div>
    </div>

  </div>
</template>

<style scoped>
.bg-primary-hover {
  background-color: var(--primary-hover, rgba(59, 130, 246, 0.95));
}
</style>
