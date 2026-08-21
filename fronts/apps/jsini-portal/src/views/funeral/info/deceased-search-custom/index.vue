<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, message, Form, Input, Select, DatePicker, Card, Descriptions } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDeceasedList } from '#/api/funeral/building';
import dayjs from 'dayjs';

const searchName = ref<string>('');
const searchGender = ref<string>('');
const searchRange = ref<[any, any]>([dayjs().subtract(1, 'year'), dayjs()]);
const detailRecord = ref<any>(null);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '고인 성명', minWidth: 100 },
      {
        field: 'gender',
        title: '성별',
        minWidth: 80,
        formatter: ({ cellValue }: { cellValue: any }) => (cellValue === 'MALE' ? '남성' : '여성')
      },
      { field: 'age', title: '연세', minWidth: 80, formatter: ({ cellValue }: { cellValue: any }) => `${cellValue}세` },
      { field: 'deathDate', title: '작고 일자', minWidth: 150 },
      { field: 'burialDate', title: '발인 일자', minWidth: 150 },
      { field: 'religion', title: '종교', minWidth: 100 },
      {
        field: 'action',
        title: '동작',
        width: 120,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          const list = await getDeceasedList();
          // 조건부 필터링
          return list.filter(item => {
            const matchesName = searchName.value ? item.name.includes(searchName.value) : true;
            const matchesGender = searchGender.value ? item.gender === searchGender.value : true;
            return matchesName && matchesGender;
          });
        },
      },
    },
  },
});

function handleSearch() {
  gridApi.query();
}

function handleRowClick(row: any) {
  detailRecord.value = row;
}

function handlePrint() {
  if (!detailRecord.value) return;
  message.info(`${detailRecord.value.name} 고인의 장례 증명서 인쇄 명령이 송출되었습니다.`);
}
</script>

<template>
  <Page auto-content-height>
    <!-- 검색 조건 카드 -->
    <div class="mb-4 bg-card p-4 rounded border">
      <Form layout="inline" class="flex flex-wrap gap-4">
        <Form.Item label="고인 성명">
          <Input v-model:value="searchName" placeholder="고인 이름 입력" style="width: 150px" />
        </Form.Item>
        
        <Form.Item label="성별">
          <Select v-model:value="searchGender" style="width: 120px" placeholder="전체">
            <Select.Option value="">전체 성별</Select.Option>
            <Select.Option value="MALE">남성</Select.Option>
            <Select.Option value="FEMALE">여성</Select.Option>
          </Select>
        </Form.Item>

        <Form.Item label="작고 범위">
          <DatePicker.RangePicker v-model:value="searchRange" format="YYYY-MM-DD" style="width: 250px" />
        </Form.Item>

        <Button type="primary" @click="handleSearch">검색 필터 적용</Button>
      </Form>
    </div>

    <div class="grid grid-cols-3 gap-4 h-full">
      <!-- 좌측: 목록 -->
      <div class="col-span-2">
        <Grid table-title="종합 장례 대장 목록">
          <template #action="{ row }">
            <Button type="link" size="small" @click="handleRowClick(row)">상세 대장 보기</Button>
          </template>
        </Grid>
      </div>

      <!-- 우측: 장례 상세 대장 카드 -->
      <Card title="상세 장례 대장 증명" class="col-span-1 h-full flex flex-col">
        <div v-if="detailRecord" class="space-y-4">
          <Descriptions title="기본 인적사항" :column="1" bordered size="small">
            <Descriptions.Item label="성명">{{ detailRecord.name }}</Descriptions.Item>
            <Descriptions.Item label="성별/연세">{{ detailRecord.gender === 'MALE' ? '남성' : '여성' }} / {{ detailRecord.age }}세</Descriptions.Item>
            <Descriptions.Item label="종교">{{ detailRecord.religion || '없음' }}</Descriptions.Item>
          </Descriptions>

          <Descriptions title="장례 일정 이력" :column="1" bordered size="small" class="mt-4">
            <Descriptions.Item label="작고 일자">{{ detailRecord.deathDate }}</Descriptions.Item>
            <Descriptions.Item label="입관 일자">{{ detailRecord.funeralDate || '미지정' }}</Descriptions.Item>
            <Descriptions.Item label="발인 일자">{{ detailRecord.burialDate || '미지정' }}</Descriptions.Item>
            <Descriptions.Item label="배정 빈소">{{ detailRecord.roomName || '대기' }}</Descriptions.Item>
          </Descriptions>

          <Button type="primary" block class="mt-6" @click="handlePrint">장례 증명서 인쇄</Button>
        </div>
        <div v-else class="text-center py-20 text-muted-foreground text-xs">
          목록에서 고인을 선택하여 상세 대장을 불러오세요.
        </div>
      </Card>
    </div>
  </Page>
</template>
