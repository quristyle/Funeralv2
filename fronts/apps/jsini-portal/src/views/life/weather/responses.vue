<script lang="ts" setup>
import type { VxeTableGridOptions } from '#/adapter/vxe-table';

import { onMounted, ref, watch } from 'vue';

import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  Button,
  Form,
  Input,
  InputNumber,
  message,
  Popconfirm,
  Tag,
  Tooltip,
} from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  createResponse,
  deleteResponse,
  getResponsesByStandard,
  getStandards,
  type LifeWeatherApi,
  reorderResponses,
  updateResponse,
} from '#/api/life/weather';

/**
 * [기준별 대응 요령]
 *
 * 원본: ghubfront WeatherResponseList.vue.
 * 왼쪽에서 판정 기준을 고르면 오른쪽에 그 기준의 대응 요령(Action Plan)이
 * 나온다. 등록·수정·삭제와 정렬(reorderResponses)을 지원한다.
 * 정렬은 원본의 드래그 대신 행의 위/아래 버튼으로 옮겼다.
 */

// ── 기준 목록 (왼쪽) ─────────────────────────────────────────
const standards = ref<LifeWeatherApi.Standard[]>([]);
const selectedStandard = ref<LifeWeatherApi.Standard | null>(null);
const standardLoading = ref(false);

const CATEGORY_LABELS: Record<string, string> = {
  COLD: '한파',
  HEAT: '폭염',
  RAIN: '강우',
  SNOW: '강설',
  WIND: '풍속',
};
const getCategoryLabel = (val: string) => CATEGORY_LABELS[val] ?? val;

async function fetchStandards() {
  standardLoading.value = true;
  try {
    standards.value = await getStandards();
    if (standards.value.length > 0 && !selectedStandard.value) {
      selectedStandard.value = standards.value[0] ?? null;
    }
  } catch {
    message.error('기준 목록 로드에 실패했습니다.');
  } finally {
    standardLoading.value = false;
  }
}

// ── 대응 요령 그리드 (오른쪽) ────────────────────────────────
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        align: 'left',
        field: 'actionContent',
        minWidth: 220,
        slots: { default: 'actionContent' },
        title: '대응 행동 내용',
      },
      { align: 'left', field: 'description', title: '비고', width: 160 },
      { align: 'center', field: 'sortOrder', title: '순서', width: 70 },
      {
        align: 'center',
        field: 'action',
        fixed: 'right',
        slots: { default: 'action' },
        title: '관리',
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
          if (!selectedStandard.value) return { result: [], page: { total: 0 } };
          const rows = await getResponsesByStandard(selectedStandard.value.id);
          return { result: rows, page: { total: rows.length } };
        },
      },
    },
    rowConfig: { keyField: 'id' },
  } as VxeTableGridOptions,
});

watch(selectedStandard, () => gridApi.query());

// ── 등록 · 수정 팝업 ─────────────────────────────────────────
const isEdit = ref(false);

const formModel = ref<Partial<LifeWeatherApi.Response>>({
  actionContent: '',
  description: '',
  sortOrder: 10,
});

const [ResponseModal, responseModalApi] = useVbenModal({
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  },
  title: '대응 요령 설정',
});

function currentRows(): LifeWeatherApi.Response[] {
  return (gridApi.grid?.getTableData().fullData ?? []) as LifeWeatherApi.Response[];
}

function onCreate() {
  if (!selectedStandard.value) return;
  isEdit.value = false;
  formModel.value = {
    actionContent: '',
    description: '',
    sortOrder: (currentRows().length + 1) * 10,
    weatherStandardId: selectedStandard.value.id,
  };
  responseModalApi.setState({ title: '대응 요령 추가' }).open();
}

function onEdit(row: LifeWeatherApi.Response) {
  isEdit.value = true;
  formModel.value = { ...row };
  responseModalApi.setState({ title: '대응 요령 수정' }).open();
}

async function handleSave() {
  if (!formModel.value.actionContent?.trim()) {
    message.warning('대응 내용을 입력하세요.');
    return;
  }
  try {
    if (isEdit.value && formModel.value.id) {
      await updateResponse(formModel.value.id, formModel.value);
      message.success('수정되었습니다.');
    } else {
      await createResponse(formModel.value);
      message.success('등록되었습니다.');
    }
    responseModalApi.close();
    gridApi.query();
  } catch {
    message.error('저장에 실패했습니다.');
  }
}

async function onDelete(row: LifeWeatherApi.Response) {
  try {
    await deleteResponse(row.id);
    message.success('삭제되었습니다.');
    gridApi.query();
  } catch {
    message.error('삭제에 실패했습니다.');
  }
}

/** 행을 위/아래로 옮기고 전체 순서를 10 단위로 다시 매겨 저장한다 */
async function onMove(row: LifeWeatherApi.Response, direction: -1 | 1) {
  const rows = [...currentRows()];
  const index = rows.findIndex((r) => r.id === row.id);
  const target = index + direction;
  if (index === -1 || target < 0 || target >= rows.length) return;

  [rows[index], rows[target]] = [rows[target]!, rows[index]!];
  const updates = rows.map((item, i) => ({ id: item.id, sortOrder: (i + 1) * 10 }));
  try {
    await reorderResponses(updates);
    message.success('순서가 변경되었습니다.');
  } catch {
    message.error('순서 변경 저장에 실패했습니다.');
  } finally {
    gridApi.query();
  }
}

onMounted(fetchStandards);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <div class="flex h-full min-h-0 gap-4">
      <!-- 기준 목록 -->
      <div class="bg-card flex w-72 shrink-0 flex-col rounded border">
        <div class="flex items-center justify-between border-b p-3">
          <h3 class="text-sm font-bold">날씨 기준 선택</h3>
          <Button :loading="standardLoading" size="small" @click="fetchStandards">
            <IconifyIcon class="size-4" icon="lucide:refresh-cw" />
          </Button>
        </div>
        <div class="min-h-0 flex-1 space-y-2 overflow-y-auto p-2">
          <div
            v-for="std in standards"
            :key="std.id"
            :class="[
              'cursor-pointer rounded border p-3 transition-colors',
              selectedStandard?.id === std.id
                ? 'border-primary bg-primary/10'
                : 'border-transparent hover:bg-accent',
            ]"
            @click="selectedStandard = std"
          >
            <div class="flex items-center justify-between">
              <Tag>{{ getCategoryLabel(std.category) }}</Tag>
              <Tag
                v-if="std.workStatus"
                :color="std.workStatus === 'ALLOW' ? 'success' : 'error'"
              >
                {{ std.workStatus }}
              </Tag>
            </div>
            <div class="mt-2 text-sm font-medium">{{ std.name }}</div>
            <div class="mt-1 truncate text-xs text-gray-500">
              {{ std.conditionText }}
            </div>
          </div>
          <div
            v-if="!standardLoading && standards.length === 0"
            class="py-6 text-center text-xs text-gray-400"
          >
            등록된 기준이 없습니다.
          </div>
        </div>
      </div>

      <!-- 대응 요령 목록 -->
      <div class="flex min-w-0 flex-1 flex-col">
        <Grid
          :table-title="
            selectedStandard ? `대응 요령 — ${selectedStandard.name}` : '대응 요령'
          "
        >
          <template #toolbar-tools>
            <GridIconButton
              v-perm:create
              :disabled="!selectedStandard"
              icon="vxe-icon-add"
              title="대응 추가"
              @click="onCreate"
            />
          </template>

          <template #actionContent="{ row }">
            <div class="whitespace-pre-wrap text-left">{{ row.actionContent }}</div>
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
                title="이 대응 요령을 삭제하시겠습니까?"
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
      </div>
    </div>

    <ResponseModal>
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="대응 내용" required>
            <Input.TextArea
              v-model:value="formModel.actionContent"
              :rows="4"
              placeholder="예: 매시간 10분 휴식"
            />
          </Form.Item>
          <Form.Item label="비고/설명">
            <Input.TextArea v-model:value="formModel.description" :rows="2" />
          </Form.Item>
          <Form.Item label="정렬 순서">
            <InputNumber
              v-model:value="formModel.sortOrder"
              :min="0"
              style="width: 100%"
            />
          </Form.Item>
        </Form>
      </div>
    </ResponseModal>
  </Page>
</template>
