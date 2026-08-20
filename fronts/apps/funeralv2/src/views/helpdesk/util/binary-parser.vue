<script lang="ts" setup>
import type { McModel } from '#/api/helpdesk';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Checkbox,
  Col,
  InputNumber,
  message,
  Row,
  Select,
  Space,
  Spin,
  Switch,
  Textarea,
} from 'ant-design-vue';

import { getMcModels, parseBinary } from '#/api/helpdesk';

/**
 * [바이너리 파서]
 *
 * 원본(utils/BinaryParser.vue)의 해석 기능. 모델(프로토콜 정의)을 골라
 * 전문을 해석한다. 모델 정의 자체를 편집하는 화면은 'MC 모델 관리'로 분리했다.
 */

const loading = ref(false);
const content = ref('');
const parsedLines = ref<string[]>([]);
const models = ref<McModel[]>([]);

const options = ref({
  byteGroup: 4,
  isLittleEndian: true,
  isProtocolMode: false,
  isRxLengthFirst: false,
  modelId: undefined as number | undefined,
});

const resultText = computed(() =>
  parsedLines.value.length === 0
    ? '해석 결과가 여기에 표시됩니다.'
    : parsedLines.value.map((line) => line || ' ').join('\n'),
);

async function run() {
  if (!content.value.trim()) {
    message.warning('내용을 입력해 주세요.');
    return;
  }

  loading.value = true;
  try {
    const data = await parseBinary({
      byteGroup: options.value.byteGroup,
      content: content.value,
      interpretationType: 'HEX',
      isLittleEndian: options.value.isLittleEndian,
      isProtocolMode: options.value.isProtocolMode,
      isRxLengthFirst: options.value.isRxLengthFirst,
      model: options.value.modelId,
    });
    parsedLines.value = data?.parsedLines ?? [];
    message.success('해석을 완료했습니다.');
  } finally {
    loading.value = false;
  }
}

function clearAll() {
  content.value = '';
  parsedLines.value = [];
}

onMounted(async () => {
  models.value = (await getMcModels()) ?? [];
});
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <Space wrap size="large">
        <Space>
          <span class="text-sm">모델</span>
          <Select
            v-model:value="options.modelId"
            :options="models.map((m) => ({ label: m.modelName, value: m.id }))"
            allow-clear
            option-filter-prop="label"
            placeholder="모델 선택"
            show-search
            style="width: 200px"
          />
        </Space>
        <Space>
          <span class="text-sm">바이트 묶음</span>
          <InputNumber
            v-model:value="options.byteGroup"
            :max="16"
            :min="1"
            style="width: 80px"
          />
        </Space>
        <Checkbox v-model:checked="options.isLittleEndian">리틀 엔디언</Checkbox>
        <Checkbox v-model:checked="options.isRxLengthFirst">
          RX 길이 우선
        </Checkbox>
        <Space>
          <span class="text-sm">프로토콜 모드</span>
          <Switch v-model:checked="options.isProtocolMode" />
        </Space>
        <Space>
          <Button :loading="loading" type="primary" @click="run">
            해석 실행
          </Button>
          <Button @click="clearAll">비우기</Button>
        </Space>
      </Space>
    </Card>

    <Row :gutter="[12, 12]">
      <Col :lg="12" :xs="24">
        <Card size="small" title="입력">
          <Textarea
            v-model:value="content"
            :rows="20"
            class="font-mono"
            placeholder="16진 전문을 붙여 넣으세요."
          />
        </Card>
      </Col>

      <Col :lg="12" :xs="24">
        <Card size="small" title="해석 결과">
          <Spin :spinning="loading">
            <pre
              class="m-0 max-h-[460px] overflow-auto whitespace-pre-wrap rounded bg-muted/40 p-3 font-mono text-xs"
              >{{ resultText }}</pre
            >
          </Spin>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
