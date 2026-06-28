<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenModal } from '@vben/common-ui';
import { message, Form, Input, InputNumber, Select, DatePicker } from 'ant-design-vue';
import { createDeceased, updateDeceased, getRooms } from '#/api/building';
import dayjs from 'dayjs';

const emit = defineEmits<{
  (e: 'saved'): void;
}>();

const rooms = ref<any[]>([]);
const isEditMode = ref<boolean>(false);
const currentId = ref<string>('');

const formModel = ref({
  name: '',
  gender: 'MALE' as 'MALE' | 'FEMALE',
  age: 80,
  religion: '',
  deathDate: '',
  funeralDate: '',
  burialDate: '',
  roomId: '',
  status: 'IN_HOSPITAL' as 'IN_HOSPITAL' | 'DISCHARGED' | 'COMPLETED',
  remark: ''
});

// 날짜 바인딩용
const deathDateVal = ref<any>(null);
const funeralDateVal = ref<any>(null);
const burialDateVal = ref<any>(null);

const [DeceasedModal, deceasedModalApi] = useVbenModal({
  title: '고인 정보 설정',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

// 호실 정보 로드
async function fetchRooms() {
  try {
    const list = await getRooms({});
    rooms.value = list || [];
  } catch (error) {
    message.error('호실 목록을 가져올 수 없습니다.');
  }
}

function open(row?: any) {
  fetchRooms();
  
  if (row) {
    isEditMode.value = true;
    currentId.value = row.id;
    formModel.value = {
      name: row.name || '',
      gender: row.gender || 'MALE',
      age: row.age ?? 80,
      religion: row.religion || '',
      deathDate: row.deathDate || '',
      funeralDate: row.funeralDate || '',
      burialDate: row.burialDate || '',
      roomId: row.roomId || '',
      status: row.status || 'IN_HOSPITAL',
      remark: row.remark || ''
    };
    deathDateVal.value = row.deathDate ? dayjs(row.deathDate) : null;
    funeralDateVal.value = row.funeralDate ? dayjs(row.funeralDate) : null;
    burialDateVal.value = row.burialDate ? dayjs(row.burialDate) : null;
    deceasedModalApi.setState({ title: '고인 정보 수정' });
  } else {
    isEditMode.value = false;
    currentId.value = '';
    formModel.value = {
      name: '',
      gender: 'MALE',
      age: 80,
      religion: '',
      deathDate: '',
      funeralDate: '',
      burialDate: '',
      roomId: '',
      status: 'IN_HOSPITAL',
      remark: ''
    };
    deathDateVal.value = null;
    funeralDateVal.value = null;
    burialDateVal.value = null;
    deceasedModalApi.setState({ title: '신규 고인 등록' });
  }
  deceasedModalApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name) {
      message.warning('고인 성명은 필수 입력 사항입니다.');
      return;
    }

    formModel.value.deathDate = deathDateVal.value ? deathDateVal.value.format('YYYY-MM-DD HH:mm:ss') : '';
    formModel.value.funeralDate = funeralDateVal.value ? funeralDateVal.value.format('YYYY-MM-DD HH:mm:ss') : '';
    formModel.value.burialDate = burialDateVal.value ? burialDateVal.value.format('YYYY-MM-DD HH:mm:ss') : '';

    deceasedModalApi.lock();
    if (isEditMode.value) {
      await updateDeceased(currentId.value, formModel.value);
      message.success('고인 정보가 수정되었습니다.');
    } else {
      await createDeceased(formModel.value);
      message.success('고인 정보가 성공적으로 등록되었습니다.');
    }
    deceasedModalApi.close();
    emit('saved');
  } catch (error) {
    message.error('고인 정보 저장에 실패했습니다.');
  } finally {
    deceasedModalApi.unlock();
  }
}

defineExpose({ open });
</script>

<template>
  <DeceasedModal class="w-[650px]">
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
            <Input v-model:value="formModel.religion" placeholder="예: 기독교, 불교, 천주교, 무교 등" />
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

        <Form.Item label="설명/비고">
          <Input.TextArea v-model:value="formModel.remark" placeholder="특이 사항이나 행정 정보를 기록하십시오." />
        </Form.Item>
      </Form>
    </div>
  </DeceasedModal>
</template>
