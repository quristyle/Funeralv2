// ProjectView 콘솔 스크립트 — 화면과 원본 생성기 사이의 창구.
//
// [원본을 손대지 않고 들여왔다]
//
// `pv-script-core.js`(1,592줄)와 `pv-flow-core.js`(1,003줄)는 사내망 대시보드의
// 파일 그대로다. 고친 것은 한 줄 — 모듈 경로(`./pvScript` → `./pv-script-core.js`)
// 뿐이다. 그 안의 fetch 는 전부 **상대 주소**라, 스크립트가 ProjectView 탭 안에서
// 돌기만 하면 우리 쪽 주소를 알 필요가 없다.
//
// [왜 붙여넣는 방식인가]
//
// ProjectView 는 사내망 안에 있고 로그인 쿠키(`promise-token`)가 HttpOnly 다.
// 서버가 직접 부를 수도, 우리 화면에서 부를 수도 없다. ProjectView 탭의
// 콘솔에서 돌리면 그 쿠키가 저절로 붙는다 — 그것이 이 방식의 전부다.

import * as script from './pv-script-core.js';
import * as flow from './pv-flow-core.js';

/**
 * 스크립트 한 벌을 만든다.
 *
 * **이름에 괄호를 넣어 부르지 않는다** — Blazor 는 식별자를 `.` 으로 쪼개
 * 하나씩 찾으므로, 인자를 받는 함수 하나로 모아 둔다(`JsIdentifierTests`).
 *
 * @param {string} kind  collect · cache · push · date · flow · record · export
 * @param {object} args  갈래마다 다르다. 아래 switch 참고.
 * @returns {string} 콘솔에 붙여넣을 글자
 */
export function build(kind, args) {
  const a = args || {};
  const stamp = a.stamp || new Date().toLocaleString('ko-KR');

  switch (kind) {
    // 걷어 오기 — 결과 JSON 을 콘솔에 찍는다. 그것을 화면에 붙여넣으면
    // 서버가 캐시에 담는다.
    case 'collect':
      return script.buildPvCollectScript(a.codes || [], stamp);

    case 'cache':
      return script.buildPvCacheScript(a.codes || [], stamp);

    // 보내기 — 진척률 · 실적시작일.
    case 'push':
      return script.buildPvPushScript(a.items || [], stamp, a.cache);

    case 'rate':
      return script.buildPvScript(a.items || [], a.label || '', stamp, a.cache);

    case 'date':
      return script.buildPvDateScript(a.items || [], a.label || '', stamp, a.cache);

    case 'test':
      return script.buildPvTestScript(a.status, a.opts || {}, stamp);

    case 'export':
      return script.buildPvExportScript(stamp);

    // 워크플로 빈 칸 채우기.
    case 'flow':
      return flow.buildPvFlowScript(
        a.codes || [], a.template, a.defaultNm, a.stageRule, a.skipStages || [], stamp);

    case 'record':
      return flow.buildPvRecordScript(stamp);

    default:
      throw new Error('모르는 스크립트 갈래: ' + kind);
  }
}

/**
 * 글자를 클립보드에 담는다.
 *
 * 원본의 `copyText` 를 그대로 쓴다 — 클립보드 권한이 없을 때 숨은
 * `textarea` 로 되돌아가는 길이 거기 들어 있다.
 *
 * @returns {boolean} 담겼나
 */
export async function copy(text) {
  return script.copyText(text);
}
