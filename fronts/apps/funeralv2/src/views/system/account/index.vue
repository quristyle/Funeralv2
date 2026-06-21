<script lang="ts" setup>
import { ref, onMounted, nextTick } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus, IconifyIcon } from '@vben/icons';
import { Button, message, Popconfirm, Badge, Tooltip } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { useVbenForm } from '#/adapter/form';
import { type SystemAccountApi, getAccounts, createAccount, updateAccount, deleteAccount } from '#/api/system/account';
import { getDeptList } from '#/api/system/dept';
import { $t } from '#/locales';
import { useColumns, useSchema } from './data';

const departments = ref<any[]>([]);
const isUpdate = ref(false);
const currentId = ref('');

// useVbenForm 설정
const [Form, formApi] = useVbenForm({
  schema: useSchema(),
  showDefaultActions: false,
  handleSubmit: handleSave,
});

// useVbenModal 설정
const [AccountModal, accountModalApi] = useVbenModal({
  title: '사용자 계정 정보 설정',
  destroyOnClose: true,
  onCancel() {
    accountModalApi.close();
  },
  onConfirm: async () => {
    await formApi.validateAndSubmitForm();
  },
  onOpenChange(isOpen) {
    if (!isOpen) {
      isUpdate.value = false;
      currentId.value = '';
      formApi.resetForm();
    }
  }
});

// 부서 목록 로드 및 Form Schema 업데이트
async function fetchDepts() {
  try {
    const list = await getDeptList();
    departments.value = list || [];
    
    // Form Schema의 부서 목록 options 업데이트
    formApi.updateSchema([
      {
        fieldName: 'deptId',
        componentProps: {
          options: departments.value.map(d => ({ label: d.name, value: d.id })),
        },
      },
    ]);
  } catch (error) {
    message.error('부서 목록 로드 실패');
  }
}

// Grid 설정
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: useColumns(),
    height: 'auto',
    proxyConfig: {
      response: {
        list: (res: any) => res,
      },
      ajax: {
        query: async () => {
          return await getAccounts();
        },
      },
    },
  },
});

function onCreate() {
  isUpdate.value = false;
  currentId.value = '';
  
  accountModalApi.open();
  
  nextTick(() => {
    formApi.resetForm();
    formApi.updateSchema([
      {
        fieldName: 'loginId',
        componentProps: {
          disabled: false,
        },
      },
    ]);
  });
}

function onEdit(row: any) {
  isUpdate.value = true;
  currentId.value = row.id;
  
  accountModalApi.open();
  
  nextTick(() => {
    formApi.setValues(row);
    formApi.updateSchema([
      {
        fieldName: 'loginId',
        componentProps: {
          disabled: true,
        },
      },
    ]);
  });
}

const getPopupContainer = () => document.body;

async function onDelete(row: any) {
  try {
    await deleteAccount(row.id);
    message.success('계정이 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave(values: Record<string, any>) {
  try {
    const dept = departments.value.find(d => d.id === values.deptId);
    const postData: Omit<SystemAccountApi.Account, 'id' | 'createdAt'> = {
      loginId: values.loginId || '',
      userName: values.userName || '',
      status: values.status || 'ACTIVE',
      email: values.email,
      phone: values.phone,
      deptId: values.deptId,
      deptName: dept?.name || '',
    };

    if (isUpdate.value && currentId.value) {
      await updateAccount(currentId.value, postData);
      message.success('계정 정보가 변경되었습니다.');
    } else {
      await createAccount(postData);
      message.success('신규 계정이 등록되었습니다.');
    }
    accountModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchDepts();
});
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="어드민 사용자 계정 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 계정 등록
        </Button>
      </template>

      <template #status-tag="{ row }">
        <Badge
          v-if="row.status === 'ACTIVE'"
          status="success"
          text="정상 작동"
        />
        <Badge
          v-else-if="row.status === 'LOCKED'"
          status="warning"
          text="일시 잠금"
        />
        <Badge
          v-else
          status="error"
          text="비활성화"
        />
      </template>

      <template #action="{ row }">
        <div class="flex justify-center gap-2">
          <!-- 수정 버튼: Tooltip 및 Icon 사용 -->
          <Tooltip :title="$t('common.edit')">
            <Button type="link" size="small" @click="onEdit(row)">
              <template #icon>
                <IconifyIcon class="size-4" icon="lucide:edit" />
              </template>
            </Button>
          </Tooltip>
          
          <!-- 삭제 버튼: Popconfirm을 통한 확인 절차 포함 -->
          <Popconfirm
            :get-popup-container="getPopupContainer"
            placement="topLeft"
            :title="$t('ui.actionMessage.deleteConfirm', [row.userName])"
            @confirm="onDelete(row)"
          >
            <Tooltip :title="$t('common.delete')">
              <Button type="link" size="small" danger>
                <template #icon>
                  <IconifyIcon class="size-4" icon="lucide:trash-2" />
                </template>
              </Button>
            </Tooltip>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <AccountModal>
      <div class="p-6">
        <Form />
      </div>
    </AccountModal>
  </Page>
</template>

