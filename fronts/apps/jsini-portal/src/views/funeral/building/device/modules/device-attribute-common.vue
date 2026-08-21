<script lang="ts" setup>
import {
  Form, Input, InputNumber, Select, Slider, Switch, Divider,
} from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import type { BuildingApi } from '#/api/funeral/building';

defineProps<{
  deviceAttr: BuildingApi.DeviceAttribute;
}>();
</script>

<template>
  <!-- ① 공통 표시 설정 -->
  <div class="attr-section">
    <div class="attr-section-title">
      <IconifyIcon icon="lucide:monitor-cog" class="size-4" />
      <span>공통 표시 설정</span>
    </div>
    <div class="grid grid-cols-2 gap-3">
      <Form.Item label="화면 방향">
        <Select v-model:value="deviceAttr.displayOrientation" style="width: 100%">
          <Select.Option value="LANDSCAPE">가로 (Landscape)</Select.Option>
          <Select.Option value="PORTRAIT">세로 (Portrait)</Select.Option>
        </Select>
      </Form.Item>
      <Form.Item label="화면 표현">
        <Select v-model:value="deviceAttr.portraitOrientation" style="width: 100%">
          <Select.Option value="HORIZONTAL">가로형 모습</Select.Option>
          <Select.Option value="VERTICAL_LEFT">좌90도 세로형 모습</Select.Option>
          <Select.Option value="VERTICAL_RIGHT">우90도 세로형 모습</Select.Option>
          <Select.Option value="INVERTED">뒤집기 모습</Select.Option>
        </Select>
      </Form.Item>
    </div>
    <div class="grid grid-cols-2 gap-3 mt-2">
      <Form.Item label="전체 화면 여백 (위)">
        <div class="flex items-center gap-3">
          <Slider v-model:value="deviceAttr.displayPaddingTop" :min="0" :max="40" class="flex-1" />
          <InputNumber v-model:value="deviceAttr.displayPaddingTop" :min="0" :max="40" addon-after="%" style="width: 85px" />
        </div>
      </Form.Item>
      <Form.Item label="전체 화면 여백 (아래)">
        <div class="flex items-center gap-3">
          <Slider v-model:value="deviceAttr.displayPaddingBottom" :min="0" :max="40" class="flex-1" />
          <InputNumber v-model:value="deviceAttr.displayPaddingBottom" :min="0" :max="40" addon-after="%" style="width: 85px" />
        </div>
      </Form.Item>
    </div>
    <div class="grid grid-cols-2 gap-3">
      <Form.Item label="전체 화면 여백 (좌)">
        <div class="flex items-center gap-3">
          <Slider v-model:value="deviceAttr.displayPaddingLeft" :min="0" :max="40" class="flex-1" />
          <InputNumber v-model:value="deviceAttr.displayPaddingLeft" :min="0" :max="40" addon-after="%" style="width: 85px" />
        </div>
      </Form.Item>
      <Form.Item label="전체 화면 여백 (우)">
        <div class="flex items-center gap-3">
          <Slider v-model:value="deviceAttr.displayPaddingRight" :min="0" :max="40" class="flex-1" />
          <InputNumber v-model:value="deviceAttr.displayPaddingRight" :min="0" :max="40" addon-after="%" style="width: 85px" />
        </div>
      </Form.Item>
    </div>
    <div class="grid grid-cols-2 gap-3">
      <Form.Item label="콘텐츠 전환 간격(초)">
        <InputNumber
          v-model:value="deviceAttr.contentIntervalSec"
          :min="3"
          :max="300"
          style="width: 100%"
          addon-after="초"
        />
      </Form.Item>
    </div>
    <div class="grid grid-cols-2 gap-3">
      <Form.Item label="대기 화면(스크린세이버)">
        <Switch
          v-model:checked="deviceAttr.isScreensaverEnabled"
          checked-children="사용"
          un-checked-children="사용안함"
        />
      </Form.Item>
      <Form.Item v-if="deviceAttr.isScreensaverEnabled" label="대기 전환 시간(초)">
        <InputNumber
          v-model:value="deviceAttr.screensaverTimeoutSec"
          :min="30"
          :max="3600"
          style="width: 100%"
          addon-after="초"
        />
      </Form.Item>
    </div>
  </div>

  <Divider />

  <!-- ⑥ 비고 -->
  <div class="attr-section">
    <div class="attr-section-title">
      <IconifyIcon icon="lucide:file-text" class="size-4" />
      <span>비고</span>
    </div>
    <Form.Item>
      <Input.TextArea
        v-model:value="deviceAttr.remark"
        :rows="2"
        :maxlength="500"
        show-count
        placeholder="기타 메모 사항을 입력하세요."
      />
    </Form.Item>
  </div>
</template>

<style scoped>
.attr-section {
  margin-bottom: 4px;
}

.attr-section-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.8125rem;
  font-weight: 600;
  color: hsl(var(--foreground));
  margin-bottom: 12px;
}
</style>
