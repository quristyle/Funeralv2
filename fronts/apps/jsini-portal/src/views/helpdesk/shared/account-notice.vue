<script lang="ts" setup>
import { computed, onMounted } from 'vue';

import { Alert } from 'ant-design-vue';

import { useJsiniUser } from '#/composables/use-jsini-user';
import { useHelpdeskStore } from '#/store/helpdesk';

/**
 * 헬프데스크 계정 연결 상태 안내.
 *
 * 포털로 계정을 단일화했지만, 기존 헬프데스크 데이터(요청 작성자·담당자·댓글)는
 * 헬프데스크 내부 계정 ID 를 참조한다. 그래서 두 가지가 서로 다른 이야기가 된다.
 *
 * - **무엇을 볼 수 있는가** — 포털 역할이 정한다. 연결이 없어도 관리자는 관리 조회를 한다.
 * - **무엇이 '내 것'인가** — 연결이 있어야 정해진다. 없으면 "내가 쓴 댓글"을 찾을 수 없다.
 *
 * 전에는 이 둘을 구분하지 않고 "연결이 없으면 자료를 볼 수 없다"고만 알렸다.
 * 관리자 역할을 가진 계정에는 사실과 달랐고, 무엇을 하면 되는지도 알기 어려웠다.
 */
const helpdesk = useHelpdeskStore();
const { loginId, userName } = useJsiniUser();

/** 어떤 계정 이야기인지 적어 준다. 계정이 여럿인 환경에서 헷갈리지 않게. */
const who = computed(() =>
  loginId.value
    ? `${loginId.value}${userName.value ? ` (${userName.value})` : ''}`
    : '현재',
);

/** 안내를 띄울 상황인가. 연결이 되어 있으면 할 말이 없다. */
const show = computed(() => helpdesk.identityChecked && !helpdesk.isLinked);

/**
 * 담당자 권한은 있고 연결만 없는 경우와, 아무것도 없는 경우를 나눈다.
 * 전자는 일할 수 있으므로 경고가 아니라 정보로 띄운다.
 */
const kind = computed(() => (helpdesk.isUnlinkedAdmin ? 'admin' : 'none'));

const message = computed(() =>
  kind.value === 'admin'
    ? `${who.value} 계정은 포털 관리자 역할로 헬프데스크를 조회·관리합니다.`
    : `${who.value} 계정에 연결된 헬프데스크 사용자가 없습니다.`,
);

// 이 부품을 놓기만 하면 신원이 채워지도록 여기서 한 번 부른다.
// 이 부품을 쓰는 화면 21개 중 10개는 스스로 부르지 않고 여기에 의존한다.
// (스토어가 한 번만 조회하므로 중복 호출은 비용이 없다)
onMounted(() => helpdesk.loadIdentity());

const description = computed(() =>
  kind.value === 'admin'
    ? '조회와 관리는 그대로 하실 수 있습니다. 다만 이 계정은 헬프데스크 담당자 레코드에 이어져 있지 않아, 나에게 배정된 요청·내가 쓴 댓글·알림 구독처럼 "내 것"을 가리키는 기능은 비어 있습니다. 필요하시면 헬프데스크 설정 › 계정 연결에서 담당자 레코드와 이어 주세요.'
    : '헬프데스크 설정 › 계정 연결 화면에서 이 포털 계정을 헬프데스크 담당자 또는 고객 계정과 연결해야 요청 데이터를 볼 수 있습니다.',
);
</script>

<template>
  <Alert
    v-if="show"
    class="mb-3"
    :description="description"
    :message="message"
    show-icon
    :type="kind === 'admin' ? 'info' : 'warning'"
  />
</template>
