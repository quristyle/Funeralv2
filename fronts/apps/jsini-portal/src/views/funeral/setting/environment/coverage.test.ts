import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import { buildSections } from './catalog';

/**
 * [환경설정 항목이 갈라지지 않게 하는 장치]
 *
 * `/setting/environment` 는 헤더 톱니의 드로어와 **다른 UI** 를 쓴다
 * (드로어는 폭 350px 짜리라 넓은 화면에서 어색했다 — index.vue 머리말).
 *
 * 화면이 둘이 되면 **한쪽에만 설정이 붙는 사고**가 난다. 상위(vben)가 설정을
 * 하나 추가하면 드로어 패널에는 자동으로 들어오지만 이 화면은 모른다.
 *
 * 그래서 드로어 패널이 선언한 `defineModel` 이름을 읽어 카탈로그와 대조한다.
 * **빠진 것이 있으면 여기서 깨진다.** 그때 `catalog.ts` 에 한 줄 더하면 된다.
 *
 * 소스를 글자로 읽는 이유: 그 값들은 컴포넌트 안의 `defineModel` 이라
 * 불러와서 셀 방법이 없다. 이름 규칙(`defineModel<T>('name')`)은
 * 컴파일러가 강제하므로 정규식이 낡을 일이 없다.
 */

const HERE = dirname(fileURLToPath(import.meta.url));

const PREFERENCES_DIR = resolve(
  HERE,
  '../../../../../../../packages/effects/layouts/src/widgets/preferences',
);

const PANEL_PATH = join(PREFERENCES_DIR, 'preferences-panel.vue');
const BLOCKS_DIR = join(PREFERENCES_DIR, 'blocks');

/**
 * 드로어 패널이 다루는 설정 이름.
 *
 * `defineModel<Foo>('appLocale')` · `defineModel<boolean>(\n  'appWatermark',\n)`
 * 두 모양을 모두 잡는다(포매터가 줄을 바꾼다).
 */
function readPanelModels(): string[] {
  return namedModels(readFileSync(PANEL_PATH, 'utf8'));
}

/** 이름 붙은 `defineModel` 을 모은다. 이름 없는 기본 모델은 잡히지 않는다. */
function namedModels(source: string): string[] {
  const names = new Set<string>();
  const pattern = /defineModel<[^>]*>\(\s*'([^']+)'/g;
  let match = pattern.exec(source);
  while (match) {
    if (match[1]) names.add(match[1]);
    match = pattern.exec(source);
  }
  return [...names];
}

/** `blocks/` 아래 모든 `.vue` 를 훑는다. */
function blockFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) return blockFiles(full);
    return entry.endsWith('.vue') ? [full] : [];
  });
}

/** 패널이 `v-model:kebab-name="..."` 으로 잇고 있는 이름(카멜). */
function panelBoundNames(source: string): Set<string> {
  const names = new Set<string>();
  const pattern = /v-model:([\w-]+)=/g;
  let match = pattern.exec(source);
  while (match) {
    const kebab = match[1] ?? '';
    names.add(
      kebab.replace(/-([a-z])/g, (_, char: string) => char.toUpperCase()),
    );
    match = pattern.exec(source);
  }
  return names;
}

/**
 * 패널이 일부러 잇지 않는 블록 모델.
 *
 * **여기에 더할 때는 이유를 적는다.** 이유 없이 더하면 이 테스트가
 * "고장을 적어 두는 곳" 이 된다 — 실제로 `shortcutKeysPreferences` 가
 * 그렇게 조용히 빠져 있었다(23번 문서 5.5절).
 */
const UNBOUND_ON_PURPOSE: Record<string, string> = {
  // 패널이 `<Radius v-model="themeRadius" />` 로 **기본 모델**에 꽂는다.
  // 이름이 어긋나 보이지만 실제로 동작한다 — 23번 문서 5.2 에서 확인하고 되돌렸다.
  themeRadius: '패널이 이름 없는 기본 모델로 잇는다 (23번 문서 5.2)',
  // 사이드바의 [축소]·[고정] 체크 묶음을 그리기 위한 화면 안 임시 값이다.
  // 실제 설정은 sidebarCollapsedButton · sidebarFixedButton 이고 그 둘은 이어져 있다.
  sidebarButtons: '블록 안에서만 쓰는 표시용 값 (실제 설정 둘로 갈라진다)',
};

/** 카탈로그가 다루는 설정 이름 (한 컨트롤이 여럿을 다루면 `models` 에 적혀 있다). */
function catalogModels(): Set<string> {
  const names = new Set<string>();
  for (const section of buildSections()) {
    for (const field of section.fields) {
      names.add(field.model);
      for (const extra of field.models ?? []) names.add(extra);
    }
  }
  return names;
}

describe('환경설정 화면 카탈로그', () => {
  it('드로어 패널의 설정 항목을 하나도 빠뜨리지 않는다', () => {
    const panel = readPanelModels();

    // 정규식이 아무것도 못 잡았다면 패널 파일 위치나 모양이 바뀐 것이다.
    // 그때 "빠진 것 없음" 으로 통과해 버리면 이 테스트가 무용해진다.
    expect(panel.length).toBeGreaterThan(50);

    const catalog = catalogModels();
    const missing = panel.filter((name) => !catalog.has(name));

    expect(missing).toEqual([]);
  });

  it('스토어 경로와 모델 이름이 서로 맞는다', () => {
    // `sidebar.collapsedShowTitle` → `sidebarCollapsedShowTitle`.
    // 둘을 손으로 적으므로 오타가 날 수 있고, 오타가 나면 화면이 값을
    // 못 읽거나(경로 오타) 누락 감시가 헛돈다(모델 오타).
    for (const section of buildSections()) {
      for (const field of section.fields) {
        if (field.path.startsWith('custom.')) continue;

        const [group, key] = field.path.split('.');
        const expected = `${group}${(key ?? '').charAt(0).toUpperCase()}${(key ?? '').slice(1)}`;

        expect(field.model, `경로 ${field.path}`).toBe(expected);
      }
    }
  });

  it('드로어 블록의 스위치가 하나도 끊겨 있지 않다', () => {
    // 이 테스트가 잡는 고장: 블록에는 컨트롤이 있는데 패널이 `v-model` 로 잇지
    // 않아 **눌러도 저장되지 않는** 상태. `shortcutKeys.globalPreferences` 가
    // 실제로 그랬다(23번 문서 5.5절). 눈으로는 정상으로 보여 찾기 어렵다.
    const panelSource = readFileSync(PANEL_PATH, 'utf8');
    const bound = panelBoundNames(panelSource);

    const files = blockFiles(BLOCKS_DIR);
    expect(files.length).toBeGreaterThan(10);

    const dangling: string[] = [];
    for (const file of files) {
      for (const model of namedModels(readFileSync(file, 'utf8'))) {
        if (bound.has(model)) continue;
        if (model in UNBOUND_ON_PURPOSE) continue;
        dangling.push(`${model} (${file.split(/[/\\]/).slice(-2).join('/')})`);
      }
    }

    expect(dangling).toEqual([]);
  });

  it('같은 설정을 두 갈래에 두지 않는다', () => {
    const seen = new Map<string, string>();
    for (const section of buildSections()) {
      for (const field of section.fields) {
        const already = seen.get(field.path);
        expect(
          already,
          `${field.path} 가 '${already}' 와 '${section.title}' 두 곳에 있다`,
        ).toBeUndefined();
        seen.set(field.path, section.title);
      }
    }
  });
});
