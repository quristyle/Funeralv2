<script lang="ts" setup>
import type { Recordable } from '@vben/types';

import { useQuery } from '@tanstack/vue-query';

import { useVbenForm } from '#/adapter/form';
import { getMenuList } from '#/api';

const queryKey = ['demo', 'api', 'options'];
const count = 4;

const { dataUpdatedAt, promise: fetchDataFn } = useQuery({
  // 컴포넌트 렌더링 중 데이터 프리패치
  experimental_prefetchInRender: true,
  // 인터페이스 데이터를 가져오는 함수
  queryFn: getMenuList,
  queryKey,
  // 컴포넌트가 마운트될 때마다 데이터를 다시 가져옵니다. 매번 다시 가져올 필요가 없다면 always로 설정하지 마세요.
  refetchOnMount: 'always',
  // 캐시 시간
  staleTime: 1000 * 60 * 5,
});

async function fetchOptions() {
  return await fetchDataFn.value;
}

const schema = [];

for (let i = 0; i < count; i++) {
  schema.push({
    component: 'ApiSelect',
    componentProps: {
      api: fetchOptions,
      class: 'w-full',
      filterOption: (input: string, option: Recordable<any>) => {
        return option.label.toLowerCase().includes(input.toLowerCase());
      },
      labelField: 'name',
      showSearch: true,
      valueField: 'id',
    },
    fieldName: `field${i}`,
    label: `Select ${i}`,
  });
}

const [Form] = useVbenForm({
  schema,
  showDefaultActions: false,
});
</script>
<template>
  <div>
    <div class="mb-2 flex gap-2">
      <div>다음 {{ count }}개 컴포넌트가 하나의 데이터 소스를 공유합니다.</div>
      <div>캐시 업데이트 시간: {{ new Date(dataUpdatedAt).toLocaleString() }}</div>
    </div>
    <Form />
  </div>
</template>
