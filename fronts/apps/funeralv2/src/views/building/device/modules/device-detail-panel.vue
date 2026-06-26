<script lang="ts" setup>
import { Badge, Button, Tabs, Tooltip } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import { getDeviceTypeInfo } from '../constants/device-type';
import DeviceConfigTab from './device-config-tab.vue';
import DeviceAttributeTab from './device-attribute-tab.vue';
import type { BuildingApi } from '#/api/building';

const props = defineProps<{
  device: BuildingApi.Device;
  activeTab: string;
  deviceConfig: BuildingApi.DeviceConfig | null;
  configLoading: boolean;
  configSaving: boolean;
  powerOnTimeVal: any;
  powerOffTimeVal: any;
  rebootTimeVal: any;
  deviceAttr: BuildingApi.DeviceAttribute | null;
  attrLoading: boolean;
  attrSaving: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'update:activeTab', val: string): void;
  (e: 'configSave'): void;
  (e: 'configReload'): void;
  (e: 'update:powerOnTimeVal', val: any): void;
  (e: 'update:powerOffTimeVal', val: any): void;
  (e: 'update:rebootTimeVal', val: any): void;
  (e: 'attrSave'): void;
  (e: 'attrReset'): void;
}>();
</script>

<template>
  <div class="flex h-full flex-col rounded-lg border border-border bg-card shadow-sm">
    <!-- 패널 헤더 -->
    <div class="flex shrink-0 items-center justify-between rounded-t-lg border-b border-border bg-muted/40 px-4 py-2">
      <div class="flex min-w-0 items-center gap-2">
        <IconifyIcon
          :icon="getDeviceTypeInfo(device.deviceType).icon"
          class="size-5 shrink-0 text-primary"
        />
        <div class="min-w-0">
          <div class="flex items-center gap-2">
            <span class="truncate text-sm font-semibold">{{ device.name }}</span>
            <span class="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary">
              {{ getDeviceTypeInfo(device.deviceType).label }}
            </span>
          </div>
          <div class="truncate text-xs text-muted-foreground">
            {{ device.code }}
            <span v-if="device.ipAddress"> · {{ device.ipAddress }}</span>
          </div>
        </div>
      </div>
      <div class="flex shrink-0 items-center gap-2">
        <Badge v-if="device.status === 'ONLINE'" status="success" text="온라인" />
        <Badge v-else-if="device.status === 'OFFLINE'" status="error" text="오프라인" />
        <Badge v-else status="default" text="미확인" />
        <Tooltip title="패널 닫기">
          <Button type="text" size="small" @click="emit('close')">
            <IconifyIcon icon="lucide:x" class="size-4" />
          </Button>
        </Tooltip>
      </div>
    </div>

    <!-- 탭 영역 -->
    <Tabs
      :activeKey="activeTab"
      size="small"
      class="flex min-h-0 flex-1 flex-col device-tabs"
      :tab-bar-style="{ margin: 0, paddingLeft: '12px', paddingRight: '12px', flexShrink: 0 }"
      @update:activeKey="(val) => emit('update:activeTab', val as string)"
    >
      <!-- 탭1: 기본 설정 -->
      <Tabs.TabPane key="config">
        <template #tab>
          <span class="flex items-center gap-1.5">
            <IconifyIcon icon="lucide:settings-2" class="size-3.5" />
            기본 설정
          </span>
        </template>
        <DeviceConfigTab
          :device-config="deviceConfig"
          :config-loading="configLoading"
          :config-saving="configSaving"
          :power-on-time-val="powerOnTimeVal"
          :power-off-time-val="powerOffTimeVal"
          :reboot-time-val="rebootTimeVal"
          :device-id="device.id"
          @save="emit('configSave')"
          @reload="emit('configReload')"
          @update:powerOnTimeVal="(val) => emit('update:powerOnTimeVal', val)"
          @update:powerOffTimeVal="(val) => emit('update:powerOffTimeVal', val)"
          @update:rebootTimeVal="(val) => emit('update:rebootTimeVal', val)"
        />
      </Tabs.TabPane>

      <!-- 탭2: 장비 속성 -->
      <Tabs.TabPane key="attribute">
        <template #tab>
          <span class="flex items-center gap-1.5">
            <IconifyIcon icon="lucide:sliders-horizontal" class="size-3.5" />
            장비 속성
          </span>
        </template>
        <DeviceAttributeTab
          :device-attr="deviceAttr"
          :attr-loading="attrLoading"
          :attr-saving="attrSaving"
          @save="emit('attrSave')"
          @reset="emit('attrReset')"
        />
      </Tabs.TabPane>
    </Tabs>
  </div>
</template>

<style scoped>
:deep(.device-tabs .ant-tabs-content-holder) {
  display: flex;
  flex-direction: column;
  min-height: 0;
  flex: 1;
  overflow: hidden;
}
:deep(.device-tabs .ant-tabs-content) {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
:deep(.device-tabs .ant-tabs-tabpane) {
  height: 100%;
  display: flex;
  flex-direction: column;
}
</style>
