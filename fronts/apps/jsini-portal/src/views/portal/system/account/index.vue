<script lang="ts" setup>
import { ref, onMounted, nextTick } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus, IconifyIcon } from '@vben/icons';
import { Avatar, Button, message, Popconfirm, Badge, Tooltip, Tag } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { useVbenForm } from '#/adapter/form';
import { type SystemAccountApi, getAccounts, createAccount, updateAccount, deleteAccount } from '#/api/portal/system/account';
import { getDeptList } from '#/api/portal/system/dept';
import {
  loadMsaUserDirectory,
  matchAccount,
  type MsaUserDirectory,
} from '#/api/portal/system/msa-users';
import { getRoleList } from '#/api/portal/system/role';
import { $t } from '#/locales';
import { avatarInitial, avatarStyle, avatarThumbUrl } from '#/utils/avatar';

import { useColumns, useSchema } from './data';

const departments = ref<any[]>([]);
const roles = ref<any[]>([]);
const isUpdate = ref(false);
const currentId = ref('');

// useVbenForm 설정
const [Form, formApi] = useVbenForm({
  schema: useSchema(),
  showDefaultActions: false,
  handleSubmit: handleSave,
});

// 역할 목록 로드 및 Form Schema 업데이트
async function fetchRoles() {
  try {
    const response = await getRoleList({});
    const list = (response as any)?.result ?? response;
    roles.value = Array.isArray(list) ? list : [];
    
    formApi.updateSchema([
      {
        fieldName: 'roleIds',
        componentProps: {
          options: roles.value.map(r => ({ label: r.name, value: r.id })),
        },
      },
    ]);
  } catch (error) {
    message.error('역할 목록 로드 실패');
  }
}

// useVbenModal 설정
const [AccountModal, accountModalApi] = useVbenModal<Record<string, any>>({
  title: '사용자 계정 정보 설정',
  destroyOnClose: true,
  onCancel() {
    accountModalApi.close();
  },
  onConfirm: async () => {
    await formApi.validateAndSubmitForm();
  },
  onOpenChange: async (isOpen) => {
    if (isOpen) {
      await fetchRoles();
      const data = accountModalApi.getData();
      if (data?.id) {
        isUpdate.value = true;
        currentId.value = data.id;
        
        await nextTick();
        
        const dept = departments.value.find(d => d.id === data.deptId);
        const formValues = {
          ...data,
          companyId: dept?.companyId || '',
          roleIds: data.roleIds || [],
        };
        formApi.setValues(formValues);
        formApi.updateSchema([
          {
            fieldName: 'loginId',
            componentProps: {
              disabled: true,
            },
          },
        ]);
      } else {
        isUpdate.value = false;
        currentId.value = '';
        
        await nextTick();
        
        formApi.resetForm();
        formApi.updateSchema([
          {
            fieldName: 'loginId',
            componentProps: {
              disabled: false,
            },
          },
        ]);
      }
    } else {
      isUpdate.value = false;
      currentId.value = '';
      formApi.resetForm();
    }
  }
});

// 부서 목록 로드 및 Form Schema 업데이트
async function fetchDepts() {
  try {
    const response = await getDeptList();
    const list = (response as any)?.result ?? response;
    departments.value = Array.isArray(list) ? list : [];
  } catch (error) {
    message.error('부서 목록 로드 실패');
  }
}

/** MSA 사용자 원본. 열에서 조회 실패 사유를 보여 줄 때도 쓴다. */
const msaDirectory = ref<MsaUserDirectory | null>(null);

// Grid 설정
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: useColumns(),
    height: 'auto',
    // ── 정렬·필터는 화면에서 한다 ──────────────────────────
    //
    // `remote: false` 를 **분명히 적어 둔다.** proxyConfig 를 함께 쓰면 vxe 가
    // 정렬·필터를 서버에 다시 물어보려 할 수 있는데, 이 화면의 query 는 인자를
    // 받지 않고 전체를 내려주므로 그러면 아무 일도 일어나지 않는다.
    // 계정 수가 수십 건이라 받아 둔 데이터를 화면에서 다루는 것이 제일 빠르다.
    sortConfig: {
      // 여러 칸으로 줄 세우기. '회사 → 이름' 처럼 묶어 보는 일이 많다.
      multiple: true,
      remote: false,
    },
    filterConfig: {
      remote: false,
    },
    proxyConfig: {
      response: {
        list: (res: any) => res,
      },
      ajax: {
        query: async () => {
          const accounts = (await getAccounts()) ?? [];

          // 헬프데스크·프로젝트관리에서 사용자 목록을 읽어 계정과 맞춰 본다.
          // 두 시스템의 DB 는 건드리지 않는다 — API 로 읽기만 한다.
          // 한쪽이 죽어 있어도 계정 목록 자체는 그대로 뜬다.
          try {
            msaDirectory.value = await loadMsaUserDirectory();
          } catch {
            msaDirectory.value = null;
          }

          const dir = msaDirectory.value;
          const rows = accounts.map((row) => ({
            ...row,
            msa: dir ? matchAccount(row, dir) : undefined,
          }));

          // vxe 그리드의 프록시는 페이징을 쓸 때 `result` / `page.total` 을 읽는다.
          // 배열을 그대로 돌려주면 `result` 가 없어 0 건으로 표시된다.
          return { page: { total: rows.length }, result: rows };
        },
      },
    },
  },
});

function onCreate() {
  accountModalApi.setData({}).open();
}

function onEdit(row: any) {
  accountModalApi.setData(row).open();
}

/** 대조된 MSA 사용자에 마우스를 올렸을 때 보여 줄 상세 */
function msaTitle(u?: { belongTo?: string; email?: string; loginId?: string }) {
  if (!u) return '';
  return [u.loginId && `아이디 ${u.loginId}`, u.email, u.belongTo]
    .filter(Boolean)
    .join(' · ');
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
      roleIds: values.roleIds || [],
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
  fetchRoles();
});
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="어드민 사용자 계정 목록">
      <template #toolbar-tools>
        <Button v-perm:create type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 계정 등록
        </Button>
      </template>

      <!--
        얼굴 + 이름. 사진이 없으면 이름 첫 글자로 그린다 —
        43명 중 사진이 있는 사람은 한 명뿐이라 **없는 쪽이 정상**이고,
        빈 동그라미를 두면 목록에서 사람이 구분되지 않는다.
      -->
      <template #user-name="{ row }">
        <div class="flex items-center gap-2">
          <Avatar
            :src="avatarThumbUrl(row.avatar)"
            class="shrink-0"
            :style="avatarStyle(row.userName, !!row.avatar, 1.5)"
          >
            {{ avatarInitial(row.userName) }}
          </Avatar>
          <span class="truncate">{{ row.userName }}</span>
        </div>
      </template>

      <template #role-tag="{ row }">
        <div class="flex flex-wrap gap-1">
          <Tag
            v-for="roleName in row.roleNames"
            :key="roleName"
            color="blue"
          >
            {{ roleName }}
          </Tag>
        </div>
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

      <!--
        MSA 사용자 대조.
        '연결됨' 은 관리자가 연결 테이블에 직접 이어 둔 것이고,
        '추정' 은 로그인 아이디·이메일이 같아 그렇게 보이는 것이다.
        같은 아이디를 쓰는 다른 사람일 수 있어 구분해서 보여 준다.
      -->
      <template #msa-helpdesk="{ row }">
        <span
          v-if="msaDirectory?.helpdesk.error"
          class="text-xs text-muted-foreground"
          :title="msaDirectory.helpdesk.error"
        >
          확인 불가
        </span>
        <div v-else-if="row.msa?.helpdesk" class="flex items-center gap-1">
          <Tag :color="row.msa.helpdeskLinked ? 'success' : 'default'">
            {{ row.msa.helpdeskLinked ? '연결됨' : '추정' }}
          </Tag>
          <span class="truncate text-xs" :title="msaTitle(row.msa.helpdesk)">
            {{ row.msa.helpdesk.name }}
            <span v-if="row.msa.helpdesk.kind" class="text-muted-foreground">
              ({{ row.msa.helpdesk.kind }})
            </span>
          </span>
        </div>
        <span v-else class="text-xs text-muted-foreground">—</span>
      </template>

      <template #msa-projmng="{ row }">
        <span
          v-if="msaDirectory?.projmng.error"
          class="text-xs text-muted-foreground"
          :title="msaDirectory.projmng.error"
        >
          확인 불가
        </span>
        <div v-else-if="row.msa?.projmng" class="flex items-center gap-1">
          <Tag>추정</Tag>
          <span class="truncate text-xs" :title="msaTitle(row.msa.projmng)">
            {{ row.msa.projmng.name }}
          </span>
        </div>
        <span v-else class="text-xs text-muted-foreground">—</span>
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

