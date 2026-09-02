<script lang="ts" setup>
import type { NotificationApi } from '#/api/portal/notification';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Button,
  Card,
  message,
  Popconfirm,
  Skeleton,
  Switch,
  Tag,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  getMyNotificationState,
  sendTestPushToMe,
  subscribeMyDevice,
  unsubscribeMyDevice,
  updateMyNotificationPreference,
} from '#/api/portal/notification';
import GridIconButton from '#/components/GridIconButton.vue';

/**
 * [내 알림 설정] — `/system/push/setting`
 *
 * **로그인한 본인이 자기 알림을 관리하는 화면이다.** 남의 알림을 다루지 않는다
 * (관리자용 발송 현황·이력은 같은 묶음의 다른 화면이다).
 *
 * ── 이 화면이 바뀐 이유 ─────────────────────────────────────
 *
 * 원래 헬프데스크의 구독 시험 화면(NotificationSettings.vue)을 옮겨 온 것이라
 * 데이터도 헬프데스크(`/api/helpdesk/push/*`)를 보고 있었다. 헬프데스크의 구독은
 * 주인을 `(int Admin.Id, UserType)` 로 잡아 **포털 계정으로는 맞출 수가 없다.**
 * 관리 주체를 포털로 옮겼다 — 이제 NotificationServer(`/api/notification`)가 정본이고
 * 주인은 `("jsini", 로그인 아이디)` 다.
 *
 * 함께 고친 것 둘:
 * - **구독에 `applicationServerKey` 를 주지 않고 있었다.** VAPID 공개키 없이
 *   `pushManager.subscribe()` 를 부르면 크로미움계 브라우저는 그냥 거절한다.
 *   서버에서 공개키를 받아 넘긴다.
 * - 화면 머리말이 "이 프론트에는 서비스 워커가 없다" 고 적어 두었는데, 포털은
 *   2026-08-30 에 PWA 가 됐다(39번 문서). 그 안내를 걷어냈다.
 *
 * ── 스위치와 구독은 다른 것이다 ──────────────────────────────
 *
 * 구독은 **기기**다(브라우저마다 하나). 스위치는 **사람의 뜻**이다.
 * 그래서 스위치를 끄면 구독을 지우지 않고 발송만 멈춘다 — 지워 버리면 다시 켤 때
 * 브라우저 권한부터 다시 받아야 한다. 판정은 서버(`PushSender`)가 한다.
 */

/** 사람이 읽는 브라우저 이름. 기기 목록에 원문 UA 를 그대로 두면 읽히지 않는다. */
function prettyDevice(userAgent?: null | string): string {
  if (!userAgent) return '알 수 없는 기기';

  const ua = userAgent;
  // Edge · Samsung 은 자기 표시 뒤에 Chrome 도 함께 적는다. 먼저 걸러야 한다.
  const browser = /Edg\//.test(ua)
    ? 'Edge'
    : /SamsungBrowser/.test(ua)
      ? '삼성 인터넷'
      : /OPR\/|Opera/.test(ua)
        ? 'Opera'
        : /Firefox\//.test(ua)
          ? 'Firefox'
          : /Chrome\//.test(ua)
            ? 'Chrome'
            : /Safari\//.test(ua)
              ? 'Safari'
              : '브라우저';

  const os = /Windows/.test(ua)
    ? 'Windows'
    : /Android/.test(ua)
      ? 'Android'
      : /iPhone|iPad|iPod/.test(ua)
        ? 'iOS'
        : /Mac OS X/.test(ua)
          ? 'macOS'
          : /Linux/.test(ua)
            ? 'Linux'
            : '';

  return os ? `${browser} · ${os}` : browser;
}

/** `2026-09-02 14:30` 까지만. 초는 이 화면에서 쓸 데가 없다. */
function shortTime(value?: null | string): string {
  if (!value) return '-';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '-';
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

/**
 * VAPID 공개키(base64url)를 `subscribe()` 가 받는 바이트 배열로 바꾼다.
 *
 * base64url 은 `+/` 대신 `-_` 를 쓰고 꼬리의 `=` 를 떼어 둔 형식이다.
 * `atob` 는 그것을 모르므로 되돌려 준 뒤 넘긴다.
 *
 * `ArrayBuffer` 를 돌려준다 — `applicationServerKey` 는 `BufferSource` 를 받고,
 * 요즘 TS 의 `Uint8Array` 는 버퍼 종류가 제네릭이라 그대로는 맞지 않는다.
 */
function urlBase64ToBuffer(base64: string): ArrayBuffer {
  const padded = base64.padEnd(
    base64.length + ((4 - (base64.length % 4)) % 4),
    '=',
  );
  const raw = window.atob(padded.replaceAll('-', '+').replaceAll('_', '/'));
  const buffer = new ArrayBuffer(raw.length);
  const bytes = new Uint8Array(buffer);
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.codePointAt(i) ?? 0;
  return buffer;
}

const loading = ref(true);
const busy = ref(false);
/** 스위치별 저장 중 표시. 하나를 눌렀을 때 셋이 다 도는 것을 막는다. */
const savingKey = ref<'' | keyof PreferencePatch>('');

interface PreferencePatch {
  emailEnabled?: boolean;
  pushEnabled?: boolean;
  weatherEnabled?: boolean;
}

const state = ref<NotificationApi.MyState | null>(null);

/** 브라우저가 알림을 지원하는가 · 권한 · 서비스워커 */
const supported = ref(true);
const permission = ref<'unsupported' | NotificationPermission>('default');
const swActive = ref(false);
/** 이 브라우저에 남아 있는 구독의 endpoint. 없으면 빈 문자열 */
const myEndpoint = ref('');

const pref = computed(() => state.value?.preference);
const devices = computed(() => state.value?.devices ?? []);

/** 이 브라우저가 서버에도 등록되어 있는가. 둘 다 맞아야 알림이 온다. */
const thisBrowserSubscribed = computed(
  () =>
    Boolean(myEndpoint.value) &&
    devices.value.some((d) => d.endpoint === myEndpoint.value),
);

/** 구독 단추를 누를 수 있는 상태인가 */
const canSubscribe = computed(
  () =>
    supported.value &&
    swActive.value &&
    permission.value !== 'denied' &&
    Boolean(state.value?.pushAvailable),
);

/** 화면 맨 위에 한 줄로 알려 줄 것. 없으면 아무것도 띄우지 않는다. */
const notice = computed<null | { text: string; type: 'info' | 'warning' }>(
  () => {
    if (!supported.value) {
      return {
        text: '이 브라우저는 웹 알림을 지원하지 않습니다. 아래 이메일·날씨 설정은 그대로 저장됩니다.',
        type: 'warning',
      };
    }
    if (state.value && !state.value.pushAvailable) {
      return {
        text: '서버에 푸시 발송 키(VAPID)가 설정되지 않아 구독을 만들 수 없습니다. 시스템 담당자에게 알려 주세요.',
        type: 'warning',
      };
    }
    if (permission.value === 'denied') {
      return {
        text: '브라우저에서 이 사이트의 알림을 차단했습니다. 주소창 왼쪽 자물쇠 › 알림 › 허용으로 바꾼 뒤 다시 시도하세요.',
        type: 'warning',
      };
    }
    if (!swActive.value) {
      return {
        text: '서비스 워커가 아직 준비되지 않았습니다. 화면을 새로 고치면 대개 해결됩니다 (알림은 HTTPS 에서만 동작합니다).',
        type: 'warning',
      };
    }
    if (pref.value?.pushEnabled && !thisBrowserSubscribed.value) {
      return {
        text: '푸시 알림은 켜져 있지만 이 브라우저는 아직 구독하지 않았습니다. [이 브라우저 구독]을 누르세요.',
        type: 'info',
      };
    }
    return null;
  },
);

/** 브라우저 쪽 상태(지원·권한·서비스워커·이 기기 구독)를 다시 읽는다. */
async function readBrowserState() {
  if (!('Notification' in window)) {
    supported.value = false;
    permission.value = 'unsupported';
    return;
  }
  supported.value = true;
  permission.value = Notification.permission;

  if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
    swActive.value = false;
    return;
  }

  const registration = await navigator.serviceWorker.getRegistration();
  swActive.value = Boolean(registration?.active);
  if (!swActive.value) {
    myEndpoint.value = '';
    return;
  }

  const ready = await navigator.serviceWorker.ready;
  const subscription = await ready.pushManager.getSubscription();
  myEndpoint.value = subscription?.endpoint ?? '';
}

async function load(silent = false) {
  if (!silent) loading.value = true;
  try {
    // 브라우저 상태와 서버 상태를 함께 읽는다. 한쪽만 새로 읽으면
    // "이 브라우저 구독" 판정이 한 박자 어긋난다.
    const [server] = await Promise.all([
      getMyNotificationState(),
      readBrowserState(),
    ]);
    state.value = server;
  } catch {
    message.error('알림 설정을 불러오지 못했습니다.');
  } finally {
    loading.value = false;
  }
}

/**
 * 스위치 하나를 저장한다.
 *
 * 누른 즉시 보낸다 — 개인 설정 화면에 저장 단추를 두면 켜 놓고 나가는 일이 잦다.
 * 실패하면 화면 값을 되돌린다(껐다고 보이는데 알림이 오는 상태를 만들지 않는다).
 */
async function saveSwitch(key: keyof PreferencePatch, value: boolean) {
  if (!pref.value) return;

  const before = pref.value[key];
  pref.value[key] = value;
  savingKey.value = key;

  try {
    const saved = await updateMyNotificationPreference({ [key]: value });
    if (state.value) state.value.preference = saved;
  } catch {
    pref.value[key] = before;
    message.error('설정을 저장하지 못했습니다.');
  } finally {
    savingKey.value = '';
  }
}

/** 이 브라우저를 구독한다 — 권한 요청 → 브라우저 구독 → 서버 등록. */
async function handleSubscribe() {
  const key = state.value?.vapidPublicKey;
  if (!key) {
    message.warning('서버에서 푸시 공개키를 받지 못했습니다.');
    return;
  }

  busy.value = true;
  try {
    const granted = await Notification.requestPermission();
    permission.value = granted;
    if (granted !== 'granted') {
      message.warning('브라우저에서 알림 권한이 허용되지 않았습니다.');
      return;
    }

    const registration = await navigator.serviceWorker.ready;
    // 공개키가 빠지면 크로미움계는 그냥 거절한다. 예전 화면이 이것을 빠뜨려
    // 구독 단추가 늘 실패했다.
    const subscription = await registration.pushManager.subscribe({
      applicationServerKey: urlBase64ToBuffer(key),
      userVisibleOnly: true,
    });

    const json = subscription.toJSON();
    await subscribeMyDevice({
      auth: json.keys?.auth ?? '',
      endpoint: json.endpoint ?? subscription.endpoint,
      p256dh: json.keys?.p256dh ?? '',
      source: 'portal',
    });

    // 구독은 "받겠다" 는 뜻이다. 스위치가 꺼져 있으면 함께 켠다 —
    // 그러지 않으면 구독은 됐는데 아무것도 오지 않는 상태로 남는다.
    if (pref.value && !pref.value.pushEnabled) {
      await updateMyNotificationPreference({ pushEnabled: true });
    }

    await load(true);
    message.success('이 브라우저로 알림을 받도록 등록했습니다.');
  } catch (error) {
    message.error(`구독에 실패했습니다: ${(error as Error).message}`);
  } finally {
    busy.value = false;
  }
}

/** 이 브라우저의 구독을 해제한다 (브라우저 쪽과 서버 쪽 모두). */
async function handleUnsubscribe() {
  busy.value = true;
  try {
    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.getSubscription();
    if (subscription) {
      // 서버를 먼저 지운다. 브라우저에서 먼저 지우면 endpoint 를 잃어
      // 서버에 유령 구독이 남는다.
      await unsubscribeMyDevice(subscription.endpoint).catch(() => null);
      await subscription.unsubscribe();
    }
    await load(true);
    message.success('이 브라우저의 알림 구독을 해제했습니다.');
  } finally {
    busy.value = false;
  }
}

/** 목록에 있는 다른 기기를 서버에서 떼어 낸다. */
async function handleRemoveDevice(endpoint: string) {
  busy.value = true;
  try {
    await unsubscribeMyDevice(endpoint).catch(() => null);
    if (endpoint === myEndpoint.value) {
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();
      await subscription?.unsubscribe();
    }
    await load(true);
    message.success('기기를 목록에서 뺐습니다.');
  } finally {
    busy.value = false;
  }
}

/** 나에게 시험 알림을 보낸다. 실제 발송과 같은 길을 지난다. */
async function handleTest() {
  busy.value = true;
  try {
    const result = await sendTestPushToMe();
    if (result?.sent > 0) {
      message.success(`시험 알림을 보냈습니다 (기기 ${result.sent}대).`);
    } else {
      // 보낸 것이 없으면 성공으로 말하지 않는다. 이유는 서버가 준다.
      message.warning(result?.message ?? '보낸 알림이 없습니다.');
    }
  } finally {
    busy.value = false;
  }
}

const [Grid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'device', slots: { default: 'device' }, title: '기기', minWidth: 180 },
      { field: 'createdAt', slots: { default: 'createdAt' }, title: '등록', width: 140 },
      { field: 'lastSentAt', slots: { default: 'lastSentAt' }, title: '마지막 발송', width: 140 },
      { field: 'action', slots: { default: 'action' }, title: '', width: 70 },
    ],
    data: [],
    emptyText: '구독된 기기가 없습니다.',
    // 내 기기 두세 대를 보는 목록이다. 정렬·필터·엑셀·순번은 둘 자리가 아니다.
    //
    // 도구줄을 끄므로 `onRefresh` 도 주지 않는다 — 재조회는 카드 머리의
    // [상태 새로고침] 하나다. 둘을 두면 같은 단추가 두 벌이 된다(준수사항 6).
    gridFeatures: {
      filter: false,
      seq: false,
      sort: false,
      tools: false,
    },
    height: 'auto',
    // 전량 조회다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'endpoint' },
  } as any,
});

onMounted(() => load());
</script>

<template>
  <Page auto-content-height>
    <div class="mx-auto flex h-full w-full max-w-5xl flex-col gap-3">
      <div class="flex items-start justify-between gap-3">
        <div>
          <h1 class="text-lg font-bold">내 알림 설정</h1>
          <p class="text-xs text-muted-foreground">
            이 계정이 알림을 <b>어떤 방법으로</b> 받을지 정한다. 다른 사람에게는
            영향이 없다.
          </p>
        </div>
        <GridIconButton
          icon="vxe-table-icon-repeat"
          title="상태 새로고침"
          :loading="busy"
          @click="load(true)"
        />
      </div>

      <Alert
        v-if="notice"
        class="shrink-0"
        :message="notice.text"
        :type="notice.type"
        show-icon
      />

      <Skeleton v-if="loading" active :paragraph="{ rows: 8 }" />

      <div v-else class="grid min-h-0 flex-1 gap-3 lg:grid-cols-2">
        <!-- 왼쪽: 받을 알림 -->
        <Card size="small" title="받을 알림" class="self-start">
          <ul class="divide-y">
            <li class="flex items-start justify-between gap-4 pb-3">
              <div class="min-w-0">
                <div class="text-sm font-medium">푸시 알림</div>
                <p class="mt-0.5 text-xs text-muted-foreground">
                  브라우저·휴대전화 알림으로 받는다. 앱을 닫아 두어도 온다.
                  끄면 구독은 그대로 두고 발송만 멈춘다.
                </p>
              </div>
              <Switch
                :checked="pref?.pushEnabled"
                :loading="savingKey === 'pushEnabled'"
                @change="(v) => saveSwitch('pushEnabled', Boolean(v))"
              />
            </li>
            <li class="flex items-start justify-between gap-4 py-3">
              <div class="min-w-0">
                <div class="text-sm font-medium">이메일 알림</div>
                <p class="mt-0.5 text-xs text-muted-foreground">
                  계정의 대표 이메일로 받는다. <b>역할로 오는 알림</b>에만 적용된다 —
                  이름을 적어 보내는 업무 메일은 이 설정과 무관하다.
                </p>
              </div>
              <Switch
                :checked="pref?.emailEnabled"
                :loading="savingKey === 'emailEnabled'"
                @change="(v) => saveSwitch('emailEnabled', Boolean(v))"
              />
            </li>
            <li class="flex items-start justify-between gap-4 pt-3">
              <div class="min-w-0">
                <div class="text-sm font-medium">날씨 알림</div>
                <p class="mt-0.5 text-xs text-muted-foreground">
                  기상 특보와 설정한 임계치(강풍·한파 등)를 알린다.
                  <b>아직 발송이 켜지지 않았다</b> — 켜 두면 발송이 붙는 즉시 받는다.
                </p>
              </div>
              <Switch
                :checked="pref?.weatherEnabled"
                :loading="savingKey === 'weatherEnabled'"
                @change="(v) => saveSwitch('weatherEnabled', Boolean(v))"
              />
            </li>
          </ul>
        </Card>

        <!-- 오른쪽: 이 브라우저 + 기기 목록 -->
        <div class="flex min-h-0 flex-col gap-3">
          <Card size="small" title="이 브라우저">
            <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs">
              <span class="flex items-center gap-1">
                <span class="text-muted-foreground">알림 권한</span>
                <Tag
                  :color="
                    permission === 'granted'
                      ? 'success'
                      : permission === 'denied'
                        ? 'error'
                        : 'default'
                  "
                >
                  {{
                    permission === 'granted'
                      ? '허용'
                      : permission === 'denied'
                        ? '차단'
                        : permission === 'unsupported'
                          ? '지원 안 함'
                          : '아직 안 물음'
                  }}
                </Tag>
              </span>
              <span class="flex items-center gap-1">
                <span class="text-muted-foreground">서비스 워커</span>
                <Tag :color="swActive ? 'success' : 'default'">
                  {{ swActive ? '활성' : '없음' }}
                </Tag>
              </span>
              <span class="flex items-center gap-1">
                <span class="text-muted-foreground">구독</span>
                <Tag :color="thisBrowserSubscribed ? 'success' : 'default'">
                  {{ thisBrowserSubscribed ? '등록됨' : '미등록' }}
                </Tag>
              </span>
            </div>

            <div class="mt-3 flex flex-wrap gap-2">
              <Button
                v-if="!thisBrowserSubscribed"
                type="primary"
                :disabled="!canSubscribe"
                :loading="busy"
                @click="handleSubscribe"
              >
                <IconifyIcon icon="lucide:bell-plus" class="mr-1 size-4" />
                이 브라우저 구독
              </Button>
              <Button v-else danger :loading="busy" @click="handleUnsubscribe">
                <IconifyIcon icon="lucide:bell-off" class="mr-1 size-4" />
                이 브라우저 구독 해제
              </Button>
              <Button
                :disabled="!thisBrowserSubscribed || !pref?.pushEnabled"
                :loading="busy"
                @click="handleTest"
              >
                <IconifyIcon icon="lucide:send" class="mr-1 size-4" />
                시험 발송
              </Button>
            </div>
          </Card>

          <Card
            size="small"
            :title="`알림을 받는 기기 (${devices.length})`"
            class="flex min-h-0 flex-1 flex-col"
            :body-style="{ display: 'flex', flexDirection: 'column', minHeight: 0, flex: 1 }"
          >
            <Grid :table-data="devices" class="h-auto min-h-0 flex-1">
              <template #device="{ row }">
                <div class="flex items-center gap-1">
                  <span>{{ prettyDevice(row.userAgent) }}</span>
                  <Tag v-if="row.endpoint === myEndpoint" color="processing">
                    이 브라우저
                  </Tag>
                  <Tag v-if="row.failureCount > 0" color="warning">
                    실패 {{ row.failureCount }}
                  </Tag>
                </div>
              </template>
              <template #createdAt="{ row }">{{ shortTime(row.createdAt) }}</template>
              <template #lastSentAt="{ row }">
                {{ shortTime(row.lastSentAt) }}
              </template>
              <template #action="{ row }">
                <Popconfirm
                  title="이 기기로 알림을 보내지 않게 합니다."
                  ok-text="빼기"
                  cancel-text="취소"
                  @confirm="handleRemoveDevice(row.endpoint)"
                >
                  <Button type="link" danger size="small">빼기</Button>
                </Popconfirm>
              </template>
            </Grid>
          </Card>
        </div>
      </div>
    </div>
  </Page>
</template>
