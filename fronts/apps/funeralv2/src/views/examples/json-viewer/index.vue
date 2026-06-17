<script lang="ts" setup>
import type { JsonViewerAction, JsonViewerValue } from '@vben/common-ui';

import { JsonViewer, Page } from '@vben/common-ui';

import { Card, message } from 'ant-design-vue';

import { json1, json2 } from './data';

function handleKeyClick(key: string) {
  message.info(`Key ${key}를 클릭했습니다`);
}

function handleValueClick(value: JsonViewerValue) {
  message.info(`Value ${JSON.stringify(value)}를 클릭했습니다`);
}

function handleCopied(_event: JsonViewerAction) {
  message.success('JSON이 복사되었습니다');
}
</script>
<template>
  <Page
    title="Json Viewer"
    description="JSON 구조 데이터를 렌더링하는 컴포넌트입니다. 복사, 확장 등을 지원하며 사용이 간편합니다."
  >
    <Card title="기본 설정">
      <JsonViewer :value="json1" />
    </Card>
    <Card title="복사 가능, 기본 3계층 확장, 테두리 표시, 이벤트 처리" class="mt-4">
      <JsonViewer
        :value="json2"
        :expand-depth="3"
        copyable
        :sort="false"
        @key-click="handleKeyClick"
        @value-click="handleValueClick"
        @copied="handleCopied"
        boxed
      />
    </Card>
    <Card title="미리보기 모드" class="mt-4">
      <JsonViewer
        :value="json2"
        copyable
        preview-mode
        :show-array-index="false"
      />
    </Card>
  </Page>
</template>
