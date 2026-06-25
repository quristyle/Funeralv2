<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, InputNumber, Select, DatePicker } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDeceasedList, createDeceased, updateDeceased, deleteDeceased, getRooms } from '#/api/building';
import dayjs from 'dayjs';

const rooms = ref<any[]>([]);
const [DeceasedModal, deceasedModalApi] = useVbenModal({
  title: '고인 정보 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  name: '',
  gender: 'MALE' as 'MALE' | 'FEMALE',
  age: 80,
  religion: '',
  deathDate: '',
  funeralDate: '',
  burialDate: '',
  roomId: '',
  status: 'IN_HOSPITAL' as 'IN_HOSPITAL' | 'DISCHARGED' | 'COMPLETED',
});

// 날짜 바인딩용
const deathDateVal = ref<any>(null);
const funeralDateVal = ref<any>(null);
const burialDateVal = ref<any>(null);

// 호실 정보 로드
async function fetchRooms() {
  try {
    const list = await getRooms({});
    rooms.value = list || [];
  } catch (error) {
    message.error('호실 목록을 가져올 수 없습니다.');
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '고인명', minWidth: 100 },
      {
        field: 'gender',
        title: '성별',
        minWidth: 80,
        formatter: ({ cellValue }: { cellValue: any }) => (cellValue === 'MALE' ? '남성' : '여성')
      },
      { field: 'age', title: '연세', minWidth: 80, formatter: ({ cellValue }: { cellValue: any }) => `${cellValue}세` },
      { field: 'religion', title: '종교', minWidth: 100 },
      { field: 'roomName', title: '배정 빈소', minWidth: 120 },
      { field: 'deathDate', title: '작고 일시', minWidth: 160 },
      {
        field: 'status',
        title: '장례 상태',
        minWidth: 120,
        slots: { default: 'status-tag' }
      },
      {
        field: 'action',
        title: '작업',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getDeceasedList();
        },
      },
    },
  },
});

function onCreate() {
  formModel.value = {
    id: '',
    name: '',
    gender: 'MALE',
    age: 80,
    religion: '',
    deathDate: '',
    funeralDate: '',
    burialDate: '',
    roomId: '',
    status: 'IN_HOSPITAL',
  };
  deathDateVal.value = null;
  funeralDateVal.value = null;
  burialDateVal.value = null;
  deceasedModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  deathDateVal.value = row.deathDate ? dayjs(row.deathDate) : null;
  funeralDateVal.value = row.funeralDate ? dayjs(row.funeralDate) : null;
  burialDateVal.value = row.burialDate ? dayjs(row.burialDate) : null;
  deceasedModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteDeceased(row.id);
    message.success('고인 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    formModel.value.deathDate = deathDateVal.value ? deathDateVal.value.format('YYYY-MM-DD HH:mm:ss') : '';
    formModel.value.funeralDate = funeralDateVal.value ? funeralDateVal.value.format('YYYY-MM-DD HH:mm:ss') : '';
    formModel.value.burialDate = burialDateVal.value ? burialDateVal.value.format('YYYY-MM-DD HH:mm:ss') : '';

    if (formModel.value.id) {
      await updateDeceased(formModel.value.id, formModel.value);
      message.success('고인 정보가 수정되었습니다.');
    } else {
      await createDeceased(formModel.value);
      message.success('고인 정보가 성공적으로 등록되었습니다.');
    }
    deceasedModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchRooms();
});
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="장례식장 고인(Deceased) 등록 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 고인 등록
        </Button>
      </template>

      <template #status-tag="{ row }">
        <span
          v-if="row.status === 'IN_HOSPITAL'"
          class="px-2 py-1 rounded text-xs font-semibold bg-blue-100 text-blue-800"
        >
          장례 진행중
        </span>
        <span
          v-else-if="row.status === 'DISCHARGED'"
          class="px-2 py-1 rounded text-xs font-semibold bg-orange-100 text-orange-800"
        >
          발인 완료
        </span>
        <span
          v-else
          class="px-2 py-1 rounded text-xs font-semibold bg-gray-100 text-gray-800"
        >
          정산 완료
        </span>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 고인 데이터를 영구 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <DeceasedModal @ok="handleSave" class="w-[600px]">
      <div class="p-6">
        <Form layout="vertical">
          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="고인명" required>
              <Input v-model:value="formModel.name" placeholder="고인 성명" />
            </Form.Item>
            <Form.Item label="성별">
              <Select v-model:value="formModel.gender">
                <Select.Option value="MALE">남성</Select.Option>
                <Select.Option value="FEMALE">여성</Select.Option>
              </Select>
            </Form.Item>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="연령/연세" required>
              <InputNumber v-model:value="formModel.age" :min="0" style="width: 100%" />
            </Form.Item>
            <Form.Item label="종교">
              <Input v-model:value="formModel.religion" placeholder="예: 기독교, 불교, 천주교 등" />
            </Form.Item>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="배정 호실(빈소)">
              <Select v-model:value="formModel.roomId" placeholder="빈소 선택">
                <Select.Option v-for="r in rooms" :key="r.id" :value="r.id">{{ r.name }}</Select.Option>
              </Select>
            </Form.Item>
            <Form.Item label="장례 상태">
              <Select v-model:value="formModel.status">
                <Select.Option value="IN_HOSPITAL">장례 진행중</Select.Option>
                <Select.Option value="DISCHARGED">발인 완료</Select.Option>
                <Select.Option value="COMPLETED">정산 완료</Select.Option>
              </Select>
            </Form.Item>
          </div>

          <div class="grid grid-cols-3 gap-4">
            <Form.Item label="작고 일시">
              <DatePicker v-model:value="deathDateVal" show-time format="YYYY-MM-DD HH:mm:ss" style="width: 100%" />
            </Form.Item>
            <Form.Item label="입관 일시">
              <DatePicker v-model:value="funeralDateVal" show-time format="YYYY-MM-DD HH:mm:ss" style="width: 100%" />
            </Form.Item>
            <Form.Item label="발인 일시">
              <DatePicker v-model:value="burialDateVal" show-time format="YYYY-MM-DD HH:mm:ss" style="width: 100%" />
            </Form.Item>
          </div>
        </Form>
      </div>
    </DeceasedModal>
  </Page>
</template>
