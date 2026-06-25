<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Button, message, Form, Input, TimePicker, Select, Switch, Slider } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources } from '#/api/building';
import dayjs from 'dayjs';

const audioSources = ref<any[]>([]);
const [ScheduleModal, scheduleModalApi] = useVbenModal({
  title: '방송/배경음악 스케줄 정보 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  name: '',
  audioSourceId: '',
  playTime: '',
  volume: 50,
  isEnabled: true,
  remark: ''
});

const playTimeVal = ref<any>(null);

// 오디오 음원 목록 로드
async function fetchAudioSources() {
  try {
    const list = await getMediaSources('AUDIO');
    audioSources.value = list || [];
  } catch (error) {
    message.error('오디오 음원 목록 로드 실패');
  }
}

// 가상 스케줄 데이터 정의
const mockSchedules = ref([
  { id: '1', name: '아침 기상 방송', audioSourceName: '기상 송 1번.mp3', playTime: '07:00', volume: 40, isEnabled: true, remark: '아침 시작 안내 방송' },
  { id: '2', name: '점심 클래식 배경음', audioSourceName: '클래식 명곡집.mp3', playTime: '12:00', volume: 30, isEnabled: true, remark: '로비 점심 시간용 BGM' },
  { id: '3', name: '저녁 면회 종료 방송', audioSourceName: '종료 알림 송.mp3', playTime: '21:00', volume: 60, isEnabled: false, remark: '면회 시간 종료 예보 방송' }
]);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '스케줄 명칭', minWidth: 150 },
      { field: 'audioSourceName', title: '연결 음원', minWidth: 150 },
      { field: 'playTime', title: '재생 시각', minWidth: 100 },
      { field: 'volume', title: '음량', minWidth: 100, formatter: ({ cellValue }: { cellValue: any }) => `${cellValue}%` },
      {
        field: 'isEnabled',
        title: '동작 여부',
        minWidth: 120,
        slots: { default: 'status-switch' }
      },
      { field: 'remark', title: '설명', minWidth: 200 },
      {
        field: 'action',
        title: '설정',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return mockSchedules.value;
        },
      },
    },
  },
});

function onEdit(row: any) {
  formModel.value = { ...row };
  playTimeVal.value = row.playTime ? dayjs(row.playTime, 'HH:mm') : null;
  scheduleModalApi.open();
}

async function handleSave() {
  try {
    const formattedTime = playTimeVal.value ? playTimeVal.value.format('HH:mm') : '';
    const index = mockSchedules.value.findIndex(item => item.id === formModel.value.id);
    const audioName = audioSources.value.find(a => a.id === formModel.value.audioSourceId)?.name || '선택한 음원.mp3';

    if (index !== -1) {
      mockSchedules.value[index] = {
        ...mockSchedules.value[index]!,
        name: formModel.value.name,
        audioSourceName: audioName,
        playTime: formattedTime,
        volume: formModel.value.volume,
        isEnabled: formModel.value.isEnabled,
        remark: formModel.value.remark
      };
    }
    message.success('음원 방송 일정이 업데이트되었습니다.');
    scheduleModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('스케줄 저장 실패');
  }
}

function handleToggleEnable(row: any) {
  row.isEnabled = !row.isEnabled;
  message.success(`${row.name} 스케줄의 작동 상태가 변경되었습니다.`);
}

onMounted(() => {
  fetchAudioSources();
});
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="관내 음향/방송 스케줄 및 볼륨 관리 목록">
      <template #status-switch="{ row }">
        <Switch :checked="row.isEnabled" @change="handleToggleEnable(row)" />
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
        </div>
      </template>
    </Grid>

    <ScheduleModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="스케줄 명칭" required>
            <Input v-model:value="formModel.name" placeholder="스케줄 성격 묘사" />
          </Form.Item>
          <Form.Item label="방송/재생 음원 연계" required>
            <Select v-model:value="formModel.audioSourceId" placeholder="음원 리소스 선택">
              <Select.Option v-for="a in audioSources" :key="a.id" :value="a.id">{{ a.name }}</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="재생 예약 시간" required>
            <TimePicker v-model:value="playTimeVal" format="HH:mm" style="width: 100%" />
          </Form.Item>
          <Form.Item label="송출 데시벨 볼륨">
            <Slider v-model:value="formModel.volume" :min="0" :max="100" />
            <div class="text-right text-xs text-muted-foreground">{{ formModel.volume }}%</div>
          </Form.Item>
          <Form.Item label="자동 작동 스위치">
            <Switch v-model:value="formModel.isEnabled" />
          </Form.Item>
          <Form.Item label="비고">
            <Input.TextArea v-model:value="formModel.remark" placeholder="스케줄 사용 용도 서술" />
          </Form.Item>
        </Form>
      </div>
    </ScheduleModal>
  </Page>
</template>
