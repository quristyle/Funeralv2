<script setup lang="ts">
/**
 * [엑셀 시트]
 *
 * 원본: ProjMngWasm `Pages/Proj/LuckysheetPage.razor` (`/luckysheet`).
 * 프로시저: `sp_dev_excel_exec` (`xls_id` 로 문서 한 건을 읽고 쓴다)
 *
 * 원본은 Luckysheet 를 JS interop 으로 띄웠다. 그 라이브러리는 포털에 없고
 * 유지도 끊긴 상태라 그대로 가져오지 않았다.
 *
 * 대신 같은 자료(시트 JSON)를 다루도록 두 갈래를 준다.
 *   · 표 편집  — 메타 구동 그리드로 셀 값을 직접 편집
 *   · 원본 JSON — 저장된 문자열을 그대로 열어 확인·수정
 *
 * 저장 형식은 이식 전과 같은 `cont` 문자열이라 기존 자료가 그대로 열린다.
 */
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert, Button, Input, TabPane, Tabs, message } from 'ant-design-vue';

import { dbCont, dbSave } from '#/api/projmng';

import { CodeEditor, SearchBar } from '../shared';

const PROC = 'sp_dev_excel_exec';

const sheetId = ref('1');
const raw = ref('');
const loading = ref(false);
const tab = ref('json');

/** 시트를 격자로 본 것. `[[셀, 셀], ...]` 형태만 다룬다. */
const grid = ref<string[][]>([]);

function toGrid(text: string): string[][] {
  try {
    const parsed = JSON.parse(text);
    if (Array.isArray(parsed) && Array.isArray(parsed[0])) {
      return parsed.map((row: unknown[]) => row.map((cell) => String(cell ?? '')));
    }
  } catch {
    // JSON 이 아니거나 격자 모양이 아니면 표 편집은 비워 둔다.
  }
  return [];
}

async function load() {
  loading.value = true;
  try {
    const result = await dbCont(PROC, { xls_id: sheetId.value });
    raw.value = String(result.data?.[0]?.cont ?? '');
    grid.value = toGrid(raw.value);
  } finally {
    loading.value = false;
  }
}

async function save() {
  // 표 편집 탭에서 고친 것이 있으면 그것을 정본으로 삼는다.
  const payload =
    tab.value === 'grid' && grid.value.length > 0
      ? JSON.stringify(grid.value)
      : raw.value;

  const saved = await dbSave(
    PROC,
    { xls_id: sheetId.value, cont: payload },
    [{ xls_id: sheetId.value, cont: payload, quri_ischange: true }],
  );
  if (saved.code >= 0) {
    raw.value = payload;
    message.success('저장했습니다.');
  }
}

/** 셀 하나를 고친다. 중첩 배열을 v-model 로 직접 묶으면 타입이 흔들려 함수로 뺐다. */
function setCell(row: number, col: number, value: string) {
  const line = grid.value[row];
  if (line) line[col] = value;
}

function addRow() {
  const width = grid.value[0]?.length ?? 5;
  grid.value.push(Array.from({ length: width }, () => ''));
}

onMounted(load);
</script>

<template>
  <Page auto-content-height>
    <SearchBar class="mb-2">
      <Input
        v-model:value="sheetId"
        placeholder="문서 ID"
        size="small"
        style="width: 120px"
        @press-enter="load"
      />
      <template #actions>
        <Button v-perm:search size="small" :loading="loading" @click="load">
          불러오기
        </Button>
        <Button v-perm:update size="small" type="primary" @click="save">
          저장
        </Button>
      </template>
    </SearchBar>

    <Alert
      class="mb-2"
      message="원본의 Luckysheet 편집기는 이식하지 않았습니다. 같은 자료를 표 또는 JSON 으로 편집합니다."
      show-icon
      type="info"
    />

    <Tabs v-model:activeKey="tab" size="small">
      <TabPane key="json" tab="원본 JSON">
        <CodeEditor v-model="raw" height="440" language="json" />
      </TabPane>

      <TabPane key="grid" tab="표 편집">
        <div v-if="grid.length === 0" class="text-muted-foreground p-4 text-sm">
          이 문서는 격자 형태가 아니라 표로 열 수 없습니다. 원본 JSON 탭에서 편집하세요.
        </div>
        <div v-else class="overflow-auto">
          <table class="border-border w-full border text-xs">
            <tbody>
              <tr v-for="(row, r) in grid" :key="r">
                <td
                  v-for="(cell, c) in row"
                  :key="c"
                  class="border-border border p-0"
                >
                  <input
                    :value="cell"
                    class="w-full bg-transparent px-1 py-0.5 outline-none"
                    @input="setCell(r, c, ($event.target as HTMLInputElement).value)"
                  />
                </td>
              </tr>
            </tbody>
          </table>
          <Button class="mt-2" size="small" @click="addRow">행 추가</Button>
        </div>
      </TabPane>
    </Tabs>
  </Page>
</template>
