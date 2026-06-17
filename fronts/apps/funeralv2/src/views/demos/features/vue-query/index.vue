<script setup lang="ts">
import { Page } from '@vben/common-ui';

import { refAutoReset } from '@vueuse/core';
import { Button, Card, Empty } from 'ant-design-vue';

import ConcurrencyCaching from './concurrency-caching.vue';
import InfiniteQueries from './infinite-queries.vue';
import PaginatedQueries from './paginated-queries.vue';
import QueryRetries from './query-retries.vue';

const showCaching = refAutoReset(true, 1000);
</script>

<template>
  <Page title="Vue Query 예제">
    <div class="grid grid-cols-1 gap-4 md:grid-cols-2">
      <Card title="페이지네이션 조회">
        <PaginatedQueries />
      </Card>
      <Card title="무한 스크롤">
        <InfiniteQueries class="h-75 overflow-auto" />
      </Card>
      <Card title="오류 재시도">
        <QueryRetries />
      </Card>
      <Card
        title="동시성 및 캐시"
        v-spinning="!showCaching"
        :body-style="{ minHeight: '330px' }"
      >
        <template #extra>
          <Button @click="showCaching = false">새로고침</Button>
        </template>
        <ConcurrencyCaching v-if="showCaching" />
        <Empty v-else description="로딩 중..." />
      </Card>
    </div>
  </Page>
</template>
