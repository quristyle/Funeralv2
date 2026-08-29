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

/**
 * 계정에 붙이면 안 되는 값들.
 *
 * `preferences.app` 에는 사람이 고른 설정과 **환경에서 파생된 값**이 섞여 있다.
 * 파생값은 브라우저·창 크기마다 달라야 하는데, 계정에 붙으면 다른 기기까지 따라간다.
 *
 * `app.isMobile` 로 실제 사고가 있었다. 좁은 창에서 한 번 열면 그것이 '기본값과 다른 값'
 * 으로 잡혀 서버에 올라가고, 그 뒤로는 **넓은 화면에서 로그인해도 모바일로 동작했다.**
 * 모바일이면 사이드바가 상시 표시가 아니라 오버레이 서랍이 되므로,
 * 사이드바 안에 있는 **메뉴 검색칸이 아예 그려지지 않았다.**
 *
 * 보내는 쪽과 받는 쪽 **양쪽에서** 걸러야 한다. 보내는 것만 막으면 이미 서버에 저장된
 * 값이 로그인할 때마다 계속 적용된다.
 *
 * `logo.source` · `logo.sourceDark` 도 같은 이유로 뺀다. 이것은 사람이 고르는 값이 아니라
 * **앱의 브랜딩**이다. 계정에 박히면 나중에 로고를 바꿔도 옛 주소가 따라와서 깨진다 —
 * 실제로 겪었다. 로고 기본값을 브랜드 것으로 바꾸자, 옛 경로(`/jsini.svg`)를 들고 있던
 * 브라우저가 그것을 '기본값과 다른 값' 으로 보고 서버에 올렸고, 그 파일은 이미 지운 뒤였다.
 * (`logo.enable` · `logo.showText` 는 사람이 끄고 켜는 값이라 그대로 둔다)
 *
 * 형식: `'섹션.키'`
 */
const DERIVED_KEYS = [
  'app.isMobile',
  'logo.source',
  'logo.sourceDark',
] as const;

/**
 * 파생값을 걷어낸 사본을 돌려준다. 원본은 건드리지 않는다.
 * 걷어내고 나서 빈 껍데기만 남은 섹션도 함께 지운다 — 빈 `{app:{}}` 를 보내 봐야 의미가 없다.
 */
function stripDerived(source: Record<string, any>): Record<string, any> {
  const result: Record<string, any> = { ...source };

  for (const path of DERIVED_KEYS) {
    const [section, key] = path.split('.') as [string, string];
    const bucket = result[section];

    if (!bucket || typeof bucket !== 'object' || !(key in bucket)) {
      continue;
    }

    const { [key]: _removed, ...rest } = bucket;
    if (Object.keys(rest).length === 0) {
      delete result[section];
    } else {
      result[section] = rest;
    }
  }

  return result;
}

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
    const { custom, ...raw } = payload;

    // 예전에 저장된 파생값은 여기서 버린다. 저장 쪽만 막으면 이미 서버에 있는 값이
    // 로그인할 때마다 계속 적용된다 (DERIVED_KEYS 주석 참고).
    const rest = stripDerived(raw);

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
        const payload: Record<string, any> = stripDerived(diffPreference.value);
        if (
          diffCustomPreference.value &&
          Object.keys(diffCustomPreference.value).length > 0
        ) {
          payload.custom = diffCustomPreference.value;
        }

        // 걷어내고 나니 보낼 것이 없으면 보내지 않는다.
        // (창을 좁혔다 넓히는 것만으로 저장 요청이 오가던 것이 이 경우다)
        if (Object.keys(payload).length === 0) {
          return;
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
  const payload: Record<string, any> = stripDerived(diffPreference.value);
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
