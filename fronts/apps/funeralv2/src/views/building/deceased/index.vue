<script lang="ts" setup>
import { ref, onMounted, onUnmounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Plus, Pencil, Trash2, Image } from '@vben/icons';
import { Button, message, Popconfirm, Tag, Form, Input, InputNumber, RangePicker } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDeceasedList, deleteDeceased } from '#/api/building';
import DeceasedFormModal from './modules/deceased-form-modal.vue';
import BizSelect from '#/components/BizSelect.vue';
import DictSelect from '#/components/DictSelect.vue';
import type { Dayjs } from 'dayjs';

const formModalRef = ref<InstanceType<typeof DeceasedFormModal> | null>(null);

// ─── 검색 폼 모델 ──────────────────────────────────────────────────
const searchForm = ref({
  companyId: '',
  buildingId: '',
  floorId: '',
  roomId: '',
  name: '',
  gender: '',
  minAge: undefined as number | undefined,
  maxAge: undefined as number | undefined,
  religion: '',
  status: ''
});

const roomEnterDates = ref<[Dayjs, Dayjs] | undefined>(undefined);
const funeralDates = ref<[Dayjs, Dayjs] | undefined>(undefined);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'memorialPhotoUrl',
        title: '고인사진',
        width: 80,
        slots: { default: 'photo' }
      },
      { field: 'name', title: '고인명', minWidth: 100 },
      {
        field: 'gender',
        title: '성별',
        minWidth: 80,
        formatter: ({ cellValue }: { cellValue: any }) => (cellValue === 'M' ? '남성' : '여성')
      },
      { field: 'age', title: '연세', minWidth: 80, formatter: ({ cellValue }: { cellValue: any }) => `${cellValue}세` },
      { field: 'religion', title: '종교', minWidth: 100 },
      { field: 'roomName', title: '배정 빈소', minWidth: 120 },
      { field: 'deathDate', title: '작고 일시', minWidth: 160, formatter: ({ cellValue }: { cellValue: any }) => formatDate(cellValue) },
      {
        field: 'status',
        title: '장례 상태',
        minWidth: 120,
        slots: { default: 'status-tag' }
      },
      {
        field: 'action',
        title: '작업',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          const params: Record<string, any> = {};

          if (searchForm.value.companyId) params.companyId = searchForm.value.companyId;
          if (searchForm.value.buildingId) params.buildingId = searchForm.value.buildingId;
          if (searchForm.value.floorId) params.floorId = searchForm.value.floorId;
          if (searchForm.value.roomId) params.roomId = searchForm.value.roomId;
          if (searchForm.value.name) params.name = searchForm.value.name;
          if (searchForm.value.gender) params.gender = searchForm.value.gender;
          if (searchForm.value.minAge !== null) params.minAge = searchForm.value.minAge;
          if (searchForm.value.maxAge !== null) params.maxAge = searchForm.value.maxAge;
          if (searchForm.value.religion) params.religion = searchForm.value.religion;
          if (searchForm.value.status) params.status = searchForm.value.status;

          if (roomEnterDates.value && roomEnterDates.value.length === 2) {
            params.roomEnterStartDate = roomEnterDates.value[0]?.format('YYYY-MM-DDT00:00:00');
            params.roomEnterEndDate = roomEnterDates.value[1]?.format('YYYY-MM-DDT23:59:59');
          }
          if (funeralDates.value && funeralDates.value.length === 2) {
            params.funeralStartDate = funeralDates.value[0]?.format('YYYY-MM-DDT00:00:00');
            params.funeralEndDate = funeralDates.value[1]?.format('YYYY-MM-DDT23:59:59');
          }

          return await getDeceasedList(params);
        },
      },
    },
  },
});

function onCreate() {
  if (formModalRef.value) {
    formModalRef.value.open();
  }
}

function onEdit(row: any) {
  if (formModalRef.value) {
    formModalRef.value.open(row);
  }
}

function onCropPhoto(row: any) {
  const url = `/building/deceased/photo-editor?id=${row.id}`;
  window.open(url, '_blank', 'width=1120,height=860,resizable=yes');
}

function handleMessage(event: MessageEvent) {
  if (event.data === 'deceased-photo-saved') {
    gridApi.query();
  }
}

onMounted(() => {
  window.addEventListener('message', handleMessage);
});

onUnmounted(() => {
  window.removeEventListener('message', handleMessage);
});

async function onDelete(row: any) {
  try {
    await deleteDeceased(row.id);
    message.success('고인 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

function onCompanyChange() {
  searchForm.value.buildingId = '';
  searchForm.value.floorId = '';
  searchForm.value.roomId = '';
}

function onBuildingChange() {
  searchForm.value.floorId = '';
  searchForm.value.roomId = '';
}

function onFloorChange() {
  searchForm.value.roomId = '';
}

function onReset() {
  searchForm.value = {
    companyId: '',
    buildingId: '',
    floorId: '',
    roomId: '',
    name: '',
    gender: '',
    minAge: undefined,
    maxAge: undefined,
    religion: '',
    status: ''
  };
  roomEnterDates.value = undefined;
  funeralDates.value = undefined;
  gridApi.query();
}

function onSearch() {
  gridApi.query();
}

function formatDate(dateStr?: string) {
  if (!dateStr) return '-';
  try {
    return new Date(dateStr).toLocaleString('ko-KR');
  } catch {
    return dateStr;
  }
}
</script>

<template>
  <Page auto-content-height>
    <!-- ── 상단 고급 검색 바 ─────────────────────────────────────────── -->
    <div class="mb-4 bg-card p-5 rounded-lg shadow-sm border border-border">
      <Form layout="vertical" class="space-y-4">
        <!-- 1행: 계층 분류 선택 및 인적 정보 -->
        <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
          <Form.Item label="회사 필터" class="mb-0">
            <BizSelect
              v-model:value="searchForm.companyId"
              type="company"
              placeholder="회사 전체"
              show-all
              @change="onCompanyChange"
            />
          </Form.Item>
          <Form.Item label="건물 필터" class="mb-0">
            <BizSelect
              v-model:value="searchForm.buildingId"
              type="building"
              :params="{ companyId: searchForm.companyId }"
              placeholder="건물 전체"
              show-all
              @change="onBuildingChange"
            />
          </Form.Item>
          <Form.Item label="층 필터" class="mb-0">
            <BizSelect
              v-model:value="searchForm.floorId"
              type="floor"
              :params="{ buildingId: searchForm.buildingId }"
              placeholder="층 전체"
              show-all
              @change="onFloorChange"
            />
          </Form.Item>
          <Form.Item label="호실 필터" class="mb-0">
            <BizSelect
              v-model:value="searchForm.roomId"
              type="room"
              :params="{ 
                companyId: searchForm.companyId || undefined, 
                buildingId: searchForm.buildingId || undefined, 
                floorId: searchForm.floorId || undefined 
              }"
              placeholder="호실 전체"
              show-all
            />
          </Form.Item>
          <Form.Item label="고인명" class="mb-0">
            <Input v-model:value="searchForm.name" placeholder="고인명 입력" allow-clear @press-enter="onSearch" />
          </Form.Item>
          <Form.Item label="성별" class="mb-0">
            <DictSelect
              dict-code="SEX"
              v-model:value="searchForm.gender"
              placeholder="성별 전체"
              show-all
            />
          </Form.Item>
        </div>

        <!-- 2행: 세부 필터 (나이범위, 종교, 기간, 상태) -->
        <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 items-end">
          <Form.Item label="나이 범위" class="mb-0">
            <div class="flex items-center gap-1">
              <InputNumber v-model:value="searchForm.minAge" placeholder="최소" :min="0" class="w-full" />
              <span class="text-gray-400">~</span>
              <InputNumber v-model:value="searchForm.maxAge" placeholder="최대" :min="0" class="w-full" />
            </div>
          </Form.Item>
          <Form.Item label="종교" class="mb-0">
            <DictSelect
              dict-code="RELIGION"
              v-model:value="searchForm.religion"
              placeholder="종교 전체"
              show-all
            />
          </Form.Item>
          <Form.Item label="장례 상태" class="mb-0">
            <DictSelect
              dict-code="FUNERAL_STATUS"
              v-model:value="searchForm.status"
              placeholder="상태 전체"
              show-all
            />
          </Form.Item>
          <Form.Item label="입관 기간" class="mb-0">
            <RangePicker v-model:value="roomEnterDates" class="w-full" />
          </Form.Item>
          <Form.Item label="발인 기간" class="mb-0">
            <RangePicker v-model:value="funeralDates" class="w-full" />
          </Form.Item>
          
          <!-- 버튼 영역 -->
          <div class="flex gap-2 justify-end">
            <Button @click="onReset" class="w-full">초기화</Button>
            <Button type="primary" @click="onSearch" class="w-full">검색</Button>
          </div>
        </div>
      </Form>
    </div>

    <!-- ── 그리드 영역 ───────────────────────────────────────────────── -->
    <Grid table-title="장례식장 고인(Deceased) 등록 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 고인 등록
        </Button>
      </template>

      <template #photo="{ row }">
        <div class="w-10 h-12 bg-gray-100 rounded border border-gray-200 flex items-center justify-center overflow-hidden">
          <img
            v-if="row.memorialPhotoFileId || row.memorialPhotoUrl"
            :src="row.memorialPhotoFileId ? `/api/file/thumbnail/${row.memorialPhotoFileId}` : row.memorialPhotoUrl"
            class="w-full h-full object-cover"
            alt="영정"
          />
          <span v-else class="text-gray-400 text-[10px]">미등록</span>
        </div>
      </template>

      <template #status-tag="{ row }">
        <Tag v-if="row.status === 'IN_HOSPITAL'" color="processing">장례 진행중</Tag>
        <Tag v-else-if="row.status === 'DISCHARGED'" color="warning">발인 완료</Tag>
        <Tag v-else color="success">정산 완료</Tag>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onCropPhoto(row)" title="사진편집">
            <Image class="size-4 text-green-600" />
          </Button>
          <Button type="link" size="small" @click="onEdit(row)" title="수정">
            <Pencil class="size-4 text-blue-600" />
          </Button>
          <Popconfirm title="해당 고인 데이터를 영구 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger title="삭제">
              <Trash2 class="size-4 text-red-600" />
            </Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- 고인 정보 입력 폼 모달 (독립 분리 컴포넌트) -->
    <DeceasedFormModal ref="formModalRef" @saved="gridApi.query()" />
  </Page>
</template>
