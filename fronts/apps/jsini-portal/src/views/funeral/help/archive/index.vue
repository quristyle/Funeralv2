<script lang="ts" setup>
import type { UploadProps } from 'ant-design-vue';

import type { HelpArchiveApi } from '#/api/portal/help-archive';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { useAccessStore } from '@vben/stores';
import { formatDateTime } from '@vben/utils';

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
  Upload,
} from 'ant-design-vue';

import {
  createArchive,
  deleteArchive,
  getArchiveList,
  updateArchive,
} from '#/api/portal/help-archive';
import { RichEditor } from '#/components/rich-editor';

/**
 * [자료실]
 *
 * 자료의 **설명을 확인하고 내려받는** 화면이다.
 * 관리자가 자료를 올리고 나머지 사용자는 읽고 내려받는다.
 *
 * 자료실은 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다
 * (공지 · F.A.Q 와 같은 방침).
 *
 * [관리자 판정은 서버가 한다]
 * 화면이 권한 스토어만 보고 판단하면, 권한이 늦게 도착했을 때 서버와 어긋난 버튼이 보인다.
 * 그래서 목록 응답의 `canManage` 를 그대로 쓴다. 저장 요청도 서버가 다시 확인하므로
 * 화면이 틀려도 데이터가 상하지 않는다.
 *
 * [내려받기]
 * 파일 주소는 FileServer 가 아니라 AuthServer 를 한 번 거친다(`downloadUrl`).
 * 거기서 다운로드 수를 세고 FileServer 로 302 로 넘긴다 —
 * 브라우저가 FileServer 를 직접 열면 셀 수가 없다.
 *
 * [화면 구성]
 * 세로 스크롤을 만들지 않으려고(준수사항 4) 조회 줄은 위에 고정하고 목록만 안에서
 * 스크롤한다. 자료명을 누르면 설명과 파일 목록이 펼쳐진다 — F.A.Q 와 같은 모양이라
 * 도움말 메뉴 안에서 조작이 일관된다.
 */

const ALL = '전체';
/** 분류를 비워 둔 항목을 묶어 보여줄 이름 */
const ETC = '기타';

const accessStore = useAccessStore();

const loading = ref(false);
const items = ref<HelpArchiveApi.Archive[]>([]);
const categories = ref<string[]>([]);
const canManage = ref(false);
const activeKeys = ref<string[]>([]);

const keyword = ref('');
const activeCategory = ref<string>(ALL);

/** 분류 탭. 등록된 분류가 없으면 탭 자체를 감춘다. */
const categoryTabs = computed(() => [ALL, ...categories.value]);

const filtered = computed(() => {
  if (activeCategory.value === ALL) return items.value;
  return items.value.filter(
    (a) => (a.category || ETC) === activeCategory.value,
  );
});

/** 바이트를 사람이 읽는 크기로. 무엇을 내려받는지 미리 알 수 있어야 한다. */
function formatSize(bytes?: null | number) {
  if (!bytes || bytes <= 0) return '-';
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${Math.round(kb)} KB`;
  const mb = kb / 1024;
  if (mb < 1024) return `${mb.toFixed(1)} MB`;
  return `${(mb / 1024).toFixed(1)} GB`;
}

/** 확장자로 아이콘을 고른다. 목록에서 종류를 한눈에 알아보게 한다. */
function fileIcon(fileName: string) {
  const ext = fileName.split('.').pop()?.toLowerCase() ?? '';
  if (['7z', 'gz', 'rar', 'tar', 'zip'].includes(ext)) {
    return 'lucide:file-archive';
  }
  if (['pdf'].includes(ext)) return 'lucide:file-text';
  if (['csv', 'xls', 'xlsx'].includes(ext)) return 'lucide:file-spreadsheet';
  if (['doc', 'docx', 'hwp', 'hwpx'].includes(ext)) return 'lucide:file-type';
  if (['bmp', 'gif', 'jpeg', 'jpg', 'png', 'svg', 'webp'].includes(ext)) {
    return 'lucide:file-image';
  }
  if (['avi', 'mkv', 'mov', 'mp4', 'webm'].includes(ext)) {
    return 'lucide:file-video';
  }
  if (['exe', 'msi'].includes(ext)) return 'lucide:package';
  return 'lucide:file';
}

/**
 * 목록을 못 불러왔는지.
 *
 * 실패했는데도 "등록된 자료가 없습니다" 라고 하면 **거짓말이 된다** —
 * 자료가 없는 것과 못 불러온 것은 다르다. 사용자가 관리자에게
 * "자료실이 비었다" 고 말하게 되는 상황을 만들지 않는다.
 */
const loadFailed = ref(false);

async function load() {
  loading.value = true;
  try {
    const data = await getArchiveList({
      category: activeCategory.value === ALL ? undefined : activeCategory.value,
      keyword: keyword.value.trim() || undefined,
    });
    items.value = data.items ?? [];
    categories.value = data.categories ?? [];
    canManage.value = !!data.canManage;
    loadFailed.value = false;
  } catch {
    // 오류 메시지는 요청 클라이언트의 공통 처리가 띄운다.
    // 여기서는 화면이 잘못된 안내를 하지 않도록 표시만 남긴다.
    items.value = [];
    loadFailed.value = true;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  load();
}

function handleCategoryChange(value: any) {
  activeCategory.value = String(value);
  load();
}

// ── 등록·수정 (관리자) ────────────────────────────────────

const editorOpen = ref(false);
const editorSaving = ref(false);
const editingId = ref<null | string>(null);
const uploading = ref(false);

const form = reactive<{
  category: string;
  description: string;
  files: HelpArchiveApi.SaveArchiveFile[];
  orderNo: number;
  status: number;
  title: string;
}>({
  category: '',
  description: '',
  files: [],
  orderNo: 0,
  status: 1,
  title: '',
});

/** 업로드는 게이트웨이의 파일 서비스로 바로 보낸다(공지 첨부와 같은 방식). */
const uploadAction = '/api/file/upload?bizType=help-archive';
const uploadHeaders = computed(() => ({
  Authorization: accessStore.accessToken
    ? `Bearer ${accessStore.accessToken}`
    : '',
}));

const categoryOptions = computed(() =>
  categories.value.map((c) => ({ value: c })),
);

function openCreate() {
  editingId.value = null;
  form.category = '';
  form.title = '';
  form.description = '';
  form.orderNo = 0;
  form.status = 1;
  form.files = [];
  editorOpen.value = true;
}

function openEdit(archive: HelpArchiveApi.Archive) {
  editingId.value = archive.id;
  form.category = archive.category ?? '';
  form.title = archive.title;
  form.description = archive.description ?? '';
  form.orderNo = archive.orderNo;
  form.status = archive.status;
  // 이미 붙어 있는 파일은 fileId 만 다시 보내면 서버가 그대로 유지한다.
  form.files = archive.files.map((f, i) => ({
    contentType: f.contentType ?? null,
    fileId: f.fileId,
    fileName: f.fileName,
    fileSize: f.fileSize,
    sortNo: i,
  }));
  editorOpen.value = true;
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
        form.files.push({
          contentType: data.contentType ?? file.type ?? null,
          fileId: String(data.id),
          fileName: data.originalName || file.name,
          fileSize: data.size ?? file.size ?? 0,
          sortNo: form.files.length,
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

/** 목록에서만 뗀다. FileServer 의 원본 파일은 지우지 않는다. */
function removeFormFile(fileId: string) {
  form.files = form.files
    .filter((f) => f.fileId !== fileId)
    .map((f, i) => ({ ...f, sortNo: i }));
}

async function handleSave() {
  if (!form.title.trim()) {
    message.warning('자료명을 입력해주세요.');
    return;
  }

  const payload: HelpArchiveApi.SaveArchive = {
    category: form.category.trim() || null,
    description: form.description || null,
    files: form.files,
    orderNo: form.orderNo,
    status: form.status,
    title: form.title.trim(),
  };

  editorSaving.value = true;
  try {
    await (editingId.value
      ? updateArchive(editingId.value, payload)
      : createArchive(payload));
    message.success(editingId.value ? '자료를 수정했습니다.' : '자료를 등록했습니다.');
    editorOpen.value = false;
    await load();
  } catch {
    // 실패 메시지는 요청 클라이언트의 공통 오류 처리가 띄운다.
  } finally {
    editorSaving.value = false;
  }
}

async function handleDelete(id: string) {
  try {
    await deleteArchive(id);
    message.success('자료를 삭제했습니다.');
    await load();
  } catch {
    // 위와 같다.
  }
}

onMounted(load);
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3">
      <!-- 조회 줄 — 위에 고정한다. 목록만 안에서 스크롤한다. -->
      <Card :body-style="{ padding: '12px 16px' }">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <Space :size="8" wrap>
            <Input
              v-model:value="keyword"
              allow-clear
              class="w-64"
              placeholder="자료명 · 설명 · 파일명 검색"
              @press-enter="handleSearch"
            />
            <Button type="primary" @click="handleSearch">
              <IconifyIcon class="mr-1" icon="lucide:search" />
              조회
            </Button>
          </Space>

          <Button v-if="canManage" type="primary" @click="openCreate">
            <IconifyIcon class="mr-1" icon="lucide:plus" />
            자료 등록
          </Button>
        </div>

        <!-- 분류 탭. 등록된 분류가 없으면 감춘다. -->
        <div v-if="categories.length > 0" class="mt-3">
          <Segmented
            :options="categoryTabs"
            :value="activeCategory"
            @change="handleCategoryChange"
          />
        </div>
      </Card>

      <!-- 목록 -->
      <Card class="min-h-0 flex-1" :body-style="{ padding: '0', height: '100%' }">
        <Spin :spinning="loading" wrapper-class-name="h-full">
          <div class="h-full overflow-auto p-3">
            <Empty
              v-if="filtered.length === 0"
              class="py-16"
              :description="
                loadFailed
                  ? '자료 목록을 불러오지 못했습니다. 잠시 후 다시 조회해주세요.'
                  : keyword
                    ? '조건에 맞는 자료가 없습니다.'
                    : '등록된 자료가 없습니다.'
              "
            >
              <Button v-if="loadFailed" @click="load">다시 조회</Button>
            </Empty>

            <Collapse v-else v-model:active-key="activeKeys" ghost>
              <CollapsePanel
                v-for="archive in filtered"
                :key="archive.id"
                class="mb-2 rounded-lg border"
              >
                <template #header>
                  <div class="flex flex-wrap items-center gap-2">
                    <Tag v-if="archive.category" color="blue">
                      {{ archive.category }}
                    </Tag>
                    <Tag v-else>{{ ETC }}</Tag>

                    <span class="font-medium">{{ archive.title }}</span>

                    <!-- 파일 개수와 다운로드 수는 펼치지 않아도 보이게 둔다. -->
                    <Tag v-if="archive.files.length > 0" color="default">
                      파일 {{ archive.files.length }}개
                    </Tag>
                    <span class="text-muted-foreground text-xs">
                      내려받기 {{ archive.downloadCount.toLocaleString() }}회
                    </span>
                    <Tag v-if="archive.status !== 1" color="orange">비활성</Tag>
                  </div>
                </template>

                <!-- 관리자 버튼은 헤더 오른쪽에 둔다 (펼치기와 겹치지 않게 stop) -->
                <template v-if="canManage" #extra>
                  <Space :size="4" @click.stop>
                    <Tooltip title="수정">
                      <Button size="small" type="text" @click.stop="openEdit(archive)">
                        <IconifyIcon icon="lucide:pencil" />
                      </Button>
                    </Tooltip>
                    <Popconfirm
                      cancel-text="취소"
                      ok-text="삭제"
                      title="이 자료를 삭제하시겠습니까?"
                      @confirm="handleDelete(archive.id)"
                    >
                      <Button danger size="small" type="text" @click.stop>
                        <IconifyIcon icon="lucide:trash-2" />
                      </Button>
                    </Popconfirm>
                  </Space>
                </template>

                <!-- 자료 설명 -->
                <div
                  v-if="archive.description"
                  class="prose prose-sm dark:prose-invert max-w-none"
                  v-html="archive.description"
                ></div>
                <p v-else class="text-muted-foreground text-sm">
                  등록된 설명이 없습니다.
                </p>

                <!-- 첨부파일 — 내려받기 -->
                <div v-if="archive.files.length > 0" class="mt-3 border-t pt-3">
                  <div class="text-muted-foreground mb-2 text-xs font-medium">
                    첨부파일
                  </div>
                  <div class="flex flex-col gap-1.5">
                    <!--
                      `downloadUrl` 은 AuthServer 를 거치는 주소다. 거기서 다운로드 수를
                      세고 FileServer 로 302 로 넘긴다. `download` 속성을 주면 브라우저가
                      화면을 떠나지 않고 파일로 받는다.
                    -->
                    <a
                      v-for="file in archive.files"
                      :key="file.id"
                      class="hover:bg-accent flex items-center gap-2 rounded border px-3 py-2 text-sm transition-colors"
                      :download="file.fileName"
                      :href="file.downloadUrl"
                    >
                      <IconifyIcon
                        class="text-muted-foreground size-4 shrink-0"
                        :icon="fileIcon(file.fileName)"
                      />
                      <span class="truncate">{{ file.fileName }}</span>
                      <span class="text-muted-foreground shrink-0 text-xs">
                        {{ formatSize(file.fileSize) }}
                      </span>
                      <span
                        class="text-muted-foreground ml-auto shrink-0 text-xs"
                      >
                        {{ file.downloadCount.toLocaleString() }}회
                      </span>
                      <IconifyIcon
                        class="size-4 shrink-0 text-primary"
                        icon="lucide:download"
                      />
                    </a>
                  </div>
                </div>
                <p v-else class="text-muted-foreground mt-3 text-sm">
                  첨부된 파일이 없습니다.
                </p>

                <div class="text-muted-foreground mt-3 text-xs">
                  등록 {{ formatDateTime(archive.createdAt) }}
                  <template v-if="archive.updatedAt">
                    · 수정 {{ formatDateTime(archive.updatedAt) }}
                  </template>
                </div>
              </CollapsePanel>
            </Collapse>
          </div>
        </Spin>
      </Card>
    </div>

    <!-- 등록·수정 (관리자) -->
    <Modal
      v-model:open="editorOpen"
      :confirm-loading="editorSaving"
      :title="editingId ? '자료 수정' : '자료 등록'"
      ok-text="저장"
      cancel-text="취소"
      width="720px"
      @ok="handleSave"
    >
      <Form layout="vertical">
        <FormItem label="분류">
          <AutoComplete
            v-model:value="form.category"
            allow-clear
            :options="categoryOptions"
            placeholder="예: 설치 파일 · 설명서 (비우면 '기타')"
          />
        </FormItem>

        <FormItem label="자료명" required>
          <Input v-model:value="form.title" placeholder="자료명을 입력해주세요" />
        </FormItem>

        <FormItem label="설명">
          <RichEditor v-model="form.description" biz-type="help-archive" />
        </FormItem>

        <FormItem label="첨부파일">
          <Upload
            :action="uploadAction"
            :headers="uploadHeaders"
            multiple
            name="file"
            :show-upload-list="false"
            @change="handleUpload"
          >
            <Button :loading="uploading">
              <IconifyIcon class="mr-1" icon="lucide:upload" />
              파일 선택
            </Button>
          </Upload>

          <div v-if="form.files.length > 0" class="mt-2 flex flex-col gap-1.5">
            <div
              v-for="file in form.files"
              :key="file.fileId"
              class="flex items-center gap-2 rounded border px-3 py-2 text-sm"
            >
              <IconifyIcon
                class="text-muted-foreground size-4 shrink-0"
                :icon="fileIcon(file.fileName)"
              />
              <span class="truncate">{{ file.fileName }}</span>
              <span class="text-muted-foreground shrink-0 text-xs">
                {{ formatSize(file.fileSize) }}
              </span>
              <Button
                class="ml-auto shrink-0"
                danger
                size="small"
                type="text"
                @click="removeFormFile(file.fileId)"
              >
                <IconifyIcon icon="lucide:x" />
              </Button>
            </div>
          </div>
        </FormItem>

        <div class="flex items-center gap-6">
          <FormItem class="mb-0" label="노출 순서">
            <InputNumber v-model:value="form.orderNo" :min="0" />
          </FormItem>
          <FormItem class="mb-0" label="사용">
            <Checkbox
              :checked="form.status === 1"
              @change="(e: any) => (form.status = e.target.checked ? 1 : 0)"
            >
              활성 (끄면 관리자에게만 보입니다)
            </Checkbox>
          </FormItem>
        </div>
      </Form>
    </Modal>
  </Page>
</template>
