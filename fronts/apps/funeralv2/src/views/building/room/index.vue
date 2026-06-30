<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons'; 
import { Button, message, Popconfirm, Form, Input, Select, Tooltip, InputNumber } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getRooms, createRoom, updateRoom, deleteRoom } from '#/api/building';
import { getCommonCodes } from '#/api/system/common-code';
import BizSelect from '#/components/BizSelect.vue';
import DictSelect from '#/components/DictSelect.vue';

// ─── 호실유형 공통코드 Map (그리드 레이블 표시용) ─────────────────
const roomTypeMap = ref<Record<string, string>>({});

onMounted(async () => {
  try {
    const res = await getCommonCodes('ROOM_TYPE');
    // requestClient 언래핑: { result: [...] } 구조에서 result 배열 추출
    const raw = (res as any)?.result ?? res;
    const list: any[] = Array.isArray(raw) ? raw : [];
    roomTypeMap.value = Object.fromEntries(
      list.map((item: any) => [item.codeValue, item.codeName])
    );
  } catch {
    // 로드 실패 시 빈 Map 유지 (코드값 원문 표시 fallback)
  }
});

// ─── 상단 필터 상태 ─────────────────────────────────────────────
const selectedCompanyId = ref<string>('');
const selectedBuildingId = ref<string>('');
const filterFloorId = ref<string>('');

// ─── 모달 설정 ──────────────────────────────────────────────────
const [RoomModal, roomModalApi] = useVbenModal({
  title: '호실 정보 설정',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

// ─── 폼 모델 ────────────────────────────────────────────────────
const formModel = ref({
  id: '',
  buildingId: '',
  floorId: '',
  name: '',
  shortName: '',
  roomType: 'FUNERAL_HALL' as string,
  sortOrder: 1,
  status: 'ACTIVE' as 'ACTIVE' | 'INACTIVE',
  remark: ''
});

// ─── 그리드 설정 ────────────────────────────────────────────────
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '호실명', minWidth: 150 },
      { field: 'shortName', title: '짧은 명칭', minWidth: 100 },
      {
        field: 'roomType',
        title: '호실 유형',
        minWidth: 120,
        slots: { default: 'room-type-label' }
      },
      { field: 'sortOrder', title: '정렬 순서', width: 100 },
      {
        field: 'status',
        title: '상태',
        minWidth: 100,
        slots: { default: 'status-tag' }
      },
      { field: 'remark', title: '설명', minWidth: 200 },
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
          // 최소한 회사는 선택되어야 조회
          if (!selectedCompanyId.value) {
            return [];
          }

          const params: { companyId: string; buildingId?: string; floorId?: string } = {
            companyId: selectedCompanyId.value,
          };

          if (selectedBuildingId.value) params.buildingId = selectedBuildingId.value;
          if (filterFloorId.value) params.floorId = filterFloorId.value;

          return await getRooms(params);
        },
      },
    },
  },
});

// ─── Watch: 회사 변경 시 건물 ID 초기화 ─────────────────────────
watch(selectedCompanyId, () => {
  selectedBuildingId.value = '';
  filterFloorId.value = '';
});

// ─── Watch: 건물 변경 시 층 ID 초기화 ───────────────────────────
watch(selectedBuildingId, () => {
  filterFloorId.value = '';
});

// ─── Watch: 층 변경 시 그리드 재조회 ────────────────────────────
watch(filterFloorId, () => {
  gridApi.query();
});

// ─── 신규 등록 ──────────────────────────────────────────────────
function onCreate() {
  formModel.value = {
    id: '',
    buildingId: selectedBuildingId.value,
    floorId: filterFloorId.value,
    name: '',
    shortName: '',
    roomType: 'FUNERAL_HALL',
    sortOrder: 1,
    status: 'ACTIVE',
    remark: ''
  };
  roomModalApi.open();
}

// ─── 수정 ───────────────────────────────────────────────────────
function onEdit(row: any) {
  formModel.value = { ...row };
  roomModalApi.open();
}

// ─── 삭제 ───────────────────────────────────────────────────────
async function onDelete(row: any) {
  try {
    await deleteRoom(row.id);
    message.success('호실 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

// ─── 저장 ───────────────────────────────────────────────────────
async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateRoom(formModel.value.id, formModel.value);
      message.success('호실 정보가 수정되었습니다.');
    } else {
      await createRoom(formModel.value);
      message.success('호실 정보가 등록되었습니다.');
    }
    roomModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <!-- ── 상단 필터 바 ─────────────────────────────────────────── -->
    <div class="mb-4 flex flex-wrap items-center justify-between gap-4 bg-card p-4 rounded-lg shadow-sm border border-border">
      <div class="flex flex-wrap items-center gap-4">
        <!-- 회사 선택 -->
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm whitespace-nowrap">회사 필터:</span>
          <BizSelect
            v-model:value="selectedCompanyId"
            type="company"
            auto-select-first
            placeholder="회사 선택"
            class="w-48 md:w-64"
            show-search
            option-filter-prop="label"
          />
        </div>

        <!-- 건물 선택 (회사 종속) -->
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm whitespace-nowrap">건물 필터:</span>
          <BizSelect
            v-model:value="selectedBuildingId"
            type="building"
            :params="{ companyId: selectedCompanyId }"
            auto-select-first
            placeholder="건물 선택"
            class="w-48 md:w-64"
            show-search
            option-filter-prop="label"
          />
        </div>

        <!-- 층 선택 (건물 종속) -->
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm whitespace-nowrap">층 필터:</span>
          <BizSelect
            v-model:value="filterFloorId"
            type="floor"
            :params="{ buildingId: selectedBuildingId }"
            auto-select-first
            placeholder="층 선택"
            class="w-48 md:w-64"
            show-search
            allow-clear
            option-filter-prop="label"
          />
        </div>
      </div>

      <!-- 신규 등록 버튼 -->
      <Button type="primary" @click="onCreate">
        <Plus class="size-5 mr-1" />
        신규 호실 등록
      </Button>
    </div>

    <!-- ── 그리드 ───────────────────────────────────────────────── -->
    <Grid table-title="호실 정보 목록">
      <template #status-tag="{ row }">
        <span
          :class="['px-2 py-1 rounded text-xs font-semibold', row.status === 'ACTIVE' ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800']"
        >
          {{ row.status === 'ACTIVE' ? '사용중' : '사용안함' }}
        </span>
      </template>

      <template #room-type-label="{ row }">
        <span>{{ roomTypeMap[row.roomType] ?? row.roomType }}</span>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2 justify-center">
          <Tooltip title="수정">
            <Button type="link" size="small" @click="onEdit(row)">
              <IconifyIcon icon="lucide:edit" class="size-4" />
            </Button>
          </Tooltip>
          <Popconfirm title="해당 호실을 삭제하시겠습니까?" @confirm="onDelete(row)" placement="topLeft">
            <Tooltip title="삭제">
              <Button type="link" size="small" danger>
                <IconifyIcon icon="lucide:trash-2" class="size-4" />
              </Button>
            </Tooltip>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- ── 호실 등록/수정 모달 ───────────────────────────────────── -->
    <RoomModal>
      <div class="p-6">
        <Form layout="vertical">
          <!-- 배정 층 (BizSelect: 건물 종속 → 층 종속) -->
          <Form.Item label="배정 층" required>
            <BizSelect
              v-model:value="formModel.floorId"
              type="floor"
              :params="{ buildingId: formModel.buildingId || selectedBuildingId }"
              placeholder="층을 선택해주세요"
            />
          </Form.Item>
          <Form.Item label="호실명" required>
            <Input v-model:value="formModel.name" placeholder="예: 101호 빈소, 안치실 A 등" />
          </Form.Item>
          <Form.Item label="짧은 명칭">
            <Input v-model:value="formModel.shortName" placeholder="예: 101호, 특실 등" />
          </Form.Item>
          <Form.Item label="호실 유형">
            <DictSelect
              dict-code="ROOM_TYPE"
              v-model:value="formModel.roomType"
              placeholder="호실 유형을 선택해주세요"
              style="width: 100%"
            />
          </Form.Item>
          <Form.Item label="정렬 순서">
            <InputNumber v-model:value="formModel.sortOrder" :min="1" style="width: 100%" />
          </Form.Item>
          <Form.Item label="사용 여부">
            <Select v-model:value="formModel.status">
              <Select.Option value="ACTIVE">사용</Select.Option>
              <Select.Option value="INACTIVE">미사용</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="설명 입력" />
          </Form.Item>
        </Form>
      </div>
    </RoomModal>
  </Page>
</template>
