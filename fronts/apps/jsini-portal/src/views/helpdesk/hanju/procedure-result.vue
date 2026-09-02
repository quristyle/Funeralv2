<script lang="ts" setup>
import { computed, onMounted, ref, watch } from 'vue';

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
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { executeProcedure } from '#/api/helpdesk';

/**
 * [프로시저 결과]
 *
 * 원본(JinReception hanju/ProcedureResult.vue, `/procedure-result`).
 *
 * 왼쪽에서 프로시저를 고르면 그 프로시저의 파라미터 입력칸이 만들어지고,
 * 실행하면 오른쪽에 결과 표가 뜬다. 날짜처럼 보이는 값은 자동으로 읽기 좋게
 * 바꿔 보여준다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 원본이 머리글 칸마다 직접 그리던 검색칸(`filterDisplay="row"` 대응)과
 * 컬럼 정렬(`sorter`)은 걷어냈다 — 공통 레이어의 필터줄이 같은 일을 한다.
 * 결과 전체를 훑는 '결과 내 검색' 은 컬럼과 무관한 조회 조건이라 남겨 두었다.
 *
 * **가져오기 방식은 그대로다** — 프로시저가 돌려준 전량을 화면이 쥔다.
 * 원본의 프런트 페이징은 없앴다(전역 기본값이 페이저 꺼짐).
 * ------------------------------------------------------------
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

/** 전체 검색을 적용한다. 컬럼별 검색은 표 머리글의 필터줄이 맡는다. */
const filteredRows = computed(() => {
  const global = globalKeyword.value.trim().toLowerCase();
  if (!global) return resultRows.value;

  return resultRows.value.filter((row) =>
    Object.values(row).some((v) =>
      String(v ?? '')
        .toLowerCase()
        .includes(global),
    ),
  );
});

const [Grid, gridApi] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    // 결과 컬럼은 프로시저마다 다르다. 실행한 뒤에 갈아끼운다.
    columns: [],
    // 행 배열은 `:table-data` 로 간다. 여기는 빈 배열이 바탕값이다.
    data: [],
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 이 표를 채우는 것은 '실행'(`run`)이므로 같은 파라미터로 다시 실행한다.
    gridFeatures: { onRefresh: () => run() },
    height: 520,
    // 전량 조회다. 페이저를 끄지 않으면 한 줄도 안 그려진다.
    pagerConfig: { enabled: false },
    // 결과 행에는 고유 키로 쓸 만한 칸이 없다(원본도 순번을 키로 썼다).
    // `keyField` 를 적지 않으면 vxe 가 내부 키를 붙여 준다.
  } as any,
});

/**
 * 결과 컬럼은 첫 행의 키에서 만든다.
 *
 * 보이는 글자와 저장된 값이 다른 칸(날짜)이 섞여 있어 `formatter` 로 그리고,
 * 필터가 훑을 글자도 같은 것으로 맞춰 준다(`params.filterText`).
 */
const resultColumns = computed(() => {
  const first = resultRows.value[0];
  if (!first) return [];

  return Object.keys(first).map((key) => ({
    field: key,
    formatter: ({ cellValue }: any) => cellText(cellValue),
    minWidth: 140,
    params: { filterText: (row: any) => cellText(row[key]) },
    title: key,
  }));
});

watch(resultColumns, (columns) => gridApi.setGridOptions({ columns }));
watch(executing, (value) => gridApi.setLoading(value));

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
            <GridIconButton
              :loading="loading"
              icon="vxe-icon-repeat"
              title="프로시저 목록 새로고침"
              @click="fetchProcedureList"
            />
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

          <Grid :table-data="filteredRows">
            <!-- 빈 화면 안내는 상황마다 다르다. `emptyText` 대신 슬롯으로 그린다. -->
            <template #empty>
              <div class="p-8 text-center text-muted-foreground">
                <p v-if="!selectedProcedure">왼쪽에서 프로시저를 선택해 주세요.</p>
                <p v-else-if="executing">데이터를 불러오는 중입니다...</p>
                <p v-else>실행 버튼을 눌러 결과를 확인하세요.</p>
              </div>
            </template>
          </Grid>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
