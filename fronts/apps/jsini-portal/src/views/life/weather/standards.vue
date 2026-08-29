<script lang="ts" setup>
import type { VxeTableGridOptions } from '#/adapter/vxe-table';

import { ref } from 'vue';

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
  createStandard,
  deleteStandard,
  getStandards,
  type LifeWeatherApi,
  updateStandard,
} from '#/api/life/weather';

/**
 * [판정 기준 관리]
 *
 * 원본: ghubfront WeatherStandardList.vue.
 * 실황이 어떤 조건일 때 이벤트로 기록할지 정하는 기준을 관리한다 —
 * category / operator(GE,LE,GT,LT,EQ,BT,NB,DGE,DLE) / threshold /
 * 체감온도 사용 여부 / 한파 복합 조건 등.
 * 원본은 구분 목록을 공통코드(WEATHER_FACTOR)에서 받았지만 코드 API 가
 * 이식 범위 밖이라 아래 CATEGORIES 상수로 대신한다 (모르는 값은 원문 표시).
 */

// ── 상수 ─────────────────────────────────────────────────────
const CATEGORIES = [
  { label: '풍속', value: 'WIND' },
  { label: '강우', value: 'RAIN' },
  { label: '강설', value: 'SNOW' },
  { label: '폭염', value: 'HEAT' },
  { label: '한파', value: 'COLD' },
  { label: '기온', value: 'T1H' },
  { label: '습도', value: 'REH' },
];

const OPERATORS = [
  { label: '>= (이상)', value: 'GE' },
  { label: '<= (이하)', value: 'LE' },
  { label: '> (초과)', value: 'GT' },
  { label: '< (미만)', value: 'LT' },
  { label: '== (동일)', value: 'EQ' },
  { label: 'Between (사이)', value: 'BT' },
  { label: 'Not Between (범위 밖)', value: 'NB' },
  { label: 'Diff >= (차이 이상)', value: 'DGE' },
  { label: 'Diff <= (차이 이하)', value: 'DLE' },
];

const WORK_STATUSES = [
  { label: '작업 가능 (ALLOW)', value: 'ALLOW' },
  { label: '주의 (CAUTION)', value: 'CAUTION' },
  { label: '작업 제한 (RESTRICTED)', value: 'RESTRICTED' },
  { label: '작업 중지 (SUSPENDED)', value: 'SUSPENDED' },
];

const WORK_STATUS_COLOR: Record<string, string> = {
  ALLOW: 'success',
  CAUTION: 'warning',
  RESTRICTED: 'warning',
  SUSPENDED: 'error',
};

const getCategoryLabel = (val: string) =>
  CATEGORIES.find((c) => c.value === val)?.label ?? val;
const getStatusLabel = (val: string) =>
  WORK_STATUSES.find((s) => s.value === val)?.label ?? val;
const getOperatorLabel = (val?: null | string) =>
  OPERATORS.find((o) => o.value === val)?.label.split(' ')[1] ?? val ?? '';

// ── 그리드 ───────────────────────────────────────────────────
const searchText = ref('');

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        align: 'center',
        field: 'category',
        slots: { default: 'category' },
        title: '구분',
        width: 90,
      },
      { align: 'left', field: 'name', minWidth: 130, title: '명칭' },
      { align: 'left', field: 'conditionText', minWidth: 160, title: '조건/설명' },
      {
        align: 'left',
        field: 'threshold',
        minWidth: 240,
        slots: { default: 'threshold' },
        title: '비교 기준 및 수치',
      },
      {
        align: 'center',
        field: 'workStatus',
        slots: { default: 'workStatus' },
        title: '작업상태',
        width: 160,
      },
      { align: 'center', field: 'sortOrder', title: '순서', width: 70 },
      {
        align: 'center',
        field: 'action',
        fixed: 'right',
        slots: { default: 'action' },
        title: '관리',
        width: 110,
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
          const rows = await getStandards();
          // 검색어(명칭·조건·구분) 필터 + 정렬 순서 오름차순 (원본과 동일)
          const s = searchText.value.trim().toLowerCase();
          const filtered = s
            ? rows.filter(
                (i) =>
                  i.name.toLowerCase().includes(s) ||
                  i.conditionText.toLowerCase().includes(s) ||
                  i.category.toLowerCase().includes(s),
              )
            : rows;
          const sorted = [...filtered].sort(
            (a, b) => (a.sortOrder || 0) - (b.sortOrder || 0),
          );
          return { result: sorted, page: { total: sorted.length } };
        },
      },
    },
    rowConfig: { keyField: 'id' },
  } as VxeTableGridOptions,
});

function handleSearch() {
  gridApi.query();
}

// ── 등록 · 수정 팝업 ─────────────────────────────────────────
const isEdit = ref(false);

const emptyForm = (): Partial<LifeWeatherApi.Standard> => ({
  avgYearDiff: undefined,
  category: 'WIND',
  conditionText: '',
  duration: undefined,
  name: '',
  notificationInterval: undefined,
  operator: 'GE',
  prevDayDiff: undefined,
  sortOrder: 10,
  thresholdValue: undefined,
  thresholdValue2: undefined,
  unit: '',
  useSensibleTemp: false,
  workStatus: '',
});

const formModel = ref<Partial<LifeWeatherApi.Standard>>(emptyForm());

const [StandardModal, standardModalApi] = useVbenModal({
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  },
  title: '판정 기준 설정',
});

function currentRows(): LifeWeatherApi.Standard[] {
  return (gridApi.grid?.getTableData().fullData ?? []) as LifeWeatherApi.Standard[];
}

function onCreate() {
  isEdit.value = false;
  formModel.value = {
    ...emptyForm(),
    sortOrder: (currentRows().length + 1) * 10,
  };
  standardModalApi.setState({ title: '기준 추가' }).open();
}

function onEdit(row: LifeWeatherApi.Standard) {
  isEdit.value = true;
  formModel.value = { ...row };
  standardModalApi.setState({ title: '기준 수정' }).open();
}

async function handleSave() {
  const form = formModel.value;
  if (!form.category) {
    message.warning('구분을 선택하세요.');
    return;
  }
  if (!form.name?.trim()) {
    message.warning('명칭을 입력하세요.');
    return;
  }
  if (!form.conditionText?.trim()) {
    message.warning('조건 설명을 입력하세요.');
    return;
  }
  try {
    if (isEdit.value && form.id) {
      await updateStandard(form.id, form);
      message.success('수정되었습니다.');
    } else {
      await createStandard(form);
      message.success('등록되었습니다.');
    }
    standardModalApi.close();
    gridApi.query();
  } catch {
    message.error('저장에 실패했습니다.');
  }
}

async function onDelete(row: LifeWeatherApi.Standard) {
  try {
    await deleteStandard(row.id);
    message.success('삭제되었습니다.');
    gridApi.query();
  } catch {
    message.error('삭제에 실패했습니다.');
  }
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <div
      class="bg-card mb-4 flex flex-wrap items-center justify-between gap-4 rounded border p-4"
    >
      <div class="flex items-center gap-2">
        <Input
          v-model:value="searchText"
          allow-clear
          class="w-64"
          placeholder="검색 (명칭, 조건 등)"
          @press-enter="handleSearch"
        />
        <Button @click="handleSearch">조회</Button>
      </div>
      <Button v-perm:create type="primary" @click="onCreate">
        <Plus class="mr-1 size-5" />
        기준 추가
      </Button>
    </div>

    <Grid table-title="판정 기준 목록">
      <template #category="{ row }">
        <Tag color="blue">{{ getCategoryLabel(row.category) }}</Tag>
      </template>

      <template #threshold="{ row }">
        <div class="flex flex-wrap items-center gap-x-2 gap-y-1 text-left">
          <Tag class="font-mono">{{ row.operator }}</Tag>
          <span class="text-xs font-medium text-gray-500">
            {{ getOperatorLabel(row.operator) }}
          </span>
          <span class="font-bold text-orange-600">
            <template v-if="row.operator === 'BT' || row.operator === 'NB'">
              {{ row.thresholdValue }} ~ {{ row.thresholdValue2 }}
            </template>
            <template v-else-if="row.operator === 'DGE' || row.operator === 'DLE'">
              기준: {{ row.thresholdValue }}, 차이: {{ row.thresholdValue2 }}
            </template>
            <template v-else>{{ row.thresholdValue }}</template>
            <span class="text-xs font-normal text-gray-400">{{ row.unit }}</span>
          </span>
          <span v-if="row.duration" class="text-xs text-blue-500">
            ({{ row.duration }}일 이상)
          </span>
          <Tag v-if="row.prevDayDiff" color="warning">
            전일대비: {{ row.prevDayDiff }}↓
          </Tag>
          <Tag v-if="row.avgYearDiff">평년대비: {{ row.avgYearDiff }}</Tag>
          <span
            v-if="row.notificationInterval"
            class="text-[10px] font-medium text-purple-600"
          >
            알림 주기: {{ row.notificationInterval }}분
          </span>
          <Tag v-if="row.useSensibleTemp" color="error">체감온도 기준</Tag>
        </div>
      </template>

      <template #workStatus="{ row }">
        <Tag
          v-if="row.workStatus"
          :color="WORK_STATUS_COLOR[row.workStatus] ?? 'default'"
        >
          {{ getStatusLabel(row.workStatus) }}
        </Tag>
      </template>

      <template #action="{ row }">
        <div class="flex justify-center gap-1">
          <Tooltip v-perm:update title="수정">
            <Button size="small" type="link" @click="onEdit(row)">
              <IconifyIcon class="size-4" icon="lucide:edit" />
            </Button>
          </Tooltip>
          <Popconfirm
            placement="topLeft"
            title="정말로 이 기준을 삭제하시겠습니까?"
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

    <StandardModal class="w-[620px]">
      <div class="p-6">
        <Form layout="vertical">
          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="구분" required>
              <Select
                v-model:value="formModel.category"
                :options="CATEGORIES"
              />
            </Form.Item>
            <Form.Item label="명칭" required>
              <Input v-model:value="formModel.name" placeholder="예: 강풍 주의" />
            </Form.Item>
          </div>

          <Form.Item label="조건 설명" required>
            <Input.TextArea
              v-model:value="formModel.conditionText"
              :rows="2"
              placeholder="예: 10m/s 이상"
            />
          </Form.Item>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="비교 연산자">
              <Select v-model:value="formModel.operator" :options="OPERATORS" />
            </Form.Item>
            <Form.Item label="단위">
              <Input v-model:value="formModel.unit" placeholder="m/s, mm, ℃" />
            </Form.Item>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item
              :label="
                formModel.operator === 'DGE' || formModel.operator === 'DLE'
                  ? '기준값 1'
                  : '기준값'
              "
            >
              <InputNumber
                v-model:value="formModel.thresholdValue"
                style="width: 100%"
              />
            </Form.Item>
            <Form.Item
              v-if="['BT', 'DGE', 'DLE', 'NB'].includes(formModel.operator || '')"
              :label="
                formModel.operator === 'BT' || formModel.operator === 'NB'
                  ? '기준값 2'
                  : '차이값'
              "
            >
              <InputNumber
                v-model:value="formModel.thresholdValue2"
                style="width: 100%"
              />
            </Form.Item>
          </div>

          <Form.Item label="작업 상태">
            <Select
              v-model:value="formModel.workStatus"
              :options="WORK_STATUSES"
              allow-clear
            />
          </Form.Item>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="지속 기간(일)">
              <InputNumber
                v-model:value="formModel.duration"
                :min="0"
                placeholder="예: 2 (2일 이상)"
                style="width: 100%"
              />
              <div class="mt-1 text-xs text-gray-400">
                일 이상 지속 조건 시 입력
              </div>
            </Form.Item>
            <Form.Item label="알림 주기(분)">
              <InputNumber
                v-model:value="formModel.notificationInterval"
                :min="0"
                placeholder="예: 60 (1시간)"
                style="width: 100%"
              />
              <div class="mt-1 text-xs text-gray-400">
                최초 알림 후 재알림 대기 시간 (0: 매번)
              </div>
            </Form.Item>
          </div>

          <Form.Item
            v-if="['COLD', 'HEAT', 'T1H'].includes(formModel.category || '')"
            label="체감온도 사용"
          >
            <Switch
              v-model:checked="formModel.useSensibleTemp"
              checked-children="체감온도 기준"
              un-checked-children="실제기온 기준"
            />
            <div class="mt-1 text-[10px] text-gray-400">
              * 기온 관련 항목일 때 실제 측정 기온 대신 계산된 체감온도와 비교합니다.
            </div>
          </Form.Item>

          <!-- 한파 복합 조건 -->
          <div
            v-if="formModel.category === 'COLD'"
            class="mb-4 rounded border border-blue-200 p-4 dark:border-blue-900"
          >
            <p class="mb-3 text-xs font-bold text-blue-600">
              한파 복합 조건 (필요 시 입력)
            </p>
            <div class="grid grid-cols-2 gap-4">
              <Form.Item label="전일대비하강">
                <InputNumber
                  v-model:value="formModel.prevDayDiff"
                  :precision="1"
                  style="width: 100%"
                />
              </Form.Item>
              <Form.Item label="평년대비차이">
                <InputNumber
                  v-model:value="formModel.avgYearDiff"
                  :precision="1"
                  style="width: 100%"
                />
              </Form.Item>
            </div>
            <p class="text-[10px] text-gray-500">
              * 주의보 예: 전일대비 10도 하강, 평년보다 3도 낮을 때
            </p>
          </div>

          <Form.Item label="정렬 순서">
            <InputNumber
              v-model:value="formModel.sortOrder"
              :min="0"
              style="width: 100%"
            />
          </Form.Item>
        </Form>
      </div>
    </StandardModal>
  </Page>
</template>
