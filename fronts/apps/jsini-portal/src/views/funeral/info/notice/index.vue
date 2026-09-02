<script lang="ts" setup>
/**
 * 알림정보 — 옛 시스템의 `t_notification` 을 화면으로 옮긴 것이다.
 *
 * 옛 표는 받는 사람과 읽음 여부를 한 행에 담아서 같은 알림을 여럿에게 보내면
 * 본문이 복제됐다. 지금은 받는 사람을 비우면 전체 공지고 읽음은 따로 기록한다.
 */
import { onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import {
  Badge,
  Button,
  DatePicker,
  Form,
  Input,
  Modal,
  Popconfirm,
  Select,
  Switch,
  Tag,
  message,
} from 'ant-design-vue';
import dayjs from 'dayjs';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import type { InfoApi } from '#/api/funeral/info';
import {
  createNotice,
  deleteNotice,
  getNotices,
  markNoticeRead,
  updateNotice,
} from '#/api/funeral/info';
import { getBuildings } from '#/api/funeral/building';
import GridIconButton from '#/components/GridIconButton.vue';

const buildings = ref<any[]>([]);
const searchBuildingId = ref<string | undefined>();
const includeExpired = ref<boolean>(false);

/** 등록·수정 팝업 */
const showEditModal = ref(false);
const editingId = ref<string>('');
const saving = ref(false);

/** 본문 보기 팝업 */
const showViewModal = ref(false);
const viewing = ref<InfoApi.Notice | null>(null);

const emptyForm = () => ({
  title: '',
  content: '',
  noticeType: 'NOTICE',
  isImportant: false,
  buildingId: undefined as string | undefined,
  targetUserId: '',
  range: undefined as [any, any] | undefined,
});

const form = ref(emptyForm());

const NOTICE_TYPES = [
  { label: '공지', value: 'NOTICE' },
  { label: '경고', value: 'ALERT' },
  { label: '시스템', value: 'SYSTEM' },
];

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'isRead', title: '읽음', width: 70, slots: { default: 'read' } },
      { field: 'noticeType', title: '구분', width: 90, slots: { default: 'type' } },
      { field: 'title', title: '제목', minWidth: 240, slots: { default: 'title' } },
      { field: 'buildingName', title: '대상 건물', width: 140 },
      { field: 'targetUserId', title: '받는 사람', width: 140, slots: { default: 'target' } },
      { field: 'startAt', title: '게시 시작', width: 160, formatter: fmtDate },
      { field: 'endAt', title: '게시 종료', width: 160, formatter: fmtDate },
      { field: 'author', title: '등록자', width: 120 },
      { field: 'createdAt', title: '등록일시', width: 160, formatter: fmtDate },
      { field: 'action', title: '관리', width: 110, fixed: 'right', slots: { default: 'action' } },
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () =>
          await getNotices({
            buildingId: searchBuildingId.value || undefined,
            includeExpired: includeExpired.value,
          }),
      },
    },
  },
});

function fmtDate({ cellValue }: { cellValue: any }) {
  return cellValue ? dayjs(cellValue).format('YYYY-MM-DD HH:mm') : '-';
}

async function fetchBuildings() {
  try {
    buildings.value = (await getBuildings()) || [];
  } catch {
    message.error('건물 목록을 불러오지 못했습니다.');
  }
}

function openCreate() {
  editingId.value = '';
  form.value = emptyForm();
  showEditModal.value = true;
}

function openEdit(row: any) {
  editingId.value = row.id;
  form.value = {
    title: row.title,
    content: row.content ?? '',
    noticeType: row.noticeType,
    isImportant: row.isImportant,
    buildingId: row.buildingId || undefined,
    targetUserId: row.targetUserId ?? '',
    range:
      row.startAt || row.endAt
        ? [row.startAt ? dayjs(row.startAt) : null, row.endAt ? dayjs(row.endAt) : null]
        : undefined,
  };
  showEditModal.value = true;
}

/** 제목을 누르면 본문을 보여 주고 읽음으로 표시한다. */
async function openView(row: any) {
  viewing.value = row;
  showViewModal.value = true;

  if (!row.isRead) {
    try {
      await markNoticeRead(row.id);
      gridApi.query();
    } catch {
      // 읽음 표시는 실패해도 본문 보기를 막지 않는다.
    }
  }
}

async function handleSave() {
  if (!form.value.title.trim()) {
    message.warning('제목을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      title: form.value.title.trim(),
      content: form.value.content,
      noticeType: form.value.noticeType,
      isImportant: form.value.isImportant,
      buildingId: form.value.buildingId,
      targetUserId: form.value.targetUserId?.trim() || undefined,
      startAt: form.value.range?.[0] ? form.value.range[0].toISOString() : undefined,
      endAt: form.value.range?.[1] ? form.value.range[1].toISOString() : undefined,
    };

    if (editingId.value) {
      await updateNotice(editingId.value, payload);
      message.success('알림을 수정했습니다.');
    } else {
      await createNotice(payload);
      message.success('알림을 등록했습니다.');
    }

    showEditModal.value = false;
    gridApi.query();
  } catch {
    message.error('저장에 실패했습니다.');
  } finally {
    saving.value = false;
  }
}

async function handleDelete(row: any) {
  try {
    await deleteNotice(row.id);
    message.success('알림을 삭제했습니다.');
    gridApi.query();
  } catch {
    message.error('삭제에 실패했습니다.');
  }
}

onMounted(fetchBuildings);
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="알림 정보">
      <template #toolbar-tools>
        <div class="flex items-center gap-2">
          <Select
            v-model:value="searchBuildingId"
            class="w-40"
            allow-clear
            placeholder="건물 전체"
            :options="buildings.map((b) => ({ label: b.name, value: b.id }))"
            @change="gridApi.query()"
          />
          <div class="flex items-center gap-1 text-xs text-muted-foreground">
            <Switch v-model:checked="includeExpired" size="small" @change="gridApi.query()" />
            <span>기간 지난 것도 보기</span>
          </div>
          <GridIconButton
            icon="vxe-icon-add"
            title="신규 알림 등록"
            @click="openCreate"
          />
        </div>
      </template>

      <template #read="{ row }">
        <Badge v-if="!row.isRead" status="processing" text="새" />
        <span v-else class="text-xs text-muted-foreground">읽음</span>
      </template>

      <template #type="{ row }">
        <Tag v-if="row.noticeType === 'ALERT'" color="error">경고</Tag>
        <Tag v-else-if="row.noticeType === 'SYSTEM'" color="processing">시스템</Tag>
        <Tag v-else color="default">공지</Tag>
      </template>

      <template #title="{ row }">
        <button
          type="button"
          class="text-left hover:underline"
          :class="row.isRead ? 'text-muted-foreground' : 'font-semibold'"
          @click="openView(row)"
        >
          <IconifyIcon v-if="row.isImportant" icon="lucide:pin" class="size-3.5 mr-1 inline text-red-500" />
          {{ row.title }}
        </button>
      </template>

      <template #target="{ row }">
        <span v-if="row.targetUserId">{{ row.targetUserId }}</span>
        <Tag v-else color="blue">전체</Tag>
      </template>

      <template #action="{ row }">
        <div class="flex gap-1">
          <Button type="link" size="small" title="수정" @click="openEdit(row)">
            <IconifyIcon icon="lucide:edit-3" class="size-4" />
          </Button>
          <Popconfirm title="이 알림을 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger title="삭제">
              <IconifyIcon icon="lucide:trash-2" class="size-4" />
            </Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- 등록·수정 -->
    <Modal
      v-model:open="showEditModal"
      :title="editingId ? '알림 수정' : '신규 알림 등록'"
      :confirm-loading="saving"
      width="640px"
      destroy-on-close
      @ok="handleSave"
    >
      <Form layout="vertical" class="pt-2">
        <Form.Item label="제목" required>
          <Input v-model:value="form.title" placeholder="알림 제목" />
        </Form.Item>
        <Form.Item label="본문">
          <Input.TextArea v-model:value="form.content" :rows="5" placeholder="알림 내용" />
        </Form.Item>
        <div class="grid grid-cols-2 gap-3">
          <Form.Item label="구분">
            <Select v-model:value="form.noticeType" :options="NOTICE_TYPES" />
          </Form.Item>
          <Form.Item label="대상 건물">
            <Select
              v-model:value="form.buildingId"
              allow-clear
              placeholder="전체 건물"
              :options="buildings.map((b) => ({ label: b.name, value: b.id }))"
            />
          </Form.Item>
        </div>
        <Form.Item label="받는 사람">
          <Input v-model:value="form.targetUserId" placeholder="비우면 전체 공지가 된다" />
        </Form.Item>
        <Form.Item label="게시 기간">
          <DatePicker.RangePicker v-model:value="form.range" show-time class="w-full" />
        </Form.Item>
        <Form.Item>
          <div class="flex items-center gap-2">
            <Switch v-model:checked="form.isImportant" />
            <span class="text-sm">중요 표시 (목록 맨 위에 붙는다)</span>
          </div>
        </Form.Item>
      </Form>
    </Modal>

    <!-- 본문 보기 -->
    <Modal
      v-model:open="showViewModal"
      :title="viewing?.title"
      :footer="null"
      width="640px"
      destroy-on-close
    >
      <div v-if="viewing" class="space-y-3">
        <div class="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
          <Tag v-if="viewing.noticeType === 'ALERT'" color="error">경고</Tag>
          <Tag v-else-if="viewing.noticeType === 'SYSTEM'" color="processing">시스템</Tag>
          <Tag v-else color="default">공지</Tag>
          <span>{{ viewing.author || '-' }}</span>
          <span>{{ dayjs(viewing.createdAt).format('YYYY-MM-DD HH:mm') }}</span>
          <span v-if="viewing.buildingName">· {{ viewing.buildingName }}</span>
        </div>
        <div class="whitespace-pre-wrap rounded border bg-muted/30 p-4 text-sm">
          {{ viewing.content || '(본문 없음)' }}
        </div>
      </div>
    </Modal>
  </Page>
</template>
