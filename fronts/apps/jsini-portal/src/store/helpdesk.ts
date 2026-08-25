import type { BizOption } from '#/api/biz-select';
import type {
  Admin,
  Company,
  Customer,
  HelpdeskIdentity,
} from '#/api/helpdesk';

import { computed, ref } from 'vue';

import { defineStore } from 'pinia';

import { fetchBizOptions } from '#/api/biz-select';
import { getMyHelpdeskIdentity } from '#/api/helpdesk';

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

  // 메타데이터가 지정한 라벨/값 필드로 이미 매핑된 셀렉트 옵션.
  const adminOpts = ref<BizOption[]>([]);
  const companyOpts = ref<BizOption[]>([]);
  const customerOpts = ref<BizOption[]>([]);

  /**
   * 담당자 권한이 있는가.
   *
   * 서버가 포털 역할까지 보고 판정한 값(`isAdmin`)을 쓴다. 전에는 `loginType === 'admin'`,
   * 즉 **계정 연결이 담당자인 경우만** 참이었다. 포털에서 관리자 역할을 받은 계정도
   * 연결이 없으면 거짓이 되어 관리 화면이 열리지 않았다.
   */
  const isAdmin = computed(
    () => identity.value?.isAdmin ?? identity.value?.loginType === 'admin',
  );

  /**
   * 헬프데스크 내부 사용자 ID. 연결되지 않았으면 undefined.
   *
   * 이 값은 **'내 것'을 가리킬 때만** 쓴다(내가 쓴 댓글, 나에게 배정된 요청, 내 알림).
   * 화면을 열지 말지는 {@link isAdmin} 으로 판단한다.
   */
  const helpdeskUserId = computed(() => identity.value?.helpdeskUserId ?? undefined);

  /** 헬프데스크 내부 레코드에 이어져 있는가 */
  const isLinked = computed(() => Boolean(identity.value?.helpdeskUserId));

  /** 담당자 권한은 있으나 연결이 없는 상태. '내 것' 을 가리키는 기능만 못 쓴다. */
  const isUnlinkedAdmin = computed(() => isAdmin.value && !isLinked.value);

  /**
   * 헬프데스크 업무 화면을 열 수 있는가.
   *
   * 담당자 권한이 있으면 연결이 없어도 조회·관리를 한다. 고객은 연결이 있어야
   * 어느 회사 사람인지 정해지므로 연결이 필요하다.
   *
   * 화면을 열지 말지는 이 값으로 판단하고, **`helpdeskUserId` 로 판단하지 않는다.**
   * 그렇게 하면 연결 없는 관리자에게 빈 화면이 나온다.
   */
  const canUse = computed(() => isAdmin.value || isLinked.value);

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

  /**
   * 조회 조건 셀렉트에 쓰는 조직 목록을 한 번에 받아 캐싱한다.
   *
   * 어느 API 를 부르는지는 여기 없다 — 포털·장례식장 셀렉트와 같은 통로를 쓴다.
   * DB 메타데이터(`scom.biz_select_configs` 의 `helpdesk_admin` · `helpdesk_company`
   * · `helpdesk_customer`)가 MSA·경로·라벨/값 필드를 정한다.
   *
   * 목록(`admins`/`companies`/`customers`)과 셀렉트 옵션을 둘 다 들고 있는 이유:
   * 계정 대조 화면처럼 라벨·값 말고 다른 컬럼까지 필요한 곳이 있다.
   */
  async function loadOrganizations(forceRefresh = false) {
    if (orgLoaded.value && !forceRefresh) return;

    const [adminRes, companyRes, customerRes] = await Promise.all([
      fetchBizOptions('helpdesk_admin'),
      fetchBizOptions('helpdesk_company'),
      fetchBizOptions('helpdesk_customer'),
    ]);

    admins.value = adminRes.items as Admin[];
    companies.value = companyRes.items as Company[];
    customers.value = customerRes.items as Customer[];

    adminOpts.value = adminRes.options;
    companyOpts.value = companyRes.options;
    customerOpts.value = customerRes.options;

    orgLoaded.value = true;
  }

  /** '전체' 는 값이 null 이다. 화면들이 `value !== null` 로 실제 항목을 가려낸다. */
  const ALL_OPTION = { label: '전체', value: null };

  /** '전체' 항목이 앞에 붙은 담당자 셀렉트 옵션 */
  const adminOptions = computed(() => [ALL_OPTION, ...adminOpts.value]);

  /** '전체' 항목이 앞에 붙은 회사 셀렉트 옵션 */
  const companyOptions = computed(() => [ALL_OPTION, ...companyOpts.value]);

  /** '전체' 항목이 앞에 붙은 고객 셀렉트 옵션 */
  const customerOptions = computed(() => [ALL_OPTION, ...customerOpts.value]);

  function $reset() {
    identity.value = null;
    identityChecked.value = false;
    admins.value = [];
    companies.value = [];
    customers.value = [];
    adminOpts.value = [];
    companyOpts.value = [];
    customerOpts.value = [];
    orgLoaded.value = false;
  }

  return {
    $reset,
    adminOptions,
    admins,
    canUse,
    companies,
    companyId,
    companyOptions,
    customerOptions,
    customers,
    helpdeskUserId,
    identity,
    identityChecked,
    isAdmin,
    isLinked,
    isUnlinkedAdmin,
    loadIdentity,
    loadOrganizations,
    orgLoaded,
  };
});
