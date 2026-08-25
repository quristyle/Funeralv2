<script lang="ts" setup>
import type { FaqApi } from '#/api/portal/faq';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  AutoComplete,
  Button,
  Card,
  Checkbox,
  Collapse,
  CollapsePanel,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Popconfirm,
  Segmented,
  Space,
  Spin,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  createFaq,
  deleteFaq,
  getFaqList,
  updateFaq,
} from '#/api/portal/faq';
import { RichEditor } from '#/components/rich-editor';

/**
 * [F.A.Q]
 *
 * 자주 묻는 질문. 관리자가 쓰고 나머지 사용자는 읽는다.
 *
 * F.A.Q 는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
 * 각 MSA 가 자기 F.A.Q 를 따로 두지 않는다(공지와 같은 방침).
 *
 * [관리자 판정은 서버가 한다]
 * 화면이 권한 스토어만 보고 판단하면, 권한이 늦게 도착했을 때 서버와 어긋난 버튼이 보인다.
 * 그래서 목록 응답의 `canManage` 를 그대로 쓴다. 저장 요청도 서버가 다시 확인하므로
 * 화면이 틀려도 데이터가 상하지 않는다.
 *
 * [화면 구성]
 * 세로 스크롤을 만들지 않으려고(준수사항 4) 조회 줄은 위에 고정하고
 * 목록만 안에서 스크롤한다. 답변은 질문을 눌러 펼친다.
 */

const ALL = '전체';
/** 분류를 비워 둔 항목을 묶어 보여줄 이름 */
const ETC = '기타';

const loading = ref(false);
const saving = ref(false);

const items = ref<FaqApi.Faq[]>([]);
const categories = ref<string[]>([]);
const canManage = ref(false);

const keyword = ref('');
const activeCategory = ref<string>(ALL);
/** 펼쳐 둔 질문. 여러 개를 함께 열어 볼 수 있다. */
const openKeys = ref<string[]>([]);

const modalOpen = ref(false);
const editingId = ref<null | string>(null);

const form = reactive<{
  answer: string;
  category: string;
  orderNo: number;
  question: string;
  status: number;
}>({
  answer: '',
  category: '',
  orderNo: 0,
  question: '',
  status: 1,
});

/** 분류 고르개. 등록된 분류만 만든다. */
const categoryOptions = computed(() => {
  const found = new Set<string>();
  items.value.forEach((item) => found.add(item.category?.trim() || ETC));
  return [ALL, ...[...found].sort()];
});

/** 화면에 보일 목록. 분류는 화면에서 거른다 — 고르개를 누를 때마다 다시 부르지 않는다. */
const visibleItems = computed(() => {
  if (activeCategory.value === ALL) return items.value;
  return items.value.filter(
    (item) => (item.category?.trim() || ETC) === activeCategory.value,
  );
});

/** 분류별로 묶는다. 같은 분류가 흩어져 보이지 않게 한다. */
const grouped = computed(() => {
  const map = new Map<string, FaqApi.Faq[]>();
  visibleItems.value.forEach((item) => {
    const key = item.category?.trim() || ETC;
    const list = map.get(key);
    if (list) list.push(item);
    else map.set(key, [item]);
  });
  return [...map.entries()].sort(([a], [b]) => a.localeCompare(b));
});

/** 분류 추천 목록. 새 분류도 그냥 적어 넣을 수 있다. */
const categorySuggestions = computed(() =>
  categories.value.map((value) => ({ value })),
);

async function loadData() {
  loading.value = true;
  try {
    const res = await getFaqList({
      keyword: keyword.value.trim() || undefined,
    });
    items.value = res.items ?? [];
    categories.value = res.categories ?? [];
    canManage.value = res.canManage;

    // 검색으로 걸러 낸 뒤 고르개에 없는 분류가 남아 있으면 전체로 되돌린다.
    if (!categoryOptions.value.includes(activeCategory.value)) {
      activeCategory.value = ALL;
    }

    // 검색해서 몇 건만 남았으면 굳이 누르게 하지 않고 펼쳐 준다.
    openKeys.value =
      keyword.value.trim() && items.value.length <= 5
        ? items.value.map((item) => item.id)
        : [];
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    answer: '',
    // 지금 보고 있는 분류를 채워 둔다. 이어서 여러 건 넣을 때 손이 덜 간다.
    category: activeCategory.value === ALL || activeCategory.value === ETC
      ? ''
      : activeCategory.value,
    orderNo: 0,
    question: '',
    status: 1,
  });
  modalOpen.value = true;
}

function openEdit(item: FaqApi.Faq) {
  editingId.value = item.id;
  Object.assign(form, {
    answer: item.answer ?? '',
    category: item.category ?? '',
    orderNo: item.orderNo,
    question: item.question,
    status: item.status,
  });
  modalOpen.value = true;
}

async function onSave() {
  if (!form.question.trim()) {
    message.warning('질문을 입력하세요.');
    return;
  }

  const payload: FaqApi.SaveFaq = {
    answer: form.answer,
    category: form.category.trim() || null,
    orderNo: form.orderNo,
    question: form.question.trim(),
    status: form.status,
  };

  saving.value = true;
  try {
    await (editingId.value
      ? updateFaq(editingId.value, payload)
      : createFaq(payload));
    message.success(`F.A.Q 를 ${editingId.value ? '수정' : '등록'}했습니다.`);
    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function onDelete(item: FaqApi.Faq) {
  await deleteFaq(item.id);
  message.success('F.A.Q 를 삭제했습니다.');
  await loadData();
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3">
      <!-- 조회 줄. 여기는 고정하고 목록만 안에서 스크롤한다. -->
      <Card class="shrink-0" size="small">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <Space wrap>
            <Input
              v-model:value="keyword"
              allow-clear
              placeholder="질문 + 답변 검색"
              style="width: 240px"
              @press-enter="loadData"
            />
            <Button :loading="loading" @click="loadData">조회</Button>
            <Segmented
              v-if="categoryOptions.length > 1"
              v-model:value="activeCategory"
              :options="categoryOptions"
            />
          </Space>

          <Button v-if="canManage" type="primary" @click="openCreate">
            F.A.Q 등록
          </Button>
        </div>
      </Card>

      <!-- 목록 -->
      <Card
        :body-style="{ padding: 0, height: '100%', overflow: 'hidden' }"
        class="min-h-0 flex-1"
        size="small"
      >
        <!--
          Spin 으로 감싸지 않는다. antd 가 안쪽에 감싸개를 하나 더 만들어서
          높이 사슬(h-full)이 그 자리에서 끊긴다. 대신 겹쳐 띄운다.
        -->
        <div class="relative h-full">
          <div
            v-if="loading"
            class="absolute inset-0 z-10 flex items-center justify-center bg-background/60"
          >
            <Spin />
          </div>

          <div class="h-full overflow-auto p-3">
            <Empty
              v-if="grouped.length === 0"
              class="py-16"
              :description="
                keyword
                  ? '검색 결과가 없습니다.'
                  : '등록된 F.A.Q 가 없습니다.'
              "
            />

            <div v-for="[category, list] in grouped" :key="category" class="mb-4">
              <div
                class="mb-2 flex items-center gap-2 text-sm font-semibold text-foreground"
              >
                <IconifyIcon class="size-4" icon="lucide:folder" />
                {{ category }}
                <span class="text-xs font-normal text-muted-foreground">
                  {{ list.length }}건
                </span>
              </div>

              <Collapse v-model:active-key="openKeys" ghost>
                <CollapsePanel
                  v-for="item in list"
                  :key="item.id"
                  class="rounded border border-border !mb-2"
                >
                  <template #header>
                    <div class="flex min-w-0 items-center gap-2">
                      <span class="shrink-0 font-bold text-primary">Q</span>
                      <span class="min-w-0 flex-1 truncate">
                        {{ item.question }}
                      </span>
                      <!-- 비활성은 관리자에게만 내려온다. 왜 안 보이는지 알 수 있게 표시한다. -->
                      <Tag v-if="item.status !== 1" color="default">중지</Tag>
                    </div>
                  </template>

                  <!-- 관리 버튼. 헤더 안에 두면 펼치기와 눌림이 겹친다. -->
                  <template v-if="canManage" #extra>
                    <div class="flex items-center gap-1" @click.stop>
                      <Tooltip title="수정">
                        <Button size="small" type="link" @click="openEdit(item)">
                          <template #icon>
                            <IconifyIcon class="size-4" icon="lucide:edit" />
                          </template>
                        </Button>
                      </Tooltip>
                      <Popconfirm
                        cancel-text="취소"
                        ok-text="삭제"
                        title="이 F.A.Q 를 삭제할까요?"
                        @confirm="onDelete(item)"
                      >
                        <Tooltip title="삭제">
                          <Button danger size="small" type="link">
                            <template #icon>
                              <IconifyIcon class="size-4" icon="lucide:trash-2" />
                            </template>
                          </Button>
                        </Tooltip>
                      </Popconfirm>
                    </div>
                  </template>

                  <div class="flex gap-2">
                    <span class="shrink-0 font-bold text-green-600">A</span>
                    <!-- eslint-disable-next-line vue/no-v-html -->
                    <div
                      class="faq-answer min-w-0 flex-1 text-sm"
                      v-html="
                        item.answer ||
                        '<p class=&quot;text-muted-foreground&quot;>답변이 준비 중입니다.</p>'
                      "
                    ></div>
                  </div>
                </CollapsePanel>
              </Collapse>
            </div>
          </div>
        </div>
      </Card>
    </div>

    <!-- 등록 · 수정 (관리자) -->
    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="editingId ? 'F.A.Q 수정' : 'F.A.Q 등록'"
      :width="720"
      cancel-text="취소"
      ok-text="저장"
      @ok="onSave"
    >
      <Form layout="vertical">
        <div class="grid grid-cols-1 gap-x-4 sm:grid-cols-3">
          <FormItem
            class="sm:col-span-2"
            extra="새 분류는 그냥 적어 넣으면 됩니다. 비우면 '기타' 로 묶입니다."
            label="분류"
          >
            <AutoComplete
              v-model:value="form.category"
              :options="categorySuggestions"
              allow-clear
              placeholder="예: 결제 · 계정 · 사용법"
            />
          </FormItem>
          <FormItem label="노출 순서">
            <InputNumber v-model:value="form.orderNo" :min="0" style="width: 100%" />
          </FormItem>
        </div>

        <FormItem label="질문" required>
          <Input v-model:value="form.question" :maxlength="300" />
        </FormItem>

        <FormItem
          extra="이미지는 붙여넣기·드래그로 바로 넣을 수 있습니다. 영상은 도구 모음의 ▶ 버튼으로 YouTube 주소나 삽입 코드를 넣습니다. HTML 을 직접 손보려면 오른쪽 끝 [HTML] 을 누르세요."
          label="답변"
        >
          <RichEditor
            v-model="form.answer"
            allow-video
            :min-height="260"
            biz-type="faq"
            html-source
            placeholder="답변을 입력하세요."
          />
        </FormItem>

        <FormItem>
          <Checkbox
            :checked="form.status === 1"
            @change="(e: any) => (form.status = e.target.checked ? 1 : 0)"
          >
            활성 (끄면 관리자에게만 보입니다)
          </Checkbox>
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
/*
  답변 본문은 편집기(tiptap)가 만든 HTML 이다. 목록 안에서도 편집기에서 본 모양과
  비슷하게 보이도록 기본 여백만 되살린다. Tailwind 의 초기화가 이것들을 지운다.
*/
.faq-answer :deep(p) {
  margin: 0 0 0.5em;
}

.faq-answer :deep(p:last-child) {
  margin-bottom: 0;
}

.faq-answer :deep(ul),
.faq-answer :deep(ol) {
  margin: 0 0 0.5em;
  padding-left: 1.5em;
  list-style: revert;
}

.faq-answer :deep(img) {
  max-width: 100%;
  height: auto;
}

/*
  영상. 넣은 사람이 적은 크기를 그대로 살리되 목록 폭을 넘지 않게 한다.
  YouTube 삽입 코드는 보통 width 816 로 오는데, 목록이 그보다 좁을 때
  가로 스크롤이 생기면 준수사항 4 를 어긴다.
*/
.faq-answer :deep(iframe) {
  display: block;
  max-width: 100%;
  margin: 0.5em 0;
  border: 0;
}

.faq-answer :deep(a) {
  color: hsl(var(--primary));
  text-decoration: underline;
}

.faq-answer :deep(blockquote) {
  margin: 0 0 0.5em;
  padding-left: 0.75em;
  border-left: 3px solid hsl(var(--border));
  color: hsl(var(--muted-foreground));
}

.faq-answer :deep(pre) {
  padding: 0.5em 0.75em;
  overflow-x: auto;
  background: hsl(var(--muted));
  border-radius: 4px;
}

.faq-answer :deep(table) {
  border-collapse: collapse;
}

.faq-answer :deep(td),
.faq-answer :deep(th) {
  padding: 0.25em 0.5em;
  border: 1px solid hsl(var(--border));
}
</style>
