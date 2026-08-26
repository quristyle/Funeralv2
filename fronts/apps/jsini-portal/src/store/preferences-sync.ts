import { watch } from 'vue';

import {
  preferences,
  updateCustomPreferences,
  updatePreferences,
  usePreferences,
} from '@vben/preferences';

import { getUserPreferencesApi, saveUserPreferencesApi } from '#/api';

/**
 * 화면 환경설정을 **계정에 붙여** 서버와 맞춘다.
 *
 * 예전에는 로컬스토리지에만 있어서 사람이 아니라 브라우저에 붙었다 —
 * 다른 PC 에서 로그인하면 기본값으로 돌아가고, 캐시를 지우면 사라졌다.
 *
 * [로컬스토리지를 없애지 않는 이유]
 * 서버 값을 받으려면 요청이 한 번 오가야 한다. 그동안 화면은 이미 그려지므로,
 * 로컬스토리지가 없으면 **기본 테마로 한 번 그려진 뒤 튀는** 것이 보인다.
 * 그래서 로컬스토리지는 '즉시 그리기용 캐시' 로 그대로 두고, 서버를 정본으로 쓴다.
 *   - 쓰던 브라우저  : 로컬 값으로 즉시 그림 → 서버 값이 와서 맞춰짐(대개 같다)
 *   - 새 브라우저    : 기본값으로 그림 → 곧 서버 값이 덮음
 *
 * [차이만 저장한다]
 * `diffPreference`(기본값과 다른 항목만)를 보낸다. 전체를 저장하면 나중에
 * 프레임워크 기본값이 바뀌어도 옛 값이 박혀 따라오지 않는다. 실제로 그 사고를
 * 겪었다 — 상위 동기화가 로그아웃 버튼 위치 기본값을 바꿨을 때, 저장돼 있던
 * 전체 값이 우선해서 새 기본값이 반영되지 않았다.
 */

/** 저장을 몇 ms 모아서 보낼지. 색을 고르면 값이 연달아 바뀐다. */
const SAVE_DEBOUNCE_MS = 800;

let saveTimer: null | ReturnType<typeof setTimeout> = null;
let stopWatch: (() => void) | null = null;
/** 서버 값을 적용하는 중에는 저장하지 않는다(받은 것을 그대로 되돌려 보내는 일 방지). */
let applying = false;

/**
 * 서버에 저장된 설정을 불러와 적용한다.
 *
 * 로그인 직후와, 이미 로그인된 상태로 앱이 시작될 때 부른다.
 * **실패해도 조용히 넘어간다** — 설정을 못 받은 것이 화면을 막을 이유는 아니다.
 */
export async function loadPreferencesFromServer() {
  let payload: Record<string, any>;
  try {
    payload = await getUserPreferencesApi();
  } catch {
    return;
  }

  if (!payload || Object.keys(payload).length === 0) {
    // 저장된 것이 없다. 지금 이 브라우저의 설정을 그대로 두고,
    // 다음 변경 때 서버에 올라가게 한다.
    return;
  }

  applying = true;
  try {
    // `custom` 은 프로젝트 확장 설정이라 다른 함수로 넣는다.
    const { custom, ...rest } = payload;

    if (Object.keys(rest).length > 0) {
      updatePreferences(rest);
    }
    if (custom && typeof custom === 'object') {
      updateCustomPreferences(custom);
    }
  } finally {
    // 적용으로 생긴 변경이 감시자에 닿은 뒤에 풀어야 한다.
    setTimeout(() => {
      applying = false;
    }, 0);
  }
}

/**
 * 설정이 바뀔 때마다 서버에 저장한다(모아서 한 번).
 *
 * 로그인한 뒤에 한 번만 걸면 된다. 두 번 불러도 이전 감시자를 정리한다.
 */
export function startPreferencesSync() {
  stopPreferencesSync();

  const { diffCustomPreference, diffPreference } = usePreferences();

  stopWatch = watch(
    // 두 값을 함께 본다. 어느 쪽이 바뀌어도 저장한다.
    () => [diffPreference.value, diffCustomPreference.value],
    () => {
      if (applying) return;

      if (saveTimer) clearTimeout(saveTimer);
      saveTimer = setTimeout(() => {
        const payload: Record<string, any> = { ...diffPreference.value };
        if (
          diffCustomPreference.value &&
          Object.keys(diffCustomPreference.value).length > 0
        ) {
          payload.custom = diffCustomPreference.value;
        }

        // 실패해도 알리지 않는다. 로컬스토리지에는 이미 남아 있어 이 브라우저는
        // 정상 동작하고, 다음 변경 때 다시 시도한다.
        saveUserPreferencesApi(payload).catch(() => undefined);
      }, SAVE_DEBOUNCE_MS);
    },
    { deep: true },
  );
}

/** 로그아웃할 때 감시를 끊는다. 남겨 두면 남의 계정으로 내 설정을 저장할 수 있다. */
export function stopPreferencesSync() {
  if (saveTimer) {
    clearTimeout(saveTimer);
    saveTimer = null;
  }
  stopWatch?.();
  stopWatch = null;
  synced = false;
}

/** 이번 로그인에서 서버 값을 이미 맞췄는지. */
let synced = false;

/**
 * 서버 값을 한 번 받아 적용하고, 이후 변경을 저장하도록 걸어 둔다.
 *
 * 로그인 직후와 새로고침 뒤(이미 로그인된 상태) 모두 같은 자리를 지나므로
 * `fetchUserInfo()` 에서 부른다. 여러 번 불려도 실제 작업은 한 번만 한다.
 */
export async function ensurePreferencesSynced() {
  if (synced) return;
  synced = true;

  await loadPreferencesFromServer();
  startPreferencesSync();
}

/**
 * 지금 이 브라우저의 설정을 곧바로 서버에 올린다(모으지 않고).
 *
 * 로그인 시 서버에 저장된 것이 없을 때, 쓰던 설정을 잃지 않게 한 번 올려 두는 용도다.
 */
export async function pushCurrentPreferences() {
  const { diffCustomPreference, diffPreference } = usePreferences();
  const payload: Record<string, any> = { ...diffPreference.value };
  if (
    diffCustomPreference.value &&
    Object.keys(diffCustomPreference.value).length > 0
  ) {
    payload.custom = diffCustomPreference.value;
  }

  if (Object.keys(payload).length === 0) return;

  // 기본값 그대로인 계정에 빈 값을 굳이 만들지는 않는다.
  await saveUserPreferencesApi(payload).catch(() => undefined);
}

/** 화면에 보여 줄 것이 없는 값이지만, 디버깅할 때 지금 상태를 보기 편하다. */
export function currentPreferencesSnapshot() {
  return JSON.parse(JSON.stringify(preferences));
}
