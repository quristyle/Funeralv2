<script lang="ts" setup>
import { computed, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  CheckboxGroup,
  Col,
  message,
  Radio,
  RadioGroup,
  Row,
  Space,
  Spin,
  Textarea,
} from 'ant-design-vue';

import { parseAscii } from '#/api/helpdesk';

/**
 * [ASCII 파서]
 *
 * 원본(utils/AsciiParser.vue). 통신 로그를 붙여 넣으면 서버가 해석해 돌려준다.
 */

const loading = ref(false);
const asciiInput = ref('');
const parsedLines = ref<string[]>([]);
const selectedHeads = ref<string[]>(['RX', 'TX']);
const interpretationType = ref<'ASCII' | 'DEC' | 'HEX'>('HEX');

const HEAD_OPTIONS = [
  { label: 'RX', value: 'RX' },
  { label: 'TX', value: 'TX' },
];

const TYPE_OPTIONS = [
  { label: 'HEX', value: 'HEX' },
  { label: 'DEC', value: 'DEC' },
  { label: 'ASCII', value: 'ASCII' },
];

const resultText = computed(() =>
  parsedLines.value.length === 0
    ? '해석 결과가 여기에 표시됩니다.\n\n"해석 실행" 버튼을 누르세요.'
    : parsedLines.value.map((line) => line || ' ').join('\n'),
);

async function run() {
  if (!asciiInput.value.trim()) {
    message.warning('내용을 입력해 주세요.');
    return;
  }

  loading.value = true;
  try {
    const data = await parseAscii({
      content: asciiInput.value,
      heads: selectedHeads.value,
      interpretationType: interpretationType.value,
    });
    parsedLines.value = data?.parsedLines ?? [];
    message.success('해석을 완료했습니다.');
  } finally {
    loading.value = false;
  }
}

function clearAll() {
  asciiInput.value = '';
  parsedLines.value = [];
}

/** 기본 샘플 전문을 불러온다. 원본과 같은 public/ascii_sample.txt 를 쓴다. */
async function loadSample() {
  try {
    const response = await fetch('/ascii_sample.txt');
    if (!response.ok) throw new Error('파일을 불러올 수 없습니다.');
    asciiInput.value = await response.text();
    parsedLines.value = [];
    message.success('샘플 데이터를 불러왔습니다.');
  } catch {
    message.error('샘플 파일을 읽어오는 데 실패했습니다.');
  }
}

async function copyResult() {
  await navigator.clipboard?.writeText(parsedLines.value.join('\n'));
  message.success('결과를 복사했습니다.');
}
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <Space wrap size="large">
        <Space>
          <span class="text-sm">헤드</span>
          <CheckboxGroup v-model:value="selectedHeads" :options="HEAD_OPTIONS" />
        </Space>
        <Space>
          <span class="text-sm">해석</span>
          <RadioGroup v-model:value="interpretationType">
            <Radio v-for="opt in TYPE_OPTIONS" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </Radio>
          </RadioGroup>
        </Space>
        <Space>
          <Button :loading="loading" type="primary" @click="run">
            해석 실행
          </Button>
          <Button @click="loadSample">기본 샘플</Button>
          <Button @click="clearAll">비우기</Button>
          <Button :disabled="parsedLines.length === 0" @click="copyResult">
            결과 복사
          </Button>
        </Space>
      </Space>
    </Card>

    <Row :gutter="[12, 12]">
      <Col :lg="12" :xs="24">
        <Card size="small" title="입력">
          <Textarea
            v-model:value="asciiInput"
            :rows="20"
            class="font-mono"
            placeholder="통신 로그를 붙여 넣으세요."
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
