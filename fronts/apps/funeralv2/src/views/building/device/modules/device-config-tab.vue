<script lang="ts" setup>
import {
  Button, Divider, Empty, Form, Slider, Spin, Switch, TimePicker,
} from 'ant-design-vue';
import type { BuildingApi } from '#/api/building';

const props = defineProps<{
  deviceConfig: BuildingApi.DeviceConfig | null;
  configLoading: boolean;
  configSaving: boolean;
  powerOnTimeVal: any;
  powerOffTimeVal: any;
  rebootTimeVal: any;
  deviceId: string;
}>();

const emit = defineEmits<{
  (e: 'save'): void;
  (e: 'reload'): void;
  (e: 'update:powerOnTimeVal', val: any): void;
  (e: 'update:powerOffTimeVal', val: any): void;
  (e: 'update:rebootTimeVal', val: any): void;
}>();
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- 로딩 -->
    <div v-if="configLoading" class="flex flex-1 items-center justify-center py-16">
      <Spin tip="설정 불러오는 중..." />
    </div>
    <!-- 설정 없음 -->
    <div v-else-if="!deviceConfig" class="flex flex-1 items-center justify-center py-10">
      <Empty description="등록된 기본 설정이 없습니다." />
    </div>
    <!-- 설정 폼 -->
    <div v-else class="flex-1 overflow-auto px-4 py-3">
      <Form layout="vertical" size="small">
        <Form.Item label="기기 음량 (Volume)">
          <Slider v-model:value="deviceConfig.volume" :min="0" :max="100" />
          <div class="text-right text-xs text-muted-foreground">{{ deviceConfig.volume }}%</div>
        </Form.Item>
        <Form.Item label="화면 밝기 (Brightness)">
          <Slider v-model:value="deviceConfig.brightness" :min="0" :max="100" />
          <div class="text-right text-xs text-muted-foreground">{{ deviceConfig.brightness }}%</div>
        </Form.Item>
        <Divider />
        <Form.Item label="자동 전원 제어">
          <Switch
            v-model:checked="deviceConfig.isAutoPower"
            checked-children="사용"
            un-checked-children="사용안함"
          />
        </Form.Item>
        <template v-if="deviceConfig.isAutoPower">
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="자동 켜짐 시각">
              <TimePicker
                :value="powerOnTimeVal"
                format="HH:mm"
                style="width: 100%"
                @change="(val) => emit('update:powerOnTimeVal', val)"
              />
            </Form.Item>
            <Form.Item label="자동 꺼짐 시각">
              <TimePicker
                :value="powerOffTimeVal"
                format="HH:mm"
                style="width: 100%"
                @change="(val) => emit('update:powerOffTimeVal', val)"
              />
            </Form.Item>
          </div>
        </template>
        <Divider />
        <Form.Item label="일일 자동 재시작 시각">
          <TimePicker
            :value="rebootTimeVal"
            format="HH:mm"
            style="width: 100%"
            @change="(val) => emit('update:rebootTimeVal', val)"
          />
        </Form.Item>
      </Form>
    </div>
    <!-- 저장 버튼 -->
    <div
      v-if="deviceConfig && !configLoading"
      class="flex shrink-0 justify-end gap-2 border-t border-border bg-muted/40 px-4 py-2"
    >
      <Button @click="emit('reload')">초기화</Button>
      <Button type="primary" :loading="configSaving" @click="emit('save')">
        설정 저장
      </Button>
    </div>
  </div>
</template>
