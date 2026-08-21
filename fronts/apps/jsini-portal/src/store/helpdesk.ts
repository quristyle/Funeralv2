import type {
  Admin,
  Company,
  Customer,
  HelpdeskIdentity,
} from '#/api/helpdesk';

import { computed, ref } from 'vue';

import { defineStore } from 'pinia';

import {
  getAdminList,
  getCompanyList,
  getCustomerList,
  getMyHelpdeskIdentity,
} from '#/api/helpdesk';

/**
 * 헬프데스크 공용 스토어.
 *
 * - 로그인한 funeralv2 계정이 어떤 헬프데스크 사용자로 해석되는지(신원)
 * - 화면 곳곳의 셀렉트에 쓰이는 조직 목록(회사·고객·담당자)
 *
 * 둘 다 여러 화면이 반복해서 필요로 하는데 자주 바뀌지 않아 한 번 받아 캐싱한다.
 */
export const useHelpdeskStore = defineStore('helpdesk', () => {
  const identity = ref<HelpdeskIdentity | null>(null);
  /** 신원 조회를 시도했는지. 연결된 계정이 없는 경우와 아직 안 불러온 경우를 구분한다. */
  const identityChecked = ref(false);

  const admins = ref<Admin[]>([]);
  const companies = ref<Company[]>([]);
  const customers = ref<Customer[]>([]);
  const orgLoaded = ref(false);

  /** 헬프데스크 관리자로 연결된 계정인지 */
  const isAdmin = computed(() => identity.value?.loginType === 'admin');

  /** 헬프데스크 내부 사용자 ID. 연결되지 않았으면 undefined. */
  const helpdeskUserId = computed(() => identity.value?.helpdeskUserId);

  /** 고객으로 연결된 경우의 소속 회사 ID */
  const companyId = computed(() => {
    const raw = identity.value?.companyId;
    return raw === null || raw === undefined || raw === ''
      ? undefined
      : Number(raw);
  });

  /**
   * 현재 계정이 연결된 헬프데스크 사용자를 조회한다.
   * 연결이 없으면 identity 는 null 로 남고 화면에서 안내 문구를 띄운다.
   */
  async function loadIdentity(forceRefresh = false) {
    if (identityChecked.value && !forceRefresh) return identity.value;

    try {
      identity.value = await getMyHelpdeskIdentity();
    } catch {
      // 연결된 헬프데스크 계정이 없는 경우. 화면에서 안내하므로 여기서는 조용히 넘어간다.
      identity.value = null;
    } finally {
      identityChecked.value = true;
    }
    return identity.value;
  }

  /** 조회 조건 셀렉트에 쓰는 조직 목록을 한 번에 받아 캐싱한다. */
  async function loadOrganizations(forceRefresh = false) {
    if (orgLoaded.value && !forceRefresh) return;

    const [adminList, companyList, customerList] = await Promise.all([
      getAdminList(),
      getCompanyList(),
      getCustomerList(),
    ]);

    admins.value = adminList ?? [];
    companies.value = companyList ?? [];
    customers.value = customerList ?? [];
    orgLoaded.value = true;
  }

  /** '전체' 항목이 앞에 붙은 담당자 셀렉트 옵션 */
  const adminOptions = computed(() => [
    { label: '전체', value: null },
    ...admins.value.map((a) => ({ label: a.userName, value: a.id })),
  ]);

  /** '전체' 항목이 앞에 붙은 회사 셀렉트 옵션 */
  const companyOptions = computed(() => [
    { label: '전체', value: null },
    ...companies.value.map((c) => ({ label: c.name, value: c.id })),
  ]);

  /** '전체' 항목이 앞에 붙은 고객 셀렉트 옵션 */
  const customerOptions = computed(() => [
    { label: '전체', value: null },
    ...customers.value.map((c) => ({ label: c.userName, value: c.id })),
  ]);

  function $reset() {
    identity.value = null;
    identityChecked.value = false;
    admins.value = [];
    companies.value = [];
    customers.value = [];
    orgLoaded.value = false;
  }

  return {
    $reset,
    adminOptions,
    admins,
    companies,
    companyId,
    companyOptions,
    customerOptions,
    customers,
    helpdeskUserId,
    identity,
    identityChecked,
    isAdmin,
    loadIdentity,
    loadOrganizations,
    orgLoaded,
  };
});
