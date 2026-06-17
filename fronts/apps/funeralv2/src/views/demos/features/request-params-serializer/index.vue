<script lang="ts" setup>
import { computed, ref, watchEffect } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, Radio, RadioGroup } from 'ant-design-vue';

import { getParamsData } from '#/api/examples/params';

const params = { ids: [2512, 3241, 4255] };
const paramsSerializer = ref<'brackets' | 'comma' | 'indices' | 'repeat'>(
  'brackets',
);
const response = ref('');
const paramsStr = computed(() => {
  // 전체 URL에서 매개변수 부분을 추출하는 코드
  const url = response.value;
  return new URL(url).searchParams.toString();
});

watchEffect(() => {
  getParamsData(params, paramsSerializer.value).then((res) => {
    response.value = res.request.responseURL;
  });
});
</script>
<template>
  <Page
    title="요청 매개변수 직렬화"
    description="백엔드 인터페이스마다 배열 유형의 GET 매개변수를 해석하는 방식이 다를 수 있습니다. 몇 가지 배열 직렬화 방식을 미리 설정해 두었으며, paramsSerializer 설정을 통해 다양한 직렬화 방식을 구현할 수 있습니다."
  >
    <Card>
      <RadioGroup v-model:value="paramsSerializer" name="paramsSerializer">
        <Radio value="brackets">brackets</Radio>
        <Radio value="comma">comma</Radio>
        <Radio value="indices">indices</Radio>
        <Radio value="repeat">repeat</Radio>
      </RadioGroup>
      <div class="mt-4 flex flex-col gap-4">
        <div>
          <h3>제출할 매개변수</h3>
          <div>{{ JSON.stringify(params, null, 2) }}</div>
        </div>
        <template v-if="response">
          <div>
            <h3>접속 주소</h3>
            <pre>{{ response }}</pre>
          </div>
          <div>
            <h3>매개변수 문자열</h3>
            <pre>{{ paramsStr }}</pre>
          </div>
          <div>
            <h3>매개변수 디코딩</h3>
            <pre>{{ decodeURIComponent(paramsStr) }}</pre>
          </div>
        </template>
      </div>
    </Card>
  </Page>
</template>
