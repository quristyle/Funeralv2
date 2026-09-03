<script lang="ts" setup>
import { ref, onMounted, onUnmounted, nextTick } from 'vue';
import { useVbenDrawer } from '@vben/common-ui';
import { message, Form, Modal, Button } from 'ant-design-vue';
import { getDeceasedDetail, saveDeceasedDetail, getRooms, cancelDeceasedDeparture } from '#/api/funeral/building';
import dayjs from 'dayjs';
import DictSelect from '#/components/DictSelect.vue';

// 하위 카드 컴포넌트 임포트
import DeceasedBasicForm from './parts/deceased-basic-form.vue';
import DeceasedMournersForm from './parts/deceased-mourners-form.vue';
import DeceasedContractorForm from './parts/deceased-contractor-form.vue';
import DeceasedManagerForm from './parts/deceased-manager-form.vue';
import DeceasedFacilitiesForm from './parts/deceased-facilities-form.vue';
import DeceasedPhotoForm from './parts/deceased-photo-form.vue';
import DeceasedRoomsForm from './parts/deceased-rooms-form.vue';
import AutoDatePicker from '#/components/AutoDatePicker.vue';

const emit = defineEmits<{
  (e: 'saved'): void;
}>();

const rooms = ref<any[]>([]);
const isEditMode = ref<boolean>(false);
const currentId = ref<string>('');
const activeSection = ref<string>('basic');
const scrollContainerRef = ref<HTMLElement | null>(null);
const isScrollingByClick = ref<boolean>(false);
const basicFormRef = ref<any>(null);
const deathDatePickerRef = ref<any>(null);
let observer: IntersectionObserver | null = null;

onMounted(() => {
  // scrollContainerRef.value를 DOM이 렌더링된 후에 접근하기 위해 nextTick을 사용할 수 있지만,
  // Modal의 생명주기상 onMounted에서 바로 참조가 가능할 수도 있습니다.
  // 확실한 참조를 위해 Modal이 열리고난 후에 이 로직을 실행하는 것이 더 안정적일 수 있습니다.
  // 일단 onMounted에서 시도합니다.
  if (!scrollContainerRef.value) {
    // Modal 내부 요소일 경우, Modal의 표시 상태에 따라 ref가 설정될 수 있으므로
    // 한 프레임 뒤에 다시 시도하는 로직을 추가할 수 있습니다.
    setTimeout(setupObserver, 100);
  } else {
    setupObserver();
  }
});

onUnmounted(() => {
  if (observer) {
    observer.disconnect();
  }
});

function setupObserver() {
  if (observer) {
    observer.disconnect();
  }
  if (!scrollContainerRef.value) {
    console.warn('[deceased-form-drawer] scrollContainerRef is not defined when setting up observer');
    return;
  }

  observer = new IntersectionObserver(
    (entries) => {
      if (isScrollingByClick.value) return;

      // 뷰포트 안에 여러 섹션이 있을 경우, 가장 위에 있는 섹션을 활성화합니다.
      const intersectingEntries = entries.filter((e) => e.isIntersecting);

      if (intersectingEntries.length > 0) {
        // top 위치를 기준으로 정렬하여 가장 위에 있는 섹션을 찾습니다.
        const sortedEntries = intersectingEntries.sort(
          (a, b) => a.target.getBoundingClientRect().top - b.target.getBoundingClientRect().top,
        );
        const topEntry = sortedEntries[0];
        if (topEntry?.target?.id) {
          activeSection.value = topEntry.target.id.replace('section-', '');
        }
      }
    },
    {
      root: scrollContainerRef.value,
      // 화면 중앙에서 약간 위쪽 영역을 기준으로 감지하도록 rootMargin 설정
      // [top, right, bottom, left]
      // -30% => 상단 30% 영역에서는 감지하지 않음 (스크롤을 내려서 컨텐츠가 위로 올라갈 때)
      // -30% => 하단 30% 영역에서는 감지하지 않음
      rootMargin: '-30% 0px -30% 0px',
      threshold: 0, // 0.0 ~ 1.0. 0은 1px이라도 보이면, 1은 100% 다보이면
    },
  );

  navItems.forEach((item) => {
    const section = document.getElementById(`section-${item.id}`);
    if (section) {
      observer?.observe(section);
    }
  });
}

// 네비게이션 메뉴 정의
const navItems = [
  { id: 'basic', label: '기본 정보' },
  { id: 'mourners', label: '상주 정보' },
  { id: 'contractor', label: '계약자 정보' },
  { id: 'manager', label: '담당자 정보' },
  { id: 'facilities', label: '시설 이용' },
  { id: 'photos', label: '사진 관리' },
  { id: 'rooms', label: '호실 지정' }
];

// 종합 데이터 모델 정의
const formModel = ref<any>({
  id: '',
  name: '',
  gender: 'M',
  age: 80,
  religion: 'NONE',
  deathDate: '',
  funeralDate: '',
  burialDate: '',
  roomId: '',
  status: 'FUNERAL_IN_PROGRESS',
  remark: '',
  ssn: '',
  causeOfDeath: '',
  burialPlot: '',
  memorialPhotoUrl: '',
  memorialPhotoFileId: '',
  familyPhotoGroupId: '',

  mourners: [],
  contractor: {
    name: '',
    contact: '',
    relation: '',
    address: '',
    remark: '',
    signatureFileId: ''
  },
  manager: {
    directorName: '',
    directorContact: '',
    mutualAidCompany: '',
    staffName: '',
    staffContact: ''
  },
  facilities: [],
  rooms: []
});

// 날짜 바인딩 (메인 폼용)
const deathDateVal = ref<any>(null);
const funeralDateVal = ref<any>(null);
const burialDateVal = ref<any>(null);

const [DeceasedDrawer, deceasedDrawerApi] = useVbenDrawer({
  title: '고인 종합 관리 시스템',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

// 호실 목록 조회
async function fetchRooms() {
  try {
    const list = await getRooms({});
    rooms.value = list || [];
  } catch (error) {
    message.error('호실 목록 로드 실패');
  }
}

// 앵커 스크롤 이동
function scrollToSection(sectionId: string) {
  activeSection.value = sectionId;
  isScrollingByClick.value = true;
  const el = document.getElementById(`section-${sectionId}`);
  if (el) {
    el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }
  setTimeout(() => {
    isScrollingByClick.value = false;
  }, 800);
}

// 상세 정보 로드
async function open(row?: any) {
  fetchRooms();
  activeSection.value = 'basic';
  
  if (row && row.id) {
    isEditMode.value = true;
    currentId.value = row.id;
    deceasedDrawerApi.setState({ title: '고인 종합 관리 (수정)' });

    try {
      deceasedDrawerApi.lock();
      // 백엔드 통합 상세 조회 호출
      const detail = await getDeceasedDetail(row.id);
      const detailData = (detail as any)?.result?.[0] || detail;
      if (detailData) {
        formModel.value = {
          ...detailData,
          contractor: detailData.contractor || { name: '', contact: '', relation: '', address: '', remark: '', signatureFileId: '' },
          manager: detailData.manager || { directorName: '', directorContact: '', mutualAidCompany: '', staffName: '', staffContact: '' },
          mourners: detailData.mourners || [],
          facilities: detailData.facilities || [],
          rooms: detailData.rooms || []
        };
        deathDateVal.value = detailData.deathDate ? dayjs(detailData.deathDate) : null;
        funeralDateVal.value = detailData.funeralDate ? dayjs(detailData.funeralDate) : null;
        burialDateVal.value = detailData.burialDate ? dayjs(detailData.burialDate) : null;
      }
    } catch (err) {
      message.error('고인 상세 정보를 로드할 수 없습니다.');
    } finally {
      deceasedDrawerApi.unlock();
    }
  } else {
    isEditMode.value = false;
    currentId.value = '';
    deceasedDrawerApi.setState({ title: '고인 종합 관리 (신규 등록)' });

    // 신규 등록 시 기본값 설정
    formModel.value = {
      id: '',
      name: '',
      gender: 'M',
      age: 80,
      religion: 'NONE',
      deathDate: '',
      funeralDate: '',
      burialDate: '',
      roomId: '',
      status: 'FUNERAL_IN_PROGRESS',
      remark: '',
      ssn: '',
      causeOfDeath: '',
      burialPlot: '',
      memorialPhotoUrl: '',
      memorialPhotoFileId: '',
      familyPhotoGroupId: '',

      mourners: [],
      contractor: { name: '', contact: '', relation: '', address: '', remark: '', signatureFileId: '' },
      manager: { directorName: '', directorContact: '', mutualAidCompany: '', staffName: '', staffContact: '' },
      facilities: [],
      rooms: []
    };

    // 빈소현황의 공실 카드에서 '고인 등록'으로 들어오면 그 호실을 미리 채운다
    // (옛 화면의 '자동생성' 즉시 INSERT 를 폼 프리필로 대체 — 47번 문서 2단계).
    if (row?.roomId) {
      formModel.value.roomId = row.roomId;
      formModel.value.rooms = [{
        id: '',
        roomId: row.roomId,
        companyId: '',
        buildingId: '',
        floorId: '',
        startTime: dayjs().format('YYYY-MM-DDTHH:mm:ss'),
        endTime: ''
      }];
    }

    deathDateVal.value = null;
    funeralDateVal.value = null;
    burialDateVal.value = null;
  }
  
  deceasedDrawerApi.open();

  nextTick(() => {
    setupObserver();
  });
}

async function handleSave() {
  try {
    if (!formModel.value.name) {
      message.warning('고인 성명은 필수 입력 사항입니다.');
      scrollToSection('basic');
      nextTick(() => {
        basicFormRef.value?.focusName();
      });
      return;
    }
    if (!deathDateVal.value) {
      message.warning('작고 일시는 필수 입력 사항입니다.');
      scrollToSection('basic');
      nextTick(() => {
        deathDatePickerRef.value?.focus();
      });
      return;
    }

    formModel.value.deathDate = deathDateVal.value ? deathDateVal.value.format('YYYY-MM-DDTHH:mm:ss') : null;
    formModel.value.funeralDate = funeralDateVal.value ? funeralDateVal.value.format('YYYY-MM-DDTHH:mm:ss') : null;
    formModel.value.burialDate = burialDateVal.value ? burialDateVal.value.format('YYYY-MM-DDTHH:mm:ss') : null;

    deceasedDrawerApi.lock();
    
    // 통합 상세 일괄 저장 (Merge)
    await saveDeceasedDetail(currentId.value, formModel.value);
    
    message.success('고인 종합 정보가 일괄 저장되었습니다.');
    deceasedDrawerApi.close();
    emit('saved');
  } catch (error) {
    message.error('저장 중 실패가 발생했습니다.');
  } finally {
    deceasedDrawerApi.unlock();
  }
}

async function handleCancelDeparture() {
  if (!currentId.value) return;

  Modal.confirm({
    title: '출상 취소 확인',
    content: '고인의 출상(장례 완료) 처리를 취소하고 다시 장례를 진행하시겠습니까? 이전 호실 배정 이력도 복구됩니다.',
    okText: '진행',
    cancelText: '취소',
    onOk: async () => {
      try {
        deceasedDrawerApi.lock();
        await cancelDeceasedDeparture(currentId.value);
        message.success('출상 취소 처리가 완료되었습니다.');
        deceasedDrawerApi.close();
        emit('saved');
      } catch (err) {
        console.error('출상 취소 실패:', err);
        message.error('출상 취소 중 오류가 발생했습니다.');
      } finally {
        deceasedDrawerApi.unlock();
      }
    }
  });
}

defineExpose({ open });
</script>

<template>
  <!--
    이 화면만 넓다(1050px). 왼쪽 바로가기 + 오른쪽 스크롤의 2단 구성이라
    기본 폭(520px)으로는 두 단이 겹친다.

    `content-class="p-0"` — 드로어 본문의 기본 여백을 없앤다. 왼쪽 바로가기 띠가
    가장자리에 붙어야 경계선이 제 노릇을 한다.
    높이는 `h-full` 이다 — 드로어는 창 높이를 꽉 채우므로 예전 모달처럼
    680px 로 못 박으면 큰 화면에서 아래가 비고 작은 화면에서는 넘친다.
  -->
  <DeceasedDrawer class="w-[1050px]" content-class="p-0">
    <div class="flex h-full overflow-hidden ">
      <!-- 좌측 스티키 바로가기 네비게이션 -->
      <div class="w-[200px] border-r border-gray-200  p-4 flex flex-col gap-1.5 shrink-0">
        <div class="text-xs font-bold text-gray-400 mb-3 px-2 uppercase tracking-wider">
          분류 목록
        </div>
        <button
          v-for="item in navItems"
          :key="item.id"
          class="w-full text-left px-3 py-2.5 rounded-lg text-sm font-medium transition-all flex items-center justify-between"
          :class="activeSection === item.id 
            ? 'bg-blue-50 text-blue-600 shadow-sm font-semibold' 
            : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'"
          @click="scrollToSection(item.id)"
          type="button"
        >
          {{ item.label }}
          <span v-if="activeSection === item.id" class="w-1.5 h-1.5 bg-blue-500 rounded-full"></span>
        </button>
      </div>

      <!-- 우측 통합 스크롤 컨테이너 -->
      <div ref="scrollContainerRef" class="flex-1 overflow-y-auto p-6 space-y-6 scroll-smooth ">
        <Form layout="vertical">
          <!-- 1. 기본 정보 섹션 -->
          <div id="section-basic" class=" p-6 rounded-xl border border-gray-200 shadow-sm space-y-4">
            <h2 class="text-base font-semibold text-gray-800 border-b pb-2 mb-4">기본 정보</h2>
            <DeceasedBasicForm ref="basicFormRef" v-model="formModel" />
            
            <div class="grid grid-cols-2 gap-4">
              <Form.Item label="장례 상태">
                <DictSelect dict-code="FUNERAL_STATUS" v-model:value="formModel.status" />
              </Form.Item>
              <Form.Item label="작고 일시">
                <AutoDatePicker ref="deathDatePickerRef" v-model:value="deathDateVal" />
              </Form.Item>
            </div>
            
            <div class="grid grid-cols-2 gap-4">
              <Form.Item label="입관 일시">
                <AutoDatePicker v-model:value="funeralDateVal" />
              </Form.Item>
              <Form.Item label="발인 일시">
                <AutoDatePicker v-model:value="burialDateVal" :offset-days="3" />
              </Form.Item>
            </div>
          </div>

          <!-- 2. 상주 정보 섹션 -->
          <div id="section-mourners" class=" p-6 rounded-xl border border-gray-200 shadow-sm space-y-4 mt-6">
            <h2 class="text-base font-semibold text-gray-800 border-b pb-2 mb-4">상주 정보</h2>
            <DeceasedMournersForm v-model="formModel.mourners" />
          </div>

          <!-- 3. 계약자 정보 섹션 -->
          <div id="section-contractor" class=" p-6 rounded-xl border border-gray-200 shadow-sm space-y-4 mt-6">
            <h2 class="text-base font-semibold text-gray-800 border-b pb-2 mb-4">계약자 정보</h2>
            <DeceasedContractorForm v-model="formModel.contractor" />
          </div>

          <!-- 4. 담당자 정보 섹션 -->
          <div id="section-manager" class=" p-6 rounded-xl border border-gray-200 shadow-sm space-y-4 mt-6">
            <h2 class="text-base font-semibold text-gray-800 border-b pb-2 mb-4">담당자 정보</h2>
            <DeceasedManagerForm v-model="formModel.manager" />
          </div>

          <!-- 5. 시설 이용 섹션 -->
          <div id="section-facilities" class=" p-6 rounded-xl border border-gray-200 shadow-sm space-y-4 mt-6">
            <h2 class="text-base font-semibold text-gray-800 border-b pb-2 mb-4">시설 이용 내역</h2>
            <DeceasedFacilitiesForm v-model="formModel.facilities" />
          </div>

          <!-- 6. 사진 관리 섹션 -->
          <div id="section-photos" class=" p-6 rounded-xl border border-gray-200 shadow-sm space-y-4 mt-6">
            <h2 class="text-base font-semibold text-gray-800 border-b pb-2 mb-4">사진 관리 및 추모 앨범</h2>
            <DeceasedPhotoForm v-model="formModel" />
          </div>

          <!-- 7. 호실 지정 섹션 -->
          <div id="section-rooms" class=" p-6 rounded-xl border border-gray-200 shadow-sm space-y-4 mt-6">
            <h2 class="text-base font-semibold text-gray-800 border-b pb-2 mb-4">호실 배정 이력</h2>
            <DeceasedRoomsForm v-model="formModel.rooms" />
          </div>
        </Form>
      </div>
    </div>

    <template #prepend-footer>
      <Button 
        v-if="isEditMode && formModel.status === 'FUNERAL_DEPARTURE_COMPLETED'" 
        type="primary" 
        danger 
        ghost
        @click="handleCancelDeparture"
      >
        출상 취소
      </Button>
    </template>
  </DeceasedDrawer>
</template>
