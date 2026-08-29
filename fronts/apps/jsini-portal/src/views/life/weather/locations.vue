<script lang="ts" setup>
import type { VxeTableGridOptions } from '#/adapter/vxe-table';

import { onMounted, ref } from 'vue';

import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons';

import {
  Button,
  Form,
  Input,
  InputNumber,
  message,
  Popconfirm,
  Select,
  Switch,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  createLocation,
  deleteLocation,
  getLocations,
  getWarningZones,
  type LifeWeatherApi,
  reorderLocations,
  searchGrid,
  updateLocation,
} from '#/api/life/weather';

/**
 * [관측 지역 관리]
 *
 * 원본: ghubfront WeatherInfo.vue (지역 관리 화면).
 * 기상청 API 연동을 위한 NX/NY 격자좌표 지역을 관리한다.
 * 등록 팝업에서 행정구역명으로 격자좌표를 검색(searchGrid)하면 nx/ny 가
 * 자동으로 채워진다. 중기예보 코드는 직접 입력하고(코드 API 미이식),
 * 특보구역 코드는 기상청 구역 마스터(getWarningZones)에서 고른다.
 * 정렬은 원본의 드래그 대신 행의 위/아래 버튼으로 옮겼다.
 * 원본의 지도 확인(카카오맵)·좌표 변환 모달은 이식 범위에서 뺐다.
 */

// ── 그리드 ───────────────────────────────────────────────────
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { align: 'center', field: 'sortOrder', title: '순서', width: 70 },
      {
        align: 'left',
        field: 'name',
        minWidth: 150,
        slots: { default: 'name' },
        title: '지역 명칭',
      },
      {
        align: 'center',
        field: 'coord',
        slots: { default: 'coord' },
        title: '좌표 (NX, NY)',
        width: 120,
      },
      {
        align: 'center',
        field: 'warningAreaCode',
        slots: { default: 'warningArea' },
        title: '특보구역',
        width: 120,
      },
      {
        align: 'center',
        field: 'midCode',
        slots: { default: 'midCode' },
        title: '중기예보 코드',
        width: 170,
      },
      { align: 'left', field: 'description', minWidth: 160, title: '설명' },
      {
        align: 'center',
        field: 'isActive',
        slots: { default: 'active' },
        title: '상태',
        width: 90,
      },
      {
        align: 'center',
        field: 'action',
        fixed: 'right',
        slots: { default: 'action' },
        title: '작업',
        width: 170,
      },
    ],
    height: 'auto',
    pagerConfig: { enabled: false },
    proxyConfig: {
      // 페이저 없는 그리드에 배열만 반환하면 vxe 가 목록을 찾지 못한다
      // (portal/system/menu/list.vue 에 기록된 선례) — result 로 감싸고 위치를 명시한다.
      response: { list: 'result' },
      ajax: {
        query: async () => {
          const rows = await getLocations();
          return { result: rows, page: { total: rows.length } };
        },
      },
    },
    rowConfig: { keyField: 'id' },
  } as VxeTableGridOptions,
});

function currentRows(): LifeWeatherApi.Location[] {
  return (gridApi.grid?.getTableData().fullData ?? []) as LifeWeatherApi.Location[];
}

// ── 특보구역 마스터 ──────────────────────────────────────────
const warningZones = ref<any[]>([]);

async function fetchWarningZones() {
  try {
    warningZones.value = await getWarningZones();
  } catch {
    // 특보구역 마스터는 선택 입력이라 실패해도 화면을 막지 않는다
  }
}

// ── 등록 · 수정 팝업 ─────────────────────────────────────────
const isEdit = ref(false);

interface LocationForm extends Partial<LifeWeatherApi.Location> {
  region1?: string;
  region2?: string;
}

const emptyForm = (): LocationForm => ({
  description: '',
  isActive: true,
  midTermLandCode: '',
  midTermTempCode: '',
  name: '',
  nx: 60,
  ny: 127,
  region1: '',
  region2: '',
  region3: '',
  sortOrder: 0,
  warningAreaCode: '',
});

const formModel = ref<LocationForm>(emptyForm());

const [LocationModal, locationModalApi] = useVbenModal({
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  },
  title: '지역 설정',
});

// 격자좌표 검색
const gridSearchQuery = ref('');
const gridSearchResults = ref<LifeWeatherApi.GridCoordinate[]>([]);
const gridSearchLoading = ref(false);

async function handleGridSearch() {
  if (!gridSearchQuery.value) return;
  gridSearchLoading.value = true;
  try {
    gridSearchResults.value = await searchGrid(gridSearchQuery.value);
    if (gridSearchResults.value.length === 0) {
      message.info('검색 결과가 없습니다.');
    }
  } catch {
    message.error('좌표 검색에 실패했습니다.');
  } finally {
    gridSearchLoading.value = false;
  }
}

/** 검색된 격자좌표를 폼에 적용한다. 지역 명칭이 비어 있으면 행정구역명으로 채운다. */
function applyGridCoordinate(item: LifeWeatherApi.GridCoordinate) {
  formModel.value.nx = item.nx;
  formModel.value.ny = item.ny;
  formModel.value.region1 = item.region1 ?? '';
  formModel.value.region2 = item.region2 ?? '';
  formModel.value.region3 = item.region3 ?? '';
  if (!formModel.value.name) {
    formModel.value.name =
      `${item.region1 ?? ''} ${item.region2 ?? ''} ${item.region3 ?? ''}`.trim();
  }
  gridSearchResults.value = [];
}

function onCreate() {
  isEdit.value = false;
  formModel.value = {
    ...emptyForm(),
    sortOrder: currentRows().length * 10 + 10,
  };
  gridSearchQuery.value = '';
  gridSearchResults.value = [];
  locationModalApi.setState({ title: '지역 추가' }).open();
}

function onEdit(row: LifeWeatherApi.Location) {
  isEdit.value = true;
  formModel.value = { ...emptyForm(), ...row };
  gridSearchQuery.value = '';
  gridSearchResults.value = [];
  locationModalApi.setState({ title: '지역 수정' }).open();
}

async function handleSave() {
  const form = formModel.value;
  if (!form.name?.trim()) {
    message.warning('지역 명칭을 입력하세요.');
    return;
  }
  if (!form.nx || !form.ny) {
    message.warning('NX/NY 좌표를 입력하세요.');
    return;
  }
  try {
    if (isEdit.value && form.id) {
      await updateLocation(form.id, form);
      message.success('지역 정보가 수정되었습니다.');
    } else {
      await createLocation(form);
      message.success('지역이 추가되었습니다.');
    }
    locationModalApi.close();
    gridApi.query();
  } catch {
    message.error('저장에 실패했습니다.');
  }
}

async function onDelete(row: LifeWeatherApi.Location) {
  try {
    await deleteLocation(row.id);
    message.success('지역이 삭제되었습니다.');
    gridApi.query();
  } catch {
    message.error('삭제에 실패했습니다.');
  }
}

/** 행을 위/아래로 옮기고 전체 순서를 10 단위로 다시 매겨 저장한다 */
async function onMove(row: LifeWeatherApi.Location, direction: -1 | 1) {
  const rows = [...currentRows()];
  const index = rows.findIndex((r) => r.id === row.id);
  const target = index + direction;
  if (index === -1 || target < 0 || target >= rows.length) return;

  [rows[index], rows[target]] = [rows[target]!, rows[index]!];
  const updates = rows.map((item, i) => ({ id: item.id, sortOrder: (i + 1) * 10 }));
  try {
    await reorderLocations(updates);
    message.success('순서가 변경되었습니다.');
  } catch {
    message.error('순서 변경 저장에 실패했습니다.');
  } finally {
    gridApi.query();
  }
}

onMounted(fetchWarningZones);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <div
      class="bg-card mb-4 flex items-center justify-between rounded border p-4"
    >
      <div>
        <h3 class="text-base font-bold">관측 대상 지역 목록</h3>
        <p class="text-sm text-gray-500">
          기상청 API 연동을 위한 NX, NY 좌표 정보를 관리합니다.
        </p>
      </div>
      <Button v-perm:create type="primary" @click="onCreate">
        <Plus class="mr-1 size-5" />
        지역 추가
      </Button>
    </div>

    <Grid table-title="관측 지역">
      <template #name="{ row }">
        <div class="flex items-center gap-2">
          <IconifyIcon class="size-4 text-blue-500" icon="lucide:map-pin" />
          <span class="font-bold">{{ row.name }}</span>
        </div>
      </template>

      <template #coord="{ row }">
        <Tag>{{ row.nx }}, {{ row.ny }}</Tag>
      </template>

      <template #warningArea="{ row }">
        <Tag v-if="row.warningAreaCode" color="warning">
          {{ row.warningAreaCode }}
        </Tag>
        <span v-else class="text-gray-300">-</span>
      </template>

      <template #midCode="{ row }">
        <div class="flex flex-col text-[10px] leading-4 text-gray-500">
          <span v-if="row.midTermLandCode">육상: {{ row.midTermLandCode }}</span>
          <span v-if="row.midTermTempCode">기온: {{ row.midTermTempCode }}</span>
          <span v-if="!row.midTermLandCode && !row.midTermTempCode" class="text-gray-300">
            -
          </span>
        </div>
      </template>

      <template #active="{ row }">
        <Tag :color="row.isActive ? 'success' : 'default'">
          {{ row.isActive ? '사용중' : '중지' }}
        </Tag>
      </template>

      <template #action="{ row }">
        <div class="flex justify-center gap-1">
          <Tooltip v-perm:update title="위로">
            <Button size="small" type="link" @click="onMove(row, -1)">
              <IconifyIcon class="size-4" icon="lucide:arrow-up" />
            </Button>
          </Tooltip>
          <Tooltip v-perm:update title="아래로">
            <Button size="small" type="link" @click="onMove(row, 1)">
              <IconifyIcon class="size-4" icon="lucide:arrow-down" />
            </Button>
          </Tooltip>
          <Tooltip v-perm:update title="수정">
            <Button size="small" type="link" @click="onEdit(row)">
              <IconifyIcon class="size-4" icon="lucide:edit" />
            </Button>
          </Tooltip>
          <Popconfirm
            placement="topLeft"
            title="정말로 이 지역을 삭제하시겠습니까?"
            @confirm="onDelete(row)"
          >
            <Tooltip v-perm:delete title="삭제">
              <Button danger size="small" type="link">
                <IconifyIcon class="size-4" icon="lucide:trash-2" />
              </Button>
            </Tooltip>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <LocationModal class="w-[560px]">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="지역 명칭" required>
            <Input
              v-model:value="formModel.name"
              placeholder="예: 울산 현장, 서울 본사"
            />
          </Form.Item>

          <!-- 격자좌표 검색 -->
          <div class="mb-4 rounded border border-blue-200 p-3 dark:border-blue-900">
            <div class="mb-2 text-xs font-bold uppercase text-blue-600">
              격자 좌표 검색 (행정구역명)
            </div>
            <div class="flex gap-2">
              <Input
                v-model:value="gridSearchQuery"
                placeholder="예: 울산, 강남구"
                @press-enter="handleGridSearch"
              />
              <Button :loading="gridSearchLoading" @click="handleGridSearch">
                <IconifyIcon class="size-4" icon="lucide:search" />
              </Button>
            </div>
            <div
              v-if="gridSearchResults.length > 0"
              class="mt-2 max-h-32 overflow-y-auto rounded border"
            >
              <div
                v-for="(item, idx) in gridSearchResults"
                :key="idx"
                class="hover:bg-accent cursor-pointer border-b p-2 text-xs last:border-0"
                @click="applyGridCoordinate(item)"
              >
                <span class="font-bold">
                  {{ item.region1 }} {{ item.region2 }} {{ item.region3 || '' }}
                </span>
                <span class="ml-2 text-gray-400">
                  (NX: {{ item.nx }}, NY: {{ item.ny }})
                </span>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="행정구역 (참조)">
              <Input
                :value="
                  `${formModel.region1 || ''} ${formModel.region2 || ''} ${formModel.region3 || ''}`.trim()
                "
                placeholder="검색 시 자동 입력"
                readonly
              />
            </Form.Item>
            <Form.Item label="정렬 순서">
              <InputNumber
                v-model:value="formModel.sortOrder"
                :min="0"
                style="width: 100%"
              />
            </Form.Item>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="NX 좌표" required>
              <InputNumber
                v-model:value="formModel.nx"
                :max="200"
                :min="1"
                style="width: 100%"
              />
            </Form.Item>
            <Form.Item label="NY 좌표" required>
              <InputNumber
                v-model:value="formModel.ny"
                :max="200"
                :min="1"
                style="width: 100%"
              />
            </Form.Item>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="중기육상 코드">
              <Input
                v-model:value="formModel.midTermLandCode"
                placeholder="예: 11H20000"
              />
            </Form.Item>
            <Form.Item label="중기기온 코드">
              <Input
                v-model:value="formModel.midTermTempCode"
                placeholder="예: 11H20201"
              />
            </Form.Item>
          </div>

          <Form.Item label="특보발효구역 코드">
            <Select
              v-model:value="formModel.warningAreaCode"
              :options="
                warningZones.map((z) => ({
                  label: `${z.regKo || ''} - ${z.regName || ''} - ${z.regId}`,
                  value: z.regId,
                }))
              "
              allow-clear
              option-filter-prop="label"
              placeholder="특보 구역을 선택하세요"
              show-search
            />
            <div class="mt-1 text-[10px] text-gray-400">
              ※ 기상청 특보 발생 시 매칭되는 구역 코드입니다.
            </div>
          </Form.Item>

          <Form.Item label="설명">
            <Input.TextArea v-model:value="formModel.description" :rows="2" />
          </Form.Item>
          <Form.Item label="사용 여부">
            <Switch v-model:checked="formModel.isActive" />
          </Form.Item>
        </Form>
      </div>
    </LocationModal>
  </Page>
</template>
