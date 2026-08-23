<script lang="ts" setup>
import type { Attachment } from '#/api/helpdesk';

import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Form,
  FormItem,
  Input,
  message,
  Select,
  Space,
  Spin,
  Tag,
  Upload,
} from 'ant-design-vue';

import {
  createRequestWithFiles,
  getCustomerByLoginId,
  getRequest,
  updateRequestWithFiles,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { REQUEST_TYPES, REQUEST_TYPE_OPTIONS } from '../shared/constants';
import { RichEditor } from '#/components/rich-editor';

/**
 * [요청 등록 · 수정]
 *
 * 원본의 Request.vue(`/request`, 등록)와 RequestEdit.vue(`/request/edit/:id`, 수정)를 한 화면으로 합쳤다.
 * 라우트에 id 가 있으면 수정, 없으면 신규 등록이다.
 *
 * 원본과 같은 동작:
 *  - Ctrl+S 로 화면 이동 없이 저장
 *  - 관리자가 등록할 때는 회사를 반드시 고르고, 그 회사의 공용 계정(`pub_{회사ID}`)을 작성자로 넣는다
 *  - `?recheckId=` 로 들어오면 기존 요청을 '[재확인]' 으로 복제한다
 */

const route = useRoute();
const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const saving = ref(false);

const requestId = computed(() =>
  route.params.id ? Number(route.params.id) : undefined,
);
const isEdit = computed(() => requestId.value !== undefined);

const form = reactive({
  companyId: undefined as number | undefined,
  description: '',
  ipType: 'Improvement' as string,
  title: '',
});

/** 새로 올릴 파일 */
const filesToUpload = ref<File[]>([]);
/** 서버에 이미 붙어 있는 첨부 */
const existingFiles = ref<Attachment[]>([]);
/** 삭제 표시한 기존 첨부의 id */
const deletedFileIds = ref<number[]>([]);

/**
 * 재확인 등록. 원본 요청 내용을 옮겨 담고 원문 링크를 덧붙인다.
 * 원본(Request.vue loadRecheckData)과 같은 형식이다.
 */
async function loadRecheck(sourceId: number) {
  const original = await getRequest(sourceId).catch(() => null);
  if (!original) return;

  form.title = `[재확인] ${original.title}`;
  form.ipType = (original.ipType as string) ?? 'Improvement';

  const originalUrl = `${window.location.origin}/helpdesk/request/detail/${sourceId}`;
  const who = helpdesk.identity?.userName ?? '';
  form.description = `${original.description ?? ''}
<p></p>
<hr />
<p>이전 요청: <a href="${originalUrl}" target="_blank">${originalUrl}</a></p>
<br />
<blockquote>- ${who}</blockquote>`;
}

async function load() {
  if (!requestId.value) return;

  loading.value = true;
  try {
    const data = await getRequest(requestId.value);

    // 원본과 동일하게 작성자 본인 또는 관리자만 수정할 수 있다.
    if (
      !helpdesk.isAdmin &&
      Number(data?.customer?.id) !== Number(helpdesk.helpdeskUserId)
    ) {
      message.warning('작성자만 수정할 수 있습니다.');
      router.back();
      return;
    }

    form.title = data?.title ?? '';
    form.description = data?.description ?? '';
    form.ipType = (data?.ipType as string) ?? 'Improvement';
    existingFiles.value = data?.attachments ?? [];
  } finally {
    loading.value = false;
  }
}

/** 업로드 컴포넌트가 파일을 자동 전송하지 않도록 막고 목록에만 담는다. */
function beforeUpload(file: File) {
  filesToUpload.value = [...filesToUpload.value, file];
  return false;
}

function removePendingFile(file: File) {
  filesToUpload.value = filesToUpload.value.filter((f) => f !== file);
}

function markFileDeleted(fileId: number) {
  deletedFileIds.value = [...deletedFileIds.value, fileId];
  existingFiles.value = existingFiles.value.filter((f) => f.id !== fileId);
}

/** 서버가 받는 multipart 본문을 만든다. 필드명은 원본과 동일하게 맞춘다. */
function buildFormData(customerId?: number) {
  const fd = new FormData();
  const typeCode = REQUEST_TYPES.find((t) => t.value === form.ipType)?.code ?? 1;

  fd.append('iptype', String(typeCode));
  fd.append('title', form.title);
  fd.append('description', form.description);

  if (customerId !== undefined) fd.append('customerId', String(customerId));

  filesToUpload.value.forEach((file) => fd.append('files', file));

  if (deletedFileIds.value.length > 0) {
    fd.append('deletedFiles', JSON.stringify(deletedFileIds.value));
  }

  return fd;
}

/**
 * 신규 등록 시 작성자로 넣을 고객 ID 를 정한다.
 * 관리자는 고른 회사의 공용 계정(`pub_{회사ID}`)으로, 고객은 자기 자신으로 등록한다.
 */
async function resolveAuthorId(): Promise<number | undefined> {
  if (!helpdesk.isAdmin) return helpdesk.helpdeskUserId;

  if (!form.companyId) {
    message.warning('관리자가 작성할 때는 회사를 반드시 선택하세요.');
    return undefined;
  }

  const publicUser = await getCustomerByLoginId(`pub_${form.companyId}`);
  if (!publicUser) {
    message.warning('선택한 회사의 공용 사용자를 찾을 수 없습니다.');
    return undefined;
  }
  return publicUser.id;
}

/**
 * 저장한다.
 * @param navigate 저장 후 상세로 이동할지 여부. Ctrl+S 는 이동 없이 저장한다.
 */
async function save(navigate = true) {
  if (!helpdesk.helpdeskUserId) {
    message.warning('연결된 헬프데스크 계정이 없어 저장할 수 없습니다.');
    return;
  }
  if (!form.title.trim()) {
    message.warning('제목을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    if (isEdit.value) {
      await updateRequestWithFiles(requestId.value!, buildFormData());
      if (navigate) {
        router.replace(`/helpdesk/request/detail/${requestId.value}`);
        return;
      }
      message.success('저장했습니다.');
      filesToUpload.value = [];
      deletedFileIds.value = [];
      await load();
    } else {
      const authorId = await resolveAuthorId();
      if (authorId === undefined) return;

      const created = await createRequestWithFiles(buildFormData(authorId));
      message.success('요청을 등록했습니다.');
      router.replace(
        created?.id
          ? `/helpdesk/request/detail/${created.id}`
          : '/helpdesk/request/list',
      );
    }
  } finally {
    saving.value = false;
  }
}

/** Ctrl+S · Cmd+S 로 이동 없이 저장 */
function onKeyDown(event: KeyboardEvent) {
  if ((event.ctrlKey || event.metaKey) && event.key === 's') {
    event.preventDefault();
    save(false);
  }
}

onMounted(async () => {
  window.addEventListener('keydown', onKeyDown);
  await helpdesk.loadIdentity();

  if (helpdesk.isAdmin) await helpdesk.loadOrganizations();

  const recheckId = route.query.recheckId
    ? Number(route.query.recheckId)
    : undefined;
  if (!isEdit.value && recheckId) {
    await loadRecheck(recheckId);
    return;
  }

  await load();
});

onBeforeUnmount(() => window.removeEventListener('keydown', onKeyDown));
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <Card :title="isEdit ? '요청 수정' : '요청 등록'" size="small">
        <template #extra>
          <Space>
            <Button size="small" @click="router.back()">취소</Button>
            <Button
              :loading="saving"
              size="small"
              type="primary"
              @click="save(true)"
            >
              저장
            </Button>
          </Space>
        </template>

        <Form layout="vertical">
          <FormItem label="유형">
            <Select
              v-model:value="form.ipType"
              :options="REQUEST_TYPE_OPTIONS"
              style="width: 160px"
            />
          </FormItem>

          <!-- 관리자가 회사를 대신해 등록할 때만 쓴다. -->
          <FormItem
            v-if="!isEdit && helpdesk.isAdmin"
            label="회사 (관리자 대리 등록)"
            required
          >
            <Select
              v-model:value="form.companyId"
              :options="helpdesk.companyOptions.filter((o) => o.value !== null)"
              option-filter-prop="label"
              placeholder="요청을 등록할 회사를 선택하세요"
              show-search
              style="width: 260px"
            />
          </FormItem>

          <FormItem label="제목" required>
            <Input
              v-model:value="form.title"
              placeholder="제목을 입력하세요"
              @press-enter="save(false)"
            />
          </FormItem>

          <FormItem label="내용">
            <RichEditor
              v-model="form.description"
              :min-height="280"
              biz-type="helpdesk-request"
              placeholder="내용을 입력하세요. 이미지는 붙여넣기로 바로 넣을 수 있습니다."
            />
          </FormItem>

          <FormItem label="첨부파일">
            <Upload
              :before-upload="beforeUpload"
              :file-list="[]"
              multiple
            >
              <Button size="small">파일 선택</Button>
            </Upload>

            <div v-if="filesToUpload.length" class="mt-2">
              <Tag
                v-for="file in filesToUpload"
                :key="file.name"
                closable
                @close="removePendingFile(file)"
              >
                {{ file.name }}
              </Tag>
            </div>

            <div v-if="existingFiles.length" class="mt-2">
              <div class="mb-1 text-xs text-muted-foreground">기존 첨부</div>
              <Tag
                v-for="file in existingFiles"
                :key="file.id"
                closable
                @close="markFileDeleted(file.id)"
              >
                {{ file.fileName }}
              </Tag>
            </div>
          </FormItem>
        </Form>

        <div class="text-xs text-muted-foreground">
          Ctrl + S 로 화면 이동 없이 저장할 수 있습니다.
        </div>
      </Card>
    </Spin>
  </Page>
</template>
