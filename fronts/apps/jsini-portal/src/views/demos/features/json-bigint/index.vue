<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert, Button, Card } from 'ant-design-vue';

import { getBigIntData } from '#/api/examples/json-bigint';

const response = ref('');
function fetchData() {
  getBigIntData().then((res) => {
    response.value = res;
  });
}
</script>
<template>
  <Page
    title="JSON BigInt 지원"
    description="백엔드에서 반환된 긴 정수(long/bigInt)를 파싱합니다. 코드 위치: playground/src/api/request.ts의 transformResponse"
  >
    <Card>
      <Alert>
        <template #message>
          일부 백엔드 인터페이스는 ID를 긴 정수로 반환하지만, JavaScript의 기본 JSON 파싱은 2^53-1을 초과하는 긴 정수를 지원하지 않습니다.
          이 경우 백엔드에서 데이터를 반환하기 전에 긴 정수를 문자열로 변환하는 것을 권장합니다. 만약 백엔드에서 우리의 권장사항을 받아들이지 않는다면... 😡
          <br />
          아래 버튼을 클릭하면 요청이 시작되며, 인터페이스가 반환한 JSON 데이터의 id 필드는 정수 범위를 초과하는 숫자이지만 자동으로 문자열로 파싱됩니다.
        </template>
      </Alert>
      <Button class="mt-4" type="primary" @click="fetchData">요청 시작</Button>
      <div>
        <pre>
        {{ response }}
        </pre>
      </div>
    </Card>
  </Page>
</template>
