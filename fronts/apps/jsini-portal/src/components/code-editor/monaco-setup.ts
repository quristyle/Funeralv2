/**
 * Monaco 에디터 공통 설정.
 *
 * 이식 전 두 시스템이 모두 Monaco 를 쓰고 있었다.
 *   - 프로젝트관리(Blazor WASM) `QuriCodeEditor` — SQL·코드 편집
 *   - 헬프데스크(JinReception) 바이너리 파서 — 16진 전문 붙여넣기
 *
 * 이식하면서 잠시 의존성 없는 자체 편집기로 대체했다가, 원본과 같은 편집 경험으로
 * 되돌리기 위해 Monaco 를 다시 붙였다.
 *
 * 이 파일이 하는 일은 셋이다.
 *   1) 필요한 언어만 등록 — `monaco-editor` 를 통째로 가져오면 TypeScript·CSS·HTML
 *      언어 서비스까지 딸려 와 번들이 크게 늘어난다. 이 시스템이 쓰는 것은
 *      SQL 계열·C#·JSON·평문뿐이라 그것만 등록한다.
 *   2) 워커 배선 — Vite 의 `?worker` 로 번들해 붙인다. 이걸 하지 않으면
 *      Monaco 가 CDN 에서 워커를 받으려다 실패하고 편집기가 뜨지 않는다.
 *   3) 언어 이름 정리 — 화면들이 넘기는 이름(`pgsql`, `json`, `csharp` …)을
 *      Monaco 가 아는 언어 id 로 바꾼다.
 *
 * 편집기 인스턴스를 만드는 쪽은 `code-editor.vue` 하나뿐이다.
 */
// 편집기 본체 + 편집 기능 전부(찾기·바꾸기, 접기, 다중 커서 …). 언어는 포함되지 않는다.
import * as monaco from 'monaco-editor/esm/vs/editor/edcore.main';
// 쓰는 언어만 개별 등록한다.
import 'monaco-editor/esm/vs/basic-languages/csharp/csharp.contribution';
import 'monaco-editor/esm/vs/basic-languages/pgsql/pgsql.contribution';
import 'monaco-editor/esm/vs/basic-languages/sql/sql.contribution';
import 'monaco-editor/esm/vs/language/json/monaco.contribution';

// Vite 의 `?worker` 는 워커 생성자를 기본 내보내기로 준다.
// 정적 분석은 그 사실을 모르므로(가상 모듈이다) 해당 규칙만 끈다.
// eslint-disable-next-line import/default
import EditorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';
// eslint-disable-next-line import/default
import JsonWorker from 'monaco-editor/esm/vs/language/json/json.worker?worker';

let configured = false;

/** 워커 배선을 한 번만 수행한다. */
export function setupMonaco() {
  if (configured) return monaco;
  configured = true;

  (globalThis as any).MonacoEnvironment = {
    getWorker(_workerId: string, label: string) {
      // JSON 만 전용 워커를 쓴다(문법 검사·서식). 나머지는 기본 편집기 워커면 충분하다.
      return label === 'json' ? new JsonWorker() : new EditorWorker();
    },
  };

  return monaco;
}

/**
 * 화면이 넘기는 언어 이름을 Monaco 언어 id 로 바꾼다.
 * 등록하지 않은 이름은 `plaintext` 로 떨어뜨린다 — 편집 자체는 막지 않는다.
 */
export function toMonacoLanguage(language?: string) {
  const name = (language ?? '').trim().toLowerCase();
  if (!name) return 'plaintext';

  const alias: Record<string, string> = {
    'c#': 'csharp',
    cs: 'csharp',
    mssql: 'sql',
    postgres: 'pgsql',
    postgresql: 'pgsql',
    text: 'plaintext',
    txt: 'plaintext',
  };

  const resolved = alias[name] ?? name;
  const known = monaco.languages.getLanguages().some((l) => l.id === resolved);
  return known ? resolved : 'plaintext';
}

export { monaco };
