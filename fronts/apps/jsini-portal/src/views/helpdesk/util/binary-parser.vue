<script lang="ts" setup>
import type { BinarySample, McModel } from '#/api/helpdesk';

import { computed, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Checkbox,
  CheckboxGroup,
  Col,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Row,
  Select,
  Space,
  Spin,
  Switch,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { CodeEditor } from '#/components/code-editor';
import {
  createSample,
  getMcModels,
  getSample,
  getSamples,
  parseBinary,
  updateSample,
} from '#/api/helpdesk';

/**
 * [바이너리 파서]
 *
 * 원본(JinReception utils/BinaryParser.vue, `/utils/binary-parser`).
 *
 * 원본은 Monaco 에디터에 전문을 붙여 넣고, 줄을 클릭하면 그 줄만 다시 해석해
 * 상세 분석을 옆 패널에 띄우는 구조였다. 이식본도 같은 Monaco 편집기를 쓴다
 * (공용 부품 `#/components/code-editor`). 편집기에서 커서를 옮기면 그 줄이,
 * 아래 줄 목록에서 누르면 그 줄이 다시 해석된다.
 *
 * 규격(모델·파싱항목·태그) 편집은 'MC 모델 관리' 화면으로 분리했다.
 */

const loading = ref(false);
const analyzing = ref(false);
const savingSample = ref(false);

const content = ref('');
const parsedLines = ref<string[]>([]);

const models = ref<McModel[]>([]);
const selectedModel = ref<string | undefined>();

const samples = ref<BinarySample[]>([]);
const selectedSampleId = ref<number | undefined>();
/** 불러온 샘플. 있으면 '수정 저장'으로 동작한다. */
const loadedSampleId = ref<number | undefined>();

const selectedHeads = ref<string[]>(['RX', 'TX', 'Count', 'CRC']);
const HEAD_OPTIONS = [
  { label: 'RX', value: 'RX' },
  { label: 'TX', value: 'TX' },
  { label: 'Count', value: 'Count' },
  { label: 'CRC', value: 'CRC' },
];

/** 규격을 적용해 해석할지. 원본의 isProtocolMode. */
const protocolMode = ref(true);
const options = ref({
  byteGroup: 4,
  isLittleEndian: true,
  isRxLengthFirst: false,
});

// ── 줄 단위 상세 분석 ──────────────────────────────────────
const selectedLineIndex = ref<null | number>(null);

/** 편집기 인스턴스. 커서가 있는 줄을 그대로 해석 대상으로 삼는다(원본과 같은 동작). */
const editorRef = ref<any>(null);
const lineAnalysis = ref<string[]>([]);

/** 입력 전문을 줄 단위로 쪼갠다. */
const inputLines = computed(() =>
  content.value.length === 0 ? [] : content.value.split(/\r?\n/),
);

const resultText = computed(() =>
  parsedLines.value.length === 0
    ? '해석 결과가 여기에 표시됩니다.\n\n"해석 실행"을 누르세요.'
    : parsedLines.value.map((line) => line || ' ').join('\n'),
);

// ── 샘플 ──────────────────────────────────────────────────
const sampleSaveOpen = ref(false);
const newSampleTitle = ref('');

/** 선택한 모델의 보관 샘플을 다시 읽는다. */
async function loadSamples() {
  const model = models.value.find((m) => m.mcName === selectedModel.value);
  if (!model) {
    samples.value = [];
    return;
  }
  samples.value = (await getSamples(model.id).catch(() => [])) ?? [];
}

/** 선택한 샘플을 입력 영역에 붙인다. */
async function applySample() {
  if (!selectedSampleId.value) return;

  loading.value = true;
  try {
    const sample = await getSample(selectedSampleId.value);
    content.value = sample?.content ?? '';
    loadedSampleId.value = sample?.id;
    parsedLines.value = [];
    lineAnalysis.value = [];
    selectedLineIndex.value = null;
    message.success(`'${sample?.title}' 샘플을 불러왔습니다.`);
  } finally {
    loading.value = false;
  }
}

/** 저장 버튼. 불러온 샘플이 있으면 덮어쓰고, 없으면 제목을 물어 새로 만든다. */
function onSaveSampleClick() {
  if (!content.value.trim()) {
    message.warning('저장할 내용이 없습니다.');
    return;
  }

  if (loadedSampleId.value) {
    Modal.confirm({
      cancelText: '취소',
      content: '불러온 샘플을 현재 내용으로 덮어씁니다.',
      okText: '수정 저장',
      onOk: overwriteSample,
      title: '샘플 수정',
    });
    return;
  }

  newSampleTitle.value = '';
  sampleSaveOpen.value = true;
}

async function overwriteSample() {
  const target = samples.value.find((s) => s.id === loadedSampleId.value);
  savingSample.value = true;
  try {
    await updateSample(loadedSampleId.value!, {
      content: content.value,
      title: target?.title ?? '샘플',
    });
    message.success('샘플을 수정했습니다.');
    await loadSamples();
  } finally {
    savingSample.value = false;
  }
}

async function saveNewSample() {
  const model = models.value.find((m) => m.mcName === selectedModel.value);
  if (!model) {
    message.warning('모델을 먼저 선택하세요.');
    return;
  }
  if (!newSampleTitle.value.trim()) {
    message.warning('샘플 제목을 입력하세요.');
    return;
  }

  savingSample.value = true;
  try {
    const created = await createSample(model.id, {
      content: content.value,
      title: newSampleTitle.value.trim(),
    });
    loadedSampleId.value = created?.id;
    sampleSaveOpen.value = false;
    message.success('샘플을 저장했습니다.');
    await loadSamples();
  } finally {
    savingSample.value = false;
  }
}

// ── 해석 ──────────────────────────────────────────────────

function buildOptions(text: string) {
  return {
    byteGroup: options.value.byteGroup,
    content: text,
    heads: selectedHeads.value,
    interpretationType: 'HEX',
    isLittleEndian: options.value.isLittleEndian,
    isProtocolMode: protocolMode.value,
    isRxLengthFirst: options.value.isRxLengthFirst,
    model: selectedModel.value,
  };
}

/** 전체 전문을 해석한다. */
async function run() {
  if (!content.value.trim()) {
    message.warning('내용을 입력해 주세요.');
    return;
  }

  loading.value = true;
  try {
    const data = await parseBinary(buildOptions(content.value));
    parsedLines.value = data?.parsedLines ?? [];
    message.success('해석을 완료했습니다.');
  } finally {
    loading.value = false;
  }
}

/**
 * 특정 줄만 다시 해석한다.
 * 원본에서 에디터 줄을 클릭했을 때 오른쪽 패널을 채우던 동작과 같다.
 */
async function analyzeLine(index: number) {
  const line = inputLines.value[index];
  selectedLineIndex.value = index;
  lineAnalysis.value = [];
  if (!line?.trim()) return;

  analyzing.value = true;
  try {
    const data = await parseBinary(buildOptions(line));
    lineAnalysis.value = data?.parsedLines ?? [];
  } finally {
    analyzing.value = false;
  }
}

/** 기본 샘플 전문을 불러온다. 원본과 같은 public/binary_sample.txt 를 쓴다. */
async function loadDefaultSample() {
  try {
    const response = await fetch('/binary_sample.txt');
    if (!response.ok) throw new Error('파일을 불러올 수 없습니다.');
    content.value = await response.text();
    parsedLines.value = [];
    lineAnalysis.value = [];
    selectedLineIndex.value = null;
    loadedSampleId.value = undefined;
    message.success('기본 샘플을 불러왔습니다.');
  } catch {
    message.error('샘플 파일을 읽어오는 데 실패했습니다.');
  }
}

function clearAll() {
  content.value = '';
  parsedLines.value = [];
  lineAnalysis.value = [];
  selectedLineIndex.value = null;
  loadedSampleId.value = undefined;
}

/** 16진 문자열을 두 자리씩 띄워 읽기 쉽게 만든다. */
function formatHex() {
  if (!content.value.trim()) return;

  content.value = content.value
    .split(/\r?\n/)
    .map((line) => {
      const compact = line.replaceAll(/\s+/g, '');
      // 16진수만으로 이뤄진 줄만 재배치한다(RX/TX 같은 머리말이 붙은 줄은 그대로 둔다).
      if (!/^[\da-f]+$/i.test(compact) || compact.length % 2 !== 0) return line;
      return (compact.match(/.{2}/g) ?? []).join(' ').toUpperCase();
    })
    .join('\n');
}

watch(selectedModel, async () => {
  selectedSampleId.value = undefined;
  await loadSamples();
});

onMounted(async () => {
  // 편집기에서 커서를 옮기면 그 줄을 해석한다. 원본(Monaco)의 줄 클릭 동작이다.
  // 자식이 먼저 mount 되므로 이 시점에 인스턴스가 준비되어 있다.
  // 줄을 훑고 지나갈 때마다 서버를 부르지 않도록, 줄이 실제로 바뀌었을 때만 잠깐 뒤에 부른다.
  const editor = editorRef.value?.getEditor?.();
  let cursorTimer: number | undefined;
  editor?.onDidChangeCursorPosition((event: any) => {
    const index = (event?.position?.lineNumber ?? 1) - 1;
    if (index === selectedLineIndex.value) return;
    if (index < 0 || index >= inputLines.value.length) return;
    if (!inputLines.value[index]?.trim()) return;

    window.clearTimeout(cursorTimer);
    cursorTimer = window.setTimeout(() => analyzeLine(index), 250);
  });

  models.value = (await getMcModels()) ?? [];
  selectedModel.value = models.value[0]?.mcName;
  await loadSamples();
});
</script>

<template>
  <Page auto-content-height>
    <!-- 상단 도구 모음 -->
    <Card class="mb-3" size="small">
      <Space wrap size="large">
        <Space>
          <span class="text-sm">모델</span>
          <Select
            v-model:value="selectedModel"
            :options="models.map((m) => ({ label: m.mcName, value: m.mcName }))"
            option-filter-prop="label"
            placeholder="모델 선택"
            show-search
            style="width: 170px"
          />
        </Space>

        <Space>
          <span class="text-sm">샘플</span>
          <Select
            v-model:value="selectedSampleId"
            :options="samples.map((s) => ({ label: s.title, value: s.id }))"
            allow-clear
            option-filter-prop="label"
            placeholder="보관된 샘플"
            show-search
            style="width: 200px"
          />
          <Button :disabled="!selectedSampleId" @click="applySample">적용</Button>
          <Button
            :loading="savingSample"
            :type="loadedSampleId ? 'default' : 'dashed'"
            @click="onSaveSampleClick"
          >
            {{ loadedSampleId ? '수정 저장' : '샘플 저장' }}
          </Button>
        </Space>

        <Space>
          <Tooltip title="16진 문자열을 두 자리씩 띄워 정렬합니다.">
            <Button @click="formatHex">정렬</Button>
          </Tooltip>
          <Button @click="loadDefaultSample">기본 샘플</Button>
          <Button @click="clearAll">초기화</Button>
          <Button :loading="loading" type="primary" @click="run">
            해석 실행
          </Button>
        </Space>
      </Space>
    </Card>

    <!-- 해석 옵션 -->
    <Card class="mb-3" size="small">
      <Space wrap size="large">
        <Space>
          <span class="text-sm">헤더</span>
          <CheckboxGroup v-model:value="selectedHeads" :options="HEAD_OPTIONS" />
        </Space>
        <Space>
          <span class="text-sm">규격 적용</span>
          <Switch v-model:checked="protocolMode" />
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
      </Space>
    </Card>

    <Row :gutter="[12, 12]">
      <!-- 입력 -->
      <Col :lg="8" :xs="24">
        <Card size="small" title="입력">
          <template #extra>
            <span class="text-[11px] text-muted-foreground">
              {{ inputLines.length }}줄
            </span>
          </template>

          <CodeEditor
            ref="editorRef"
            v-model="content"
            :height="240"
            language="plaintext"
            placeholder="16진 전문을 붙여 넣으세요. 여러 줄을 넣을 수 있습니다."
          />

          <!-- 줄 목록: 클릭하면 그 줄만 다시 해석한다 -->
          <div
            v-if="inputLines.length > 0"
            class="mt-2 max-h-[240px] overflow-auto rounded border border-border"
          >
            <button
              v-for="(line, index) in inputLines"
              :key="index"
              class="flex w-full gap-2 border-b border-border px-2 py-1 text-left font-mono text-[11px] last:border-b-0 hover:bg-accent"
              :class="selectedLineIndex === index ? 'bg-accent' : ''"
              type="button"
              @click="analyzeLine(index)"
            >
              <span class="w-8 shrink-0 text-right text-muted-foreground">
                {{ index + 1 }}
              </span>
              <span class="truncate">{{ line || ' ' }}</span>
            </button>
          </div>
        </Card>
      </Col>

      <!-- 전체 해석 결과 -->
      <Col :lg="8" :xs="24">
        <Card size="small" title="전체 해석 결과">
          <Spin :spinning="loading">
            <pre
              class="m-0 max-h-[540px] overflow-auto whitespace-pre-wrap rounded bg-muted/40 p-3 font-mono text-xs"
              >{{ resultText }}</pre
            >
          </Spin>
        </Card>
      </Col>

      <!-- 선택한 줄 상세 -->
      <Col :lg="8" :xs="24">
        <Card size="small">
          <template #title>
            선택 줄 상세
            <Tag v-if="selectedLineIndex !== null" class="ml-1">
              {{ selectedLineIndex + 1 }}번째 줄
            </Tag>
          </template>

          <Spin :spinning="analyzing">
            <Empty
              v-if="selectedLineIndex === null"
              description="왼쪽 줄 목록에서 줄을 선택하면 그 줄만 다시 해석합니다."
            />
            <pre
              v-else
              class="m-0 max-h-[540px] overflow-auto whitespace-pre-wrap rounded bg-muted/40 p-3 font-mono text-xs"
              >{{
                lineAnalysis.length > 0
                  ? lineAnalysis.join('\n')
                  : '해석 결과가 없습니다.'
              }}</pre
            >
          </Spin>
        </Card>
      </Col>
    </Row>

    <!-- 샘플 저장 -->
    <Modal
      v-model:open="sampleSaveOpen"
      :confirm-loading="savingSample"
      cancel-text="취소"
      ok-text="저장"
      title="샘플 저장"
      @ok="saveNewSample"
    >
      <Form layout="vertical">
        <FormItem label="샘플 제목" required>
          <Input
            v-model:value="newSampleTitle"
            placeholder="예: KEPCO 정상 응답"
            @press-enter="saveNewSample"
          />
        </FormItem>
      </Form>
      <span class="text-xs text-muted-foreground">
        현재 선택한 모델({{ selectedModel ?? '-' }}) 아래에 저장됩니다.
      </span>
    </Modal>
  </Page>
</template>
