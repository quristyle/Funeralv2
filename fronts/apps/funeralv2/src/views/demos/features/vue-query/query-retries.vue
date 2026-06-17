<script setup lang="ts">
import { ref } from 'vue';

import { useQuery } from '@tanstack/vue-query';
import { Button } from 'ant-design-vue';

const count = ref(-1);
async function fetchApi() {
  count.value += 1;
  return new Promise((_resolve, reject) => {
    setTimeout(() => {
      reject(new Error('something went wrong!'));
    }, 1000);
  });
}

const { error, isFetching, refetch } = useQuery({
  enabled: false, // Disable automatic refetching when the query mounts
  queryFn: fetchApi,
  queryKey: ['queryKey'],
  retry: 3, // Will retry failed requests 3 times before displaying an error
});

const onClick = async () => {
  count.value = -1;
  await refetch();
};
</script>

<template>
  <Button :loading="isFetching" @click="onClick"> 오류 재시도 시작 </Button>
  <p v-if="count > 0" class="my-3">재시도 횟수 {{ count }}</p>
  <p>{{ error }}</p>
</template>
