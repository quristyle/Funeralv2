<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, message, DatePicker, Select } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getBillingStats } from '#/api/funeral/stat';
import { getCompanyList } from '#/api/portal/system/company';
import dayjs from 'dayjs';

const companies = ref<any[]>([]);
const searchCompanyId = ref<string>('');
const searchDate = ref<any>(dayjs());

// 회사 목록 로드
async function fetchCompanies() {
  try {
    const list = await getCompanyList();
    companies.value = list.items || [];
  } catch (error) {
    message.error('회사 목록 로드 실패');
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'companyName', title: '청구 대상 회사명', minWidth: 150 },
      { field: 'billingMonth', title: '청구 월', minWidth: 100 },
      { field: 'roomUsageCount', title: '빈소 이용 빈도(회)', minWidth: 120, align: 'right' },
      {
        field: 'totalAmount',
        title: '총 청구금액',
        minWidth: 150,
        align: 'right',
        formatter: ({ cellValue }: { cellValue: any }) => `${Number(cellValue).toLocaleString()}원`
      },
      {
        field: 'paymentStatus',
        title: '수납/정산 상태',
        minWidth: 120,
        slots: { default: 'status-tag' }
      },
      { field: 'paymentDate', title: '납부 완료일자', minWidth: 160 }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          const params = {
            companyId: searchCompanyId.value || undefined,
            startDate: searchDate.value ? searchDate.value.startOf('month').format('YYYY-MM-DD') : undefined,
            endDate: searchDate.value ? searchDate.value.endOf('month').format('YYYY-MM-DD') : undefined,
          };
          return await getBillingStats(params);
        },
      },
    },
  },
});

function handleSearch() {
  gridApi.query();
}

// 가상 엑셀 다운로드 트리거
function handleExport() {
  message.loading({ content: '엑셀 파일을 생성하여 변환 중입니다...', key: 'export' });
  setTimeout(() => {
    message.success({ content: '과금_내역_보고서.xlsx 파일 다운로드가 완료되었습니다.', key: 'export', duration: 2 });
  }, 1200);
}

onMounted(() => {
  fetchCompanies();
});
</script>

<template>
  <Page auto-content-height>
    <!-- 검색 영역 -->
    <div class="mb-4 flex flex-wrap items-center justify-between bg-card p-4 rounded border gap-4">
      <div class="flex items-center gap-4 flex-wrap">
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm">청구 월:</span>
          <DatePicker v-model:value="searchDate" picker="month" format="YYYY-MM" style="width: 150px" />
        </div>
        
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm">회사 필터:</span>
          <Select v-model:value="searchCompanyId" style="width: 200px" placeholder="전체 회사 조회" allow-clear>
            <Select.Option v-for="c in companies" :key="c.id" :value="c.id">{{ c.name }}</Select.Option>
          </Select>
        </div>

        <Button type="primary" @click="handleSearch">검색 조회</Button>
      </div>

      <Button v-perm:excel type="default" @click="handleExport">엑셀 다운로드</Button>
    </div>

    <!-- 통계 테이블 -->
    <Grid table-title="월별 정산 및 과금 청구 통계 목록">
      <template #status-tag="{ row }">
        <span
          v-if="row.paymentStatus === 'PAID'"
          class="px-2 py-1 rounded text-xs font-semibold bg-green-100 text-green-800"
        >
          수납 완료
        </span>
        <span
          v-else-if="row.paymentStatus === 'UNPAID'"
          class="px-2 py-1 rounded text-xs font-semibold bg-yellow-100 text-yellow-800"
        >
          미납
        </span>
        <span
          v-else
          class="px-2 py-1 rounded text-xs font-semibold bg-red-100 text-red-800"
        >
          연체 상태
        </span>
      </template>
    </Grid>
  </Page>
</template>
