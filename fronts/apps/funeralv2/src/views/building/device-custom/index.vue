<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Select, Badge } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDevices, createDevice, updateDevice, deleteDevice } from '#/api/building';

const [DeviceModal, deviceModalApi] = useVbenModal({
  title: '장비 정보 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  name: '',
  code: '',
  deviceType: 'DID', // DID, KIOSK, SIGNBOARD 등
  ipAddress: '',
  macAddress: '',
  status: 'UNKNOWN' as 'ONLINE' | 'OFFLINE' | 'UNKNOWN',
  roomId: '',
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '장비명', minWidth: 150 },
      { field: 'code', title: '장비코드', minWidth: 120 },
      {
        field: 'deviceType',
        title: '장비 유형',
        minWidth: 100,
        formatter: ({ cellValue }: { cellValue: any }) => {
          if (cellValue === 'DID') return '안내 모니터(DID)';
          if (cellValue === 'KIOSK') return '무인 키오스크';
          if (cellValue === 'SIGNBOARD') return '호실 현판';
          return cellValue;
        }
      },
      { field: 'ipAddress', title: 'IP 주소', minWidth: 120 },
      { field: 'macAddress', title: 'MAC 주소', minWidth: 150 },
      {
        field: 'status',
        title: '연결 상태',
        minWidth: 120,
        slots: { default: 'status-badge' }
      },
      {
        field: 'action',
        title: '작업',
        width: 220,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getDevices();
        },
      },
    },
  },
});

function onCreate() {
  formModel.value = {
    id: '',
    name: '',
    code: '',
    deviceType: 'DID',
    ipAddress: '',
    macAddress: '',
    status: 'UNKNOWN',
    roomId: '',
  };
  deviceModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  deviceModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteDevice(row.id);
    message.success('장비가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

// 원격 리부팅 제어 Mock
function handleReboot(row: any) {
  message.loading({ content: `${row.name} 장비에 재부팅 명령 송신 중...`, key: 'reboot' });
  setTimeout(() => {
    message.success({ content: '명령 송신 성공. 장비가 곧 리부팅됩니다.', key: 'reboot', duration: 2 });
  }, 1000);
}

async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateDevice(formModel.value.id, formModel.value);
      message.success('장비 정보가 수정되었습니다.');
    } else {
      await createDevice(formModel.value);
      message.success('장비가 성공적으로 등록되었습니다.');
    }
    deviceModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="DID 및 시스템 장비 상태 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 장비 등록
        </Button>
      </template>

      <template #status-badge="{ row }">
        <Badge
          v-if="row.status === 'ONLINE'"
          status="success"
          text="온라인"
        />
        <Badge
          v-else-if="row.status === 'OFFLINE'"
          status="error"
          text="오프라인"
        />
        <Badge
          v-else
          status="default"
          text="상태 미확인"
        />
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Button type="link" size="small" @click="handleReboot(row)">원격 재부팅</Button>
          <Popconfirm title="해당 장비를 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <DeviceModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="장비명" required>
            <Input v-model:value="formModel.name" placeholder="예: 로비 대형 DID, 102호 현판" />
          </Form.Item>
          <Form.Item label="장비코드" required>
            <Input v-model:value="formModel.code" placeholder="예: DID_LOBBY_01" :disabled="!!formModel.id" />
          </Form.Item>
          <Form.Item label="장비 유형">
            <Select v-model:value="formModel.deviceType">
              <Select.Option value="DID">안내 모니터(DID)</Select.Option>
              <Select.Option value="KIOSK">무인 키오스크</Select.Option>
              <Select.Option value="SIGNBOARD">호실 현판</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="IP 주소">
            <Input v-model:value="formModel.ipAddress" placeholder="예: 192.168.1.100" />
          </Form.Item>
          <Form.Item label="MAC 주소">
            <Input v-model:value="formModel.macAddress" placeholder="예: 00:0a:95:9d:68:16" />
          </Form.Item>
        </Form>
      </div>
    </DeviceModal>
  </Page>
</template>
