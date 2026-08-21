<script lang="ts" setup>
import { computed, onMounted, reactive, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  Input,
  List,
  ListItem,
  message,
  Row,
  Space,
  Spin,
  Table,
} from 'ant-design-vue';

import { executeProcedure } from '#/api/helpdesk';

/**
 * [프로시저 결과]
 *
 * 원본(JinReception hanju/ProcedureResult.vue, `/procedure-result`).
 *
 * 왼쪽에서 프로시저를 고르면 그 프로시저의 파라미터 입력칸이 만들어지고,
 * 실행하면 오른쪽에 결과 표가 뜬다. 결과 표는 컬럼마다 검색과 정렬이 되고,
 * 날짜처럼 보이는 값은 자동으로 읽기 좋게 바꿔 보여준다.
 */

const loading = ref(false);
const executing = ref(false);

/** 프로시저별 파라미터 정의 */
const proceduresMap = ref<Record<string, any[]>>({});
/** 왼쪽 목록에 쓰는 프로시저 */
const procedureItems = ref<{ description: string; name: string }[]>([]);
const selectedProcedure = ref<string | undefined>();
const parameterValues = ref<Record<string, any>>({});

const resultRows = ref<Record<string, any>[]>([]);

/** 프로시저 목록 검색 */
const procedureSearch = ref('');
/** 결과 전체 검색 */
const globalKeyword = ref('');
/** 컬럼별 검색. 원본의 filterDisplay="row" 대응. */
const columnFilters = reactive<Record<string, string>>({});

/**
 * 결과 표의 페이징 상태.
 * pageSize 를 넘기는 순간 Table 은 그 값을 부모가 쥔 값으로 다루기 때문에,
 * 여기에 담아 두고 change 이벤트로 되받아 줘야 페이지당 건수 변경이 유지된다.
 */
const pagination = reactive({
  current: 1,
  pageSize: 50,
  pageSizeOptions: ['20', '50', '100', '200'],
  showSizeChanger: true,
  showTotal: (total: number) => `총 ${total}건`,
  total: 0,
});

/** 페이지 이동·페이지당 건수 변경을 받아 상태에 반영한다. */
function onTableChange(pag: { current?: number; pageSize?: number }) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? pagination.pageSize;
}

/** 선택한 프로시저의 파라미터 정의 */
const parameters = computed(() =>
  selectedProcedure.value
    ? (proceduresMap.value[selectedProcedure.value] ?? [])
    : [],
);

/** 목록 검색을 통과한 프로시저 */
const filteredProcedures = computed(() => {
  const kw = procedureSearch.value.trim().toLowerCase();
  if (!kw) return procedureItems.value;
  return procedureItems.value.filter(
    (p) =>
      p.name.toLowerCase().includes(kw) ||
      p.description.toLowerCase().includes(kw),
  );
});

/** 목록에는 공통 접두어를 떼고 보여준다(원본과 동일). */
function shortName(name: string) {
  return name.replace('P_QURI_', '');
}

/**
 * 값이 날짜 문자열인지 본다. 원본 isDate 와 같은 규칙.
 * 이 경우 표에서 읽기 좋은 형식으로 바꿔 보여준다.
 */
function isDateValue(value: unknown) {
  if (typeof value !== 'string') return false;
  return (
    /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(value) ||
    (/^\d{4}-\d{2}-\d{2}/.test(value) && value.length >= 10)
  );
}

/** 'YYYY-MM-DD HH:mm:ss' 로 맞춘다. */
function formatFull(value: string) {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

/** 셀에 보일 값 */
function cellText(value: unknown) {
  return isDateValue(value) ? formatFull(value as string) : String(value ?? '');
}

/** 결과 컬럼. 정렬은 클라이언트에서 처리한다. */
const resultColumns = computed(() => {
  const first = resultRows.value[0];
  if (!first) return [];

  return Object.keys(first).map((key) => ({
    dataIndex: key,
    ellipsis: true,
    key,
    sorter: (a: any, b: any) => {
      const av = a[key];
      const bv = b[key];
      if (typeof av === 'number' && typeof bv === 'number') return av - bv;
      return String(av ?? '').localeCompare(String(bv ?? ''));
    },
    title: key,
  }));
});

/** 전체 검색 + 컬럼별 검색을 함께 적용한다. */
const filteredRows = computed(() => {
  const global = globalKeyword.value.trim().toLowerCase();
  const active = Object.entries(columnFilters).filter(([, v]) => v?.trim());

  return resultRows.value.filter((row) => {
    if (
      global &&
      !Object.values(row).some((v) =>
        String(v ?? '').toLowerCase().includes(global),
      )
    ) {
      return false;
    }

    return active.every(([key, needle]) =>
      String(row[key] ?? '')
        .toLowerCase()
        .includes(needle.trim().toLowerCase()),
    );
  });
});

// 검색으로 건수가 줄면 보고 있던 페이지가 사라질 수 있어 총 건수와 현재 페이지를 맞춰 준다.
watch(
  filteredRows,
  (list) => {
    pagination.total = list.length;
    const lastPage = Math.max(1, Math.ceil(list.length / pagination.pageSize));
    if (pagination.current > lastPage) pagination.current = lastPage;
  },
  { immediate: true },
);

/** 파라미터 타입에 맞는 입력 컴포넌트를 고른다. */
function inputKind(dataType?: string) {
  const t = String(dataType ?? '').toLowerCase();
  if (t.includes('date')) return 'date';
  return 'text';
}

/** 시각까지 받아야 하는 타입인지. 원본은 'time' 포함 여부로 판단한다. */
function withTime(dataType?: string) {
  return String(dataType ?? '')
    .toLowerCase()
    .includes('time');
}

async function fetchProcedureList() {
  loading.value = true;
  try {
    const rawList =
      (await executeProcedure<Record<string, any>[]>('P_QURI_PROC')) ?? [];

    const map: Record<string, any[]> = {};
    const info: Record<string, string> = {};

    rawList.forEach((item) => {
      const name = item.ProcedureName;
      if (!name) return;
      if (!map[name]) {
        map[name] = [];
        info[name] = item.ProcDescription ?? '';
      }
      if (item.ParameterName) map[name].push(item);
    });

    proceduresMap.value = map;
    procedureItems.value = Object.keys(map)
      .sort()
      .map((name) => ({ description: info[name] ?? '', name }));
  } catch {
    message.error('프로시저 목록을 불러오지 못했습니다.');
  } finally {
    loading.value = false;
  }
}

async function run() {
  if (!selectedProcedure.value) return;

  executing.value = true;
  try {
    const params = Object.entries(parameterValues.value).map(
      ([name, value]) => ({ name, value: String(value ?? '') }),
    );

    resultRows.value =
      (await executeProcedure<Record<string, any>[]>(
        selectedProcedure.value,
        params,
      )) ?? [];

    // 새 결과에는 이전 컬럼 필터를 남기지 않는다.
    Object.keys(columnFilters).forEach((k) => delete columnFilters[k]);
    pagination.current = 1;
    message.success(`${resultRows.value.length}건을 조회했습니다.`);
  } catch (error) {
    resultRows.value = [];
    message.error(
      `실행에 실패했습니다: ${(error as Error).message ?? '알 수 없는 오류'}`,
    );
  } finally {
    executing.value = false;
  }
}

// 프로시저를 바꾸면 입력값과 결과를 비운다(원본과 동일).
watch(selectedProcedure, (name) => {
  parameterValues.value = {};
  if (name) {
    (proceduresMap.value[name] ?? []).forEach((param) => {
      parameterValues.value[param.ParameterName] = undefined;
    });
  }
  resultRows.value = [];
  Object.keys(columnFilters).forEach((k) => delete columnFilters[k]);
});

onMounted(fetchProcedureList);
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="[12, 12]">
      <!-- 왼쪽: 프로시저 목록 + 파라미터 -->
      <Col :lg="6" :xs="24">
        <Card size="small">
          <template #title>프로시저 목록</template>
          <template #extra>
            <Button :loading="loading" size="small" @click="fetchProcedureList">
              새로고침
            </Button>
          </template>

          <Input
            v-model:value="procedureSearch"
            allow-clear
            class="mb-2"
            placeholder="프로시저 검색"
          />

          <Spin :spinning="loading">
            <List
              :data-source="filteredProcedures"
              :locale="{ emptyText: '프로시저가 없습니다.' }"
              size="small"
              style="max-height: 400px; overflow: auto"
            >
              <template #renderItem="{ item }">
                <ListItem
                  class="cursor-pointer px-2"
                  :class="item.name === selectedProcedure ? 'bg-accent' : ''"
                  @click="selectedProcedure = item.name"
                >
                  <div class="flex w-full items-center justify-between gap-2">
                    <span class="text-sm font-semibold">
                      {{ shortName(item.name) }}
                    </span>
                    <span
                      v-if="item.description"
                      class="truncate text-right text-[10px] text-muted-foreground"
                    >
                      {{ item.description }}
                    </span>
                  </div>
                </ListItem>
              </template>
            </List>
          </Spin>

          <!-- 파라미터 입력 -->
          <div v-if="selectedProcedure" class="mt-4">
            <div class="mb-2 flex items-center justify-between">
              <span class="text-sm font-semibold">파라미터 입력</span>
              <Button
                :loading="executing"
                size="small"
                type="primary"
                @click="run"
              >
                실행
              </Button>
            </div>

            <Empty
              v-if="parameters.length === 0"
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
              description="입력할 파라미터가 없습니다."
            />

            <!-- 원본은 파라미터명 / 값 두 열짜리 표로 보여줬다. -->
            <div v-else class="rounded border border-border">
              <div
                v-for="param in parameters"
                :key="param.ParameterName"
                class="flex items-start gap-2 border-b border-border p-2 last:border-b-0"
              >
                <div class="w-2/5 min-w-0">
                  <div class="truncate text-xs font-medium">
                    {{ param.ParameterName }}
                  </div>
                  <div
                    v-if="param.ParamDescription"
                    class="truncate text-[10px] text-primary"
                  >
                    {{ param.ParamDescription }}
                  </div>
                  <div class="text-[10px] text-muted-foreground">
                    {{ param.DataType }}({{ param.MaxLength }})
                  </div>
                </div>

                <div class="min-w-0 flex-1">
                  <DatePicker
                    v-if="inputKind(param.DataType) === 'date'"
                    v-model:value="parameterValues[param.ParameterName]"
                    size="small"
                    :show-time="withTime(param.DataType)"
                    style="width: 100%"
                    :value-format="
                      withTime(param.DataType)
                        ? 'YYYY-MM-DD HH:mm:ss'
                        : 'YYYY-MM-DD'
                    "
                  />
                  <Input
                    v-else
                    v-model:value="parameterValues[param.ParameterName]"
                    :maxlength="param.MaxLength > 0 ? param.MaxLength : undefined"
                    size="small"
                    @press-enter="run"
                  />
                </div>
              </div>
            </div>
          </div>
        </Card>
      </Col>

      <!-- 오른쪽: 실행 결과 -->
      <Col :lg="18" :xs="24">
        <Card :body-style="{ padding: 0 }" size="small">
          <template #title>
            실행 결과
            <span v-if="selectedProcedure" class="text-sm font-normal">
              ({{ selectedProcedure }})
            </span>
          </template>
          <template #extra>
            <Space>
              <span class="text-[11px] text-muted-foreground">
                {{ filteredRows.length }}건
              </span>
              <Input
                v-if="resultColumns.length > 0"
                v-model:value="globalKeyword"
                allow-clear
                placeholder="결과 내 검색"
                size="small"
                style="width: 180px"
              />
            </Space>
          </template>

          <Table
            :columns="resultColumns"
            :data-source="filteredRows"
            :loading="executing"
            :pagination="pagination"
            @change="onTableChange"
            :scroll="{ x: true, y: 520 }"
            :row-key="(_: any, index?: number) => String(index)"
            size="small"
          >
            <!-- 컬럼마다 검색칸. 원본 filterDisplay="row" 와 같은 자리. -->
            <template #headerCell="{ column }">
              <div class="flex flex-col gap-1">
                <span class="whitespace-nowrap">{{ column.title }}</span>
                <Input
                  v-model:value="columnFilters[column.key as string]"
                  allow-clear
                  :placeholder="`${column.title} 검색`"
                  size="small"
                  @click.stop
                />
              </div>
            </template>

            <template #emptyText>
              <div class="p-8 text-center text-muted-foreground">
                <p v-if="!selectedProcedure">왼쪽에서 프로시저를 선택해 주세요.</p>
                <p v-else-if="executing">데이터를 불러오는 중입니다...</p>
                <p v-else>실행 버튼을 눌러 결과를 확인하세요.</p>
              </div>
            </template>

            <template #bodyCell="{ column, record }">
              <span class="whitespace-nowrap text-[11px]">
                {{ cellText(record[column.dataIndex as string]) }}
              </span>
            </template>
          </Table>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
