<script lang="ts" setup>
import type { UploadProps } from 'ant-design-vue';

import type { NoticeApi } from '#/api/portal/notice';

import { computed, nextTick, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { useAccessStore } from '@vben/stores';

import {
  Button,
  Card,
  Checkbox,
  DatePicker,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Popconfirm,
  Space,
  Switch,
  Table,
  Tag,
  Upload,
} from 'ant-design-vue';

import NoticePopup from '#/components/notice/notice-popup.vue';
import { RichEditor } from '#/components/rich-editor';
import {
  createNotice,
  deleteNotice,
  getNoticeList,
  updateNotice,
} from '#/api/portal/notice';

/**
 * [공지 관리]
 *
 * 공지는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
 * 각 MSA 가 자기 공지를 따로 두지 않는다.
 *
 * '전체 공개'를 켠 공지는 로그인하지 않아도 보인다 — 화면이 뜨는 순간 팝업으로 뜬다.
 * 끄면 로그인한 뒤에 뜬다.
 *
 * 첨부파일은 FileServer 에 올리고 여기에는 파일 아이디만 들고 있는다.
 * 본문에 붙여넣은 이미지도 같은 곳에 올라간다 — 본문에는 경로(`<img src="/api/file/...">`)만 남는다.
 *
 * [미리보기]는 사용자에게 실제로 뜨는 공지 팝업(`components/notice/notice-popup.vue`)을
 * 그대로 띄운다. 미리보기용 화면을 따로 만들면 실제와 어긋나기 때문이다.
 * 게시 기간이 지났거나 팝업이 꺼진 공지도 확인할 수 있다 — 어떻게 보일지 미리 보는 것이 목적이다.
 */

const accessStore = useAccessStore();

const loading = ref(false);
const saving = ref(false);
const rows = ref<NoticeApi.Notice[]>([]);
const keyword = ref('');

const modalOpen = ref(false);
const editingId = ref<null | string>(null);

/** 미리보기로 띄운 공지. null 이면 팝업이 닫힌다. */
const previewNotice = ref<NoticeApi.Notice | null>(null);

const form = reactive<{
  content: string;
  endAt: string | undefined;
  isPopup: boolean;
  isPublic: boolean;
  orderNo: number;
  startAt: string | undefined;
  status: number;
  title: string;
}>({
  content: '',
  endAt: undefined,
  isPopup: true,
  isPublic: false,
  orderNo: 0,
  startAt: undefined,
  status: 1,
  title: '',
});

/** 첨부파일 목록. 저장할 때 이 목록 그대로가 최종 상태가 된다. */
const files = ref<NoticeApi.NoticeFile[]>([]);
const uploading = ref(false);

const columns = [
  { dataIndex: 'title', key: 'title', title: '제목', ellipsis: true },
  { key: 'flags', title: '노출', width: 170 },
  { key: 'period', title: '게시 기간', width: 220 },
  { dataIndex: 'orderNo', key: 'orderNo', title: '순서', width: 70 },
  { key: 'files', title: '첨부', width: 70 },
  { key: 'status', title: '상태', width: 90 },
  { key: 'action', title: '작업', width: 190 },
];

/** 업로드는 게이트웨이의 파일 서비스로 바로 보낸다. */
const uploadAction = '/api/file/upload?bizType=notice';
const uploadHeaders = computed(() => ({
  Authorization: accessStore.accessToken
    ? `Bearer ${accessStore.accessToken}`
    : '',
}));

function formatDate(value?: null | string) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function periodLabel(row: NoticeApi.Notice) {
  const s = formatDate(row.startAt);
  const e = formatDate(row.endAt);
  if (!s && !e) return '제한 없음';
  return `${s || '처음'} ~ ${e || '끝없음'}`;
}

function formatSize(bytes: number) {
  if (!bytes) return '';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(unit === 0 ? 0 : 1)}${units[unit]}`;
}

async function loadData() {
  loading.value = true;
  try {
    rows.value = await getNoticeList(keyword.value.trim() || undefined);
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    content: '',
    endAt: undefined,
    isPopup: true,
    isPublic: false,
    orderNo: 0,
    startAt: undefined,
    status: 1,
    title: '',
  });
  files.value = [];
  modalOpen.value = true;
}

function openEdit(row: NoticeApi.Notice) {
  editingId.value = row.id;
  Object.assign(form, {
    content: row.content ?? '',
    endAt: row.endAt ? formatDate(row.endAt) : undefined,
    isPopup: row.isPopup,
    isPublic: row.isPublic,
    orderNo: row.orderNo,
    startAt: row.startAt ? formatDate(row.startAt) : undefined,
    status: row.status,
    title: row.title,
  });
  files.value = [...(row.files ?? [])];
  modalOpen.value = true;
}

/**
 * 업로드 결과에서 파일 정보를 꺼낸다.
 * 파일 서비스는 응답을 `result` 또는 `data` 로 감싸 보내고,
 * 배열로 올 때도 있어 모두 받아 준다.
 */
function pickUploaded(res: any) {
  const raw = res?.result ?? res?.data ?? res;
  if (!raw) return null;
  if (Array.isArray(raw)) return raw[0] ?? null;
  if (Array.isArray(raw.result)) return raw.result[0] ?? null;
  return raw;
}

const handleUpload: UploadProps['onChange'] = (info) => {
  const file = info.file;
  if (!file) return;

  if (file.status === 'uploading') {
    uploading.value = true;
    return;
  }

  if (file.status === 'done') {
    uploading.value = false;
    const res = file.response as any;
    if (res && (res.code === 'S000' || res.success)) {
      const data = pickUploaded(res);
      if (data?.id) {
        files.value.push({
          contentType: data.contentType ?? file.type ?? null,
          downloadUrl: data.downloadUrl || `/api/file/download/id/${data.id}`,
          fileId: String(data.id),
          fileName: data.originalName || file.name,
          fileSize: data.size ?? file.size ?? 0,
          sortNo: files.value.length,
        });
        message.success(`${file.name} 을(를) 첨부했습니다.`);
        return;
      }
    }
    message.error(res?.message || '업로드에 실패했습니다.');
  } else if (file.status === 'error') {
    uploading.value = false;
    message.error('서버와 통신하지 못했습니다.');
  }
};

/** 목록에서만 뗀다. 원본 파일은 지우지 않는다. */
function removeFile(fileId: string) {
  files.value = files.value
    .filter((f) => f.fileId !== fileId)
    .map((f, i) => ({ ...f, sortNo: i }));
}

async function onSave() {
  if (!form.title.trim()) {
    message.warning('제목을 입력하세요.');
    return;
  }

  const payload: NoticeApi.SaveNotice = {
    content: form.content,
    endAt: form.endAt ? `${form.endAt}T23:59:59Z` : null,
    files: files.value.map((f, i) => ({
      contentType: f.contentType,
      fileId: f.fileId,
      fileName: f.fileName,
      fileSize: f.fileSize,
      sortNo: i,
    })),
    isPopup: form.isPopup,
    isPublic: form.isPublic,
    orderNo: form.orderNo,
    startAt: form.startAt ? `${form.startAt}T00:00:00Z` : null,
    status: form.status,
    title: form.title.trim(),
  };

  saving.value = true;
  try {
    await (editingId.value
      ? updateNotice(editingId.value, payload)
      : createNotice(payload));
    message.success(`공지를 ${editingId.value ? '수정' : '등록'}했습니다.`);
    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

/**
 * 사용자에게 보이는 팝업을 그대로 띄운다.
 * 같은 공지를 연달아 눌러도 열리도록 먼저 비운 뒤 넣는다.
 */
function openPreview(row: NoticeApi.Notice) {
  previewNotice.value = null;
  nextTick(() => {
    previewNotice.value = row;
  });
}

async function onDelete(row: NoticeApi.Notice) {
  await deleteNotice(row.id);
  message.success('공지를 삭제했습니다.');
  await loadData();
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Input
            v-model:value="keyword"
            allow-clear
            placeholder="제목 + 본문 검색"
            style="width: 240px"
            @press-enter="loadData"
          />
          <Button :loading="loading" @click="loadData">조회</Button>
        </Space>
        <Button v-perm:create type="primary" @click="openCreate">
          공지 등록
        </Button>
      </div>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :data-source="rows"
        :loading="loading"
        :pagination="{ pageSize: 20, showSizeChanger: true }"
        :scroll="{ x: 900 }"
        row-key="id"
        size="small"
      >
        <template #emptyText>
          <Empty description="등록된 공지가 없습니다." />
        </template>

        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'flags'">
            <Space :size="4" wrap>
              <Tag v-if="record.isPublic" color="blue">전체 공개</Tag>
              <Tag v-else>로그인 후</Tag>
              <Tag v-if="record.isPopup" color="purple">팝업</Tag>
            </Space>
          </template>

          <template v-else-if="column.key === 'period'">
            <span class="text-xs">{{ periodLabel(record as NoticeApi.Notice) }}</span>
          </template>

          <template v-else-if="column.key === 'files'">
            <span class="text-xs">{{ record.files?.length || 0 }}</span>
          </template>

          <template v-else-if="column.key === 'status'">
            <Tag :color="record.status === 1 ? 'success' : 'default'">
              {{ record.status === 1 ? '활성' : '비활성' }}
            </Tag>
          </template>

          <template v-else-if="column.key === 'action'">
            <Space :size="4">
              <Button
                size="small"
                type="link"
                @click="openPreview(record as NoticeApi.Notice)"
              >
                미리보기
              </Button>
              <Button
                v-perm:update
                size="small"
                type="link"
                @click="openEdit(record as NoticeApi.Notice)"
              >
                수정
              </Button>
              <Popconfirm
                v-perm:delete
                cancel-text="취소"
                ok-text="삭제"
                title="공지를 삭제할까요?"
                @confirm="onDelete(record as NoticeApi.Notice)"
              >
                <Button danger size="small" type="link">삭제</Button>
              </Popconfirm>
            </Space>
          </template>
        </template>
      </Table>
    </Card>

    <!-- 등록 · 수정 -->
    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      cancel-text="취소"
      ok-text="저장"
      :title="editingId ? '공지 수정' : '공지 등록'"
      :width="720"
      @ok="onSave"
    >
      <Form layout="vertical">
        <FormItem label="제목" required>
          <Input v-model:value="form.title" :maxlength="200" />
        </FormItem>

        <FormItem
          extra="이미지는 붙여넣기·드래그로 바로 넣을 수 있습니다. 넣는 즉시 파일로 보관되고 본문에는 경로만 들어갑니다."
          label="내용"
        >
          <RichEditor
            v-model="form.content"
            :min-height="300"
            biz-type="notice"
            placeholder="공지 내용을 입력하세요. 이미지는 붙여넣기로 바로 넣을 수 있습니다."
          />
        </FormItem>

        <div class="grid grid-cols-1 gap-x-4 sm:grid-cols-2">
          <FormItem label="게시 시작일">
            <DatePicker
              v-model:value="form.startAt"
              placeholder="비우면 제한 없음"
              style="width: 100%"
              value-format="YYYY-MM-DD"
            />
          </FormItem>
          <FormItem label="게시 종료일">
            <DatePicker
              v-model:value="form.endAt"
              placeholder="비우면 제한 없음"
              style="width: 100%"
              value-format="YYYY-MM-DD"
            />
          </FormItem>
        </div>

        <div class="grid grid-cols-1 gap-x-4 sm:grid-cols-3">
          <FormItem
            extra="켜면 로그인 전에도 보입니다."
            label="전체 공개"
          >
            <Switch v-model:checked="form.isPublic" />
          </FormItem>
          <FormItem extra="끄면 목록에만 남습니다." label="팝업으로 띄우기">
            <Switch v-model:checked="form.isPopup" />
          </FormItem>
          <FormItem label="노출 순서">
            <InputNumber v-model:value="form.orderNo" :min="0" style="width: 100%" />
          </FormItem>
        </div>

        <FormItem>
          <Checkbox
            :checked="form.status === 1"
            @change="(e: any) => (form.status = e.target.checked ? 1 : 0)"
          >
            활성
          </Checkbox>
        </FormItem>

        <!-- 첨부파일 -->
        <FormItem label="첨부파일">
          <Upload
            :action="uploadAction"
            :headers="uploadHeaders"
            :show-upload-list="false"
            name="file"
            @change="handleUpload"
          >
            <Button :loading="uploading">파일 추가</Button>
          </Upload>

          <div
            v-if="files.length > 0"
            class="mt-2 rounded border border-border p-2"
          >
            <div
              v-for="file in files"
              :key="file.fileId"
              class="flex items-center justify-between gap-2 border-b border-border py-1 last:border-b-0"
            >
              <a
                class="min-w-0 flex-1 truncate text-sm text-primary hover:underline"
                :href="file.downloadUrl || `/api/file/download/id/${file.fileId}`"
                rel="noopener"
                target="_blank"
              >
                {{ file.fileName }}
              </a>
              <span class="shrink-0 text-xs text-muted-foreground">
                {{ formatSize(file.fileSize) }}
              </span>
              <Button
                danger
                size="small"
                type="link"
                @click="removeFile(file.fileId)"
              >
                제거
              </Button>
            </div>
          </div>
        </FormItem>
      </Form>
    </Modal>
    <!-- 미리보기 — 사용자에게 실제로 뜨는 그 팝업이다 -->
    <NoticePopup v-model:preview="previewNotice" mode="preview" />
  </Page>
</template>
