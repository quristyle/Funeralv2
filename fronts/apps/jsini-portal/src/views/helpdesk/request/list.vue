<script lang="ts" setup>
import type { ImprovementRequest } from '#/api/helpdesk';

import { onMounted, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Card,
  Input,
  List,
  ListItem,
  Select,
  Space,
  Spin,
  Tag,
} from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';

import { searchRequests } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import {
  formatDateTime,
  REQUEST_STATUS_OPTIONS,
  statusMeta,
} from '../shared/constants';

/**
 * [내 요청]
 *
 * 원본(Request.vue)의 카드형 목록. 고객이 자기가 올린 요청을 훑어보는 화면이라
 * 표 대신 목록 형태를 유지했다.
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const rows = ref<ImprovementRequest[]>([]);

// 셀렉트의 '전체'는 undefined. 서버로 보낼 때 null 로 바꾼다.
const filters = reactive<{ keyword: string; status: number | undefined }>({
  keyword: '',
  status: undefined,
});

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
});

async function loadData() {
  if (!helpdesk.helpdeskUserId) return;

  loading.value = true;
  try {
    const keyword = filters.keyword.trim();
    const page = await searchRequests({
      select:
        'id,title,createdAt,status,customer,admin,customer.company,mainPhoto,completededAt',
      remove: 'description,admin.photo',
      sorts: [{ dir: 'desc', field: 'createdAt' }],
      page: pagination.current,
      pageSize: pagination.pageSize,
      title_or_like: keyword || null,
      description_or_like: keyword || null,
      status: filters.status ?? null,
      // 내 요청 화면이므로 항상 본인 작성 건으로 제한한다.
      customerId: helpdesk.helpdeskUserId,
    });

    rows.value = page.items;
    pagination.total = page.totalCount;
  } finally {
    loading.value = false;
  }
}

function search() {
  pagination.current = 1;
  loadData();
}

function onPageChange(page: number, pageSize: number) {
  pagination.current = page;
  pagination.pageSize = pageSize;
  loadData();
}

watch(() => filters.status, search);

onMounted(async () => {
  await helpdesk.loadIdentity();
  await loadData();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <template v-if="helpdesk.helpdeskUserId">
      <Card class="mb-3" size="small">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <Space wrap>
            <Input
              v-model:value="filters.keyword"
              allow-clear
              placeholder="제목 + 본문"
              style="width: 220px"
              @press-enter="search"
            />
            <Select
              v-model:value="filters.status"
              :options="REQUEST_STATUS_OPTIONS"
              placeholder="상태"
              style="width: 120px"
            />
          </Space>

          <!-- 동작 단추는 오른쪽에 모은다 — 조회도 동작이다. -->
          <div class="flex items-center gap-2">
            <GridIconButton
              :loading="loading"
              icon="vxe-icon-search"
              title="조회"
              @click="search"
            />
            <GridIconButton
              icon="vxe-icon-add"
              title="요청 등록"
              @click="router.push('/helpdesk/request/new')"
            />
          </div>
        </div>
      </Card>

      <Spin :spinning="loading">
        <Card size="small">
          <List
            :data-source="rows"
            :locale="{ emptyText: '등록된 요청이 없습니다.' }"
            :pagination="{
              current: pagination.current,
              pageSize: pagination.pageSize,
              total: pagination.total,
              showSizeChanger: true,
              showTotal: (total: number) => `총 ${total}건`,
              onChange: onPageChange,
            }"
          >
            <template #renderItem="{ item }">
              <ListItem
                class="cursor-pointer"
                @click="router.push(`/helpdesk/request/detail/${item.id}`)"
              >
                <div class="flex w-full items-center gap-3">
                  <img
                    v-if="item.mainPhoto"
                    :alt="item.title"
                    :src="item.mainPhoto"
                    class="h-14 w-20 rounded object-cover"
                  />
                  <div class="min-w-0 flex-1">
                    <div class="truncate font-medium">{{ item.title }}</div>
                    <div class="mt-1 text-xs text-muted-foreground">
                      {{ formatDateTime(item.createdAt) }}
                      <span v-if="item.admin?.userName" class="ml-2">
                        접수자 {{ item.admin.userName }}
                      </span>
                    </div>
                  </div>
                  <Tag :color="statusMeta(item.status).color">
                    {{ item.statusName || statusMeta(item.status).label }}
                  </Tag>
                </div>
              </ListItem>
            </template>

          </List>
        </Card>
      </Spin>
    </template>
  </Page>
</template>
