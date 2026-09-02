<script lang="ts" setup>
import type { VxeTableGridOptions } from '#/adapter/vxe-table';

import { computed, ref } from 'vue';

import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Button, Descriptions, Input, message, Select, Tag, Tooltip } from 'ant-design-vue';
import dayjs from 'dayjs';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  getSiteInquiries,
  replySiteInquiry,
  setSiteInquiryStatus,
  type SiteInquiryApi,
} from '#/api/portal/site';
import { RichEditor } from '#/components/rich-editor';

/**
 * [사이트 문의내역]
 *
 * 회사 소개 사이트(www.jsini.co.kr)의 문의 폼으로 들어온 접수를 보고 답장한다.
 * 자료는 SiteServer(/api/site/admin/*)가 준다.
 *
 * 답장은 문의에 적힌 이메일을 채워 주되 고칠 수 있다 — 이메일이 없거나
 * 다른 주소로 보내야 하는 경우를 위해서다. 메일 틀(머리글 · 원문 인용 · 서명)은
 * 서버(InquiryEmailTemplates)가 입히므로 여기서는 답변 내용만 쓴다.
 */

const STATUS = [
  { value: 'new', label: '신규', color: 'blue' },
  { value: 'reading', label: '확인 중', color: 'orange' },
  { value: 'answered', label: '답변 완료', color: 'green' },
  { value: 'spam', label: '스팸', color: 'red' },
];

function statusOf(value: string) {
  return STATUS.find((s) => s.value === value) ?? { value, label: value, color: 'default' };
}

const current = ref<null | SiteInquiryApi.Inquiry>(null);

// ── 목록 ─────────────────────────────────────────────────────

const [Grid, gridApi] = useVbenVxeGrid({
  showSearchForm: true,
  formOptions: {
    wrapperClass: 'grid-cols-1 md:grid-cols-3',
    schema: [
      {
        component: 'Select',
        fieldName: 'status',
        label: '상태',
        componentProps: {
          options: STATUS.map((s) => ({ label: s.label, value: s.value })),
          allowClear: true,
          placeholder: '전체',
        },
      },
    ],
  },
  gridOptions: {
    columns: [
      {
        field: 'createdAt',
        title: '접수일',
        width: 150,
        formatter: ({ cellValue }) => (cellValue ? dayjs(cellValue).format('YYYY-MM-DD HH:mm') : ''),
      },
      {
        field: 'status',
        title: '상태',
        width: 100,
        slots: { default: 'status' },
      },
      { field: 'category', title: '분류', width: 90 },
      { field: 'name', title: '이름', width: 110 },
      { field: 'company', title: '회사', width: 140 },
      { field: 'email', title: '이메일', width: 200 },
      { field: 'subject', title: '제목', minWidth: 220 },
      { field: 'action', title: '작업', width: 110, fixed: 'right', slots: { default: 'action' } },
    ],
    height: 'auto',
    pagerConfig: { enabled: false },
    proxyConfig: {
      // 페이저 없는 그리드에 배열만 반환하면 vxe 가 목록을 찾지 못한다
      // (portal/system/menu/list.vue 에 기록된 선례) — result 로 감싸고 위치를 명시한다.
      response: { list: 'result' },
      ajax: {
        query: async (_params, formValues) => {
          const rows = await getSiteInquiries(formValues?.status);
          // 상세를 열어 둔 채 새로고침하면 내용을 최신으로 맞춘다
          if (current.value) {
            current.value = rows.find((r) => r.id === current.value?.id) ?? null;
          }
          return { result: rows, page: { total: rows.length } };
        },
      },
    },
    rowConfig: { keyField: 'id', isCurrent: true },
  } as VxeTableGridOptions,
  gridEvents: {
    cellClick: ({ row }: { row: SiteInquiryApi.Inquiry }) => openDetail(row),
  },
});

// ── 상세 ─────────────────────────────────────────────────────

const [DetailModal, detailModalApi] = useVbenModal({
  title: '문의 상세',
  footer: false,
  draggable: true,
  class: 'w-[720px]',
});

function openDetail(row: SiteInquiryApi.Inquiry) {
  current.value = row;
  detailModalApi.open();
}

async function changeStatus(value: any) {
  if (!current.value || typeof value !== 'string') return;
  try {
    await setSiteInquiryStatus(current.value.id, value);
    current.value.status = value;
    message.success('상태를 바꿨습니다.');
    gridApi.query();
  } catch {
    message.error('상태 변경에 실패했습니다.');
  }
}

// ── 답장 ─────────────────────────────────────────────────────

const reply = ref({ to: '', subject: '', body: '' });
const replySending = ref(false);

const replyValid = computed(
  () => !!reply.value.to.trim() && !!reply.value.subject.trim() && !!reply.value.body.trim(),
);

const [ReplyModal, replyModalApi] = useVbenModal({
  title: '답장 보내기',
  draggable: true,
  class: 'w-[760px]',
  confirmText: '보내기',
  cancelText: '취소',
  onConfirm: sendReply,
});

function openReply() {
  if (!current.value) return;
  reply.value = {
    // 문의에 이메일이 있으면 채워 준다. 없으면(또는 다른 곳으로 보내려면) 직접 적는다.
    to: current.value.email ?? '',
    subject: `Re: ${current.value.subject}`,
    body: '',
  };
  replyModalApi.open();
}

async function sendReply() {
  if (!current.value) return;
  if (!replyValid.value) {
    message.warning('받는 사람 · 제목 · 본문을 모두 채워 주세요.');
    return;
  }

  replySending.value = true;
  replyModalApi.setState({ confirmLoading: true });
  try {
    await replySiteInquiry(current.value.id, {
      to: reply.value.to.trim(),
      subject: reply.value.subject.trim(),
      body: reply.value.body,
    });
    message.success('답장을 보냈습니다.');
    replyModalApi.close();
    detailModalApi.close();
    gridApi.query();
  } catch {
    // 실패 사유 토스트는 공통 인터셉터가 띄운다
  } finally {
    replySending.value = false;
    replyModalApi.setState({ confirmLoading: false });
  }
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <Grid table-title="사이트 문의내역">
      <template #status="{ row }">
        <Tag :color="statusOf(row.status).color">{{ statusOf(row.status).label }}</Tag>
      </template>
      <template #action="{ row }">
        <div class="flex justify-center gap-2">
          <Tooltip title="상세 보기">
            <Button size="small" type="link" @click.stop="openDetail(row)">
              <IconifyIcon class="size-4" icon="lucide:eye" />
            </Button>
          </Tooltip>
          <Tooltip v-perm:update title="답장">
            <Button size="small" type="link" @click.stop="openDetail(row); openReply()">
              <IconifyIcon class="size-4" icon="lucide:reply" />
            </Button>
          </Tooltip>
        </div>
      </template>
    </Grid>

    <!-- 상세 -->
    <DetailModal>
      <div v-if="current" class="max-h-[62vh] space-y-4 overflow-y-auto p-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <Tag :color="statusOf(current.status).color">{{ statusOf(current.status).label }}</Tag>
            <span class="text-base font-semibold">{{ current.subject }}</span>
          </div>
          <div class="flex items-center gap-2">
            <Select
              v-perm:update
              :options="STATUS.map((s) => ({ label: s.label, value: s.value }))"
              :value="current.status"
              size="small"
              style="width: 110px"
              @change="changeStatus"
            />
            <Button v-perm:update size="small" type="primary" @click="openReply">
              <IconifyIcon class="mr-1 size-4" icon="lucide:reply" />
              답장
            </Button>
          </div>
        </div>

        <Descriptions :column="2" bordered size="small">
          <Descriptions.Item label="이름">{{ current.name }}</Descriptions.Item>
          <Descriptions.Item label="회사">{{ current.company || '-' }}</Descriptions.Item>
          <Descriptions.Item label="이메일">{{ current.email || '-' }}</Descriptions.Item>
          <Descriptions.Item label="연락처">{{ current.phone || '-' }}</Descriptions.Item>
          <Descriptions.Item label="분류">{{ current.category || '-' }}</Descriptions.Item>
          <Descriptions.Item label="접수일">
            {{ dayjs(current.createdAt).format('YYYY-MM-DD HH:mm') }}
          </Descriptions.Item>
          <Descriptions.Item :span="2" label="접수 IP">
            {{ current.clientIp || '-' }}
          </Descriptions.Item>
        </Descriptions>

        <!-- 본문 — 서버가 허용 목록으로 거른 HTML 이다 -->
        <div class="bg-card border-border rounded border p-4">
          <p class="text-muted-foreground mb-2 text-xs font-semibold uppercase">문의 내용</p>
          <!-- eslint-disable-next-line vue/no-v-html -->
          <div
            class="text-sm leading-relaxed [&_ol]:list-decimal [&_ol]:pl-5 [&_ul]:list-disc [&_ul]:pl-5"
            v-html="current.message"
          ></div>
        </div>

        <div v-if="current.internalNote" class="bg-muted/40 border-border rounded border p-4">
          <p class="text-muted-foreground mb-2 text-xs font-semibold uppercase">처리 기록</p>
          <pre class="whitespace-pre-wrap text-xs">{{ current.internalNote }}</pre>
        </div>
      </div>
    </DetailModal>

    <!-- 답장 -->
    <ReplyModal>
      <div class="space-y-3 p-4">
        <div class="flex items-center gap-2">
          <span class="w-20 shrink-0 text-sm font-semibold">받는 사람</span>
          <Input
            v-model:value="reply.to"
            placeholder="답장 받을 이메일 주소"
            type="email"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="w-20 shrink-0 text-sm font-semibold">제목</span>
          <Input v-model:value="reply.subject" />
        </div>
        <RichEditor v-model="reply.body" :min-height="220" placeholder="답변 내용을 적어 주세요." />
        <p class="text-muted-foreground text-xs">
          머리글 · 원문 인용 · 서명이 붙은 메일 틀은 서버가 입힙니다 — 답변 내용만 쓰면 됩니다.
        </p>
      </div>
    </ReplyModal>
  </Page>
</template>
