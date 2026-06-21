<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Button, message, Form, Slider, Switch, TimePicker } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDeviceConfigs, updateDeviceConfig } from '#/api/building';
import dayjs from 'dayjs';

const [ConfigModal, configModalApi] = useVbenModal({
  title: '장비 세부 설정 변경',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  deviceId: '',
  deviceName: '',
  volume: 50,
  brightness: 80,
  rebootTime: '',
  isAutoPower: false,
  powerOnTime: '',
  powerOffTime: '',
});

// TimePicker 바인딩용
const powerOnTimeVal = ref<any>(null);
const powerOffTimeVal = ref<any>(null);
const rebootTimeVal = ref<any>(null);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'deviceName', title: '대상 장비명', minWidth: 150 },
      { field: 'volume', title: '음량 (Volume)', minWidth: 100, formatter: ({ cellValue }: { cellValue: any }) => `${cellValue}%` },
      { field: 'brightness', title: '밝기 (Brightness)', minWidth: 100, formatter: ({ cellValue }: { cellValue: any }) => `${cellValue}%` },
      {
        field: 'isAutoPower',
        title: '자동 전원 제어',
        minWidth: 120,
        formatter: ({ cellValue }: { cellValue: any }) => (cellValue ? '사용' : '사용안함')
      },
      { field: 'powerOnTime', title: '켜짐 시각', minWidth: 100 },
      { field: 'powerOffTime', title: '꺼짐 시각', minWidth: 100 },
      { field: 'rebootTime', title: '자동 재시작 시각', minWidth: 120 },
      {
        field: 'action',
        title: '설정',
        width: 100,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getDeviceConfigs();
        },
      },
    },
  },
});

function onEdit(row: any) {
  formModel.value = { ...row };
  powerOnTimeVal.value = row.powerOnTime ? dayjs(row.powerOnTime, 'HH:mm') : null;
  powerOffTimeVal.value = row.powerOffTime ? dayjs(row.powerOffTime, 'HH:mm') : null;
  rebootTimeVal.value = row.rebootTime ? dayjs(row.rebootTime, 'HH:mm') : null;
  configModalApi.open();
}

async function handleSave() {
  try {
    formModel.value.powerOnTime = powerOnTimeVal.value ? powerOnTimeVal.value.format('HH:mm') : '';
    formModel.value.powerOffTime = powerOffTimeVal.value ? powerOffTimeVal.value.format('HH:mm') : '';
    formModel.value.rebootTime = rebootTimeVal.value ? rebootTimeVal.value.format('HH:mm') : '';

    await updateDeviceConfig(formModel.value.id, formModel.value);
    message.success('장비 설정 파라미터가 저장되었습니다.');
    configModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('설정 저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="디바이스 기기별 전원/볼륨/밝기 설정 목록">
      <template #action="{ row }">
        <Button type="link" size="small" @click="onEdit(row)">수정</Button>
      </template>
    </Grid>

    <ConfigModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <div class="font-bold mb-4 text-primary">대상 장비: {{ formModel.deviceName }}</div>
          
          <Form.Item label="기기 음량 (Volume)">
            <Slider v-model:value="formModel.volume" :min="0" :max="100" />
            <div class="text-right text-xs text-muted-foreground">{{ formModel.volume }}%</div>
          </Form.Item>

          <Form.Item label="화면 밝기 (Brightness)">
            <Slider v-model:value="formModel.brightness" :min="0" :max="100" />
            <div class="text-right text-xs text-muted-foreground">{{ formModel.brightness }}%</div>
          </Form.Item>

          <Form.Item label="자동 전원 제어 활성화">
            <Switch v-model:value="formModel.isAutoPower" />
          </Form.Item>

          <div v-if="formModel.isAutoPower" class="grid grid-cols-2 gap-4">
            <Form.Item label="자동 켜짐 시각">
              <TimePicker v-model:value="powerOnTimeVal" format="HH:mm" style="width: 100%" />
            </Form.Item>
            <Form.Item label="자동 꺼짐 시각">
              <TimePicker v-model:value="powerOffTimeVal" format="HH:mm" style="width: 100%" />
            </Form.Item>
          </div>

          <Form.Item label="일일 자동 재시작 스케줄 시각">
            <TimePicker v-model:value="rebootTimeVal" format="HH:mm" style="width: 100%" />
          </Form.Item>
        </Form>
      </div>
    </ConfigModal>
  </Page>
</template>
