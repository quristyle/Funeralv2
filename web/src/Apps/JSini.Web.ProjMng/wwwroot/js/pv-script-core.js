// ProjectView(dev-wbs.hd.com) 로 값을 전송하는 콘솔 스크립트 생성기.
//
// 왜 스크립트를 "복사해서 붙여넣는" 방식인가:
//  - ProjectView 는 HTTPS, 이 대시보드는 HTTP 라서 ProjectView 페이지에서 우리 API 를
//    직접 fetch 하면 mixed content 로 차단된다. 그래서 값을 스크립트에 박아 넘긴다.
//  - ProjectView 탭에서 실행하면 로그인 쿠키(promise-token)가 자동으로 붙어 인증이 해결된다.
//
// 두 종류를 만든다.
//  - buildPvScript      : 진척률(finishRate / actualProgressRate) 전송  — 진척률 현황 화면
//  - buildPvDateScript  : 실적시작일(actualStartDate) 전송              — 지연 현황 화면

// projectId 탐지 · 공통 호출 · code→workId 매핑. 두 스크립트가 그대로 공유한다.
export const COMMON = `
  // ---------- projectId 확보 ----------
  // promise-token 은 HttpOnly 라 document.cookie 로 못 읽는다(인증은 credentials:'include' 로 자동 처리).
  // 그래서 이미 이 페이지가 호출한 요청 URL / 스토리지에서 PJT_ 아이디를 찾아낸다.
  const PJT = /PJT_[0-9a-fA-F-]{36}/;
  const projectId = (() => {
    if (PROJECT_ID) return PROJECT_ID;

    // 1) 이 페이지가 이미 호출한 API URL 에서 추출 (가장 확실)
    try {
      for (const e of performance.getEntriesByType('resource')) {
        const m = String(e.name).match(PJT);
        if (m) return m[0];
      }
    } catch { /* 무시 */ }

    // 2) 로컬/세션 스토리지 값에서 추출
    try {
      for (const st of [localStorage, sessionStorage]) {
        for (let i = 0; i < st.length; i++) {
          const m = String(st.getItem(st.key(i))).match(PJT);
          if (m) return m[0];
        }
      }
    } catch { /* 무시 */ }

    // 3) 주소창에서 추출
    const m = location.href.match(PJT);
    return m ? m[0] : null;
  })();

  if (!projectId) {
    console.error(
      'projectId 를 찾지 못했습니다.\\n' +
      'WBS 목록이 보이는 상태(작업 화면 진입 후)에서 다시 실행하거나,\\n' +
      '스크립트 상단 PROJECT_ID 에 PJT_... 값을 직접 넣어주세요.');
    return;
  }
  console.log('projectId =', projectId);

  const H = (dts) => ({
    'accept': 'application/json',
    'content-type': 'application/json',
    'x-portalid': 'AAC',
    'x-pageid': PAGE,
    'x-dataserviceid': dts,
    'x-skiperrorhandler': 'N',
  });

  const call = async (url, dts, opt = {}) => {
    const r = await fetch(url, { credentials: 'include', headers: H(dts), ...opt });
    const t = await r.text();
    if (!r.ok) {
      if (r.status === 401 || r.status === 403) throw new Error(r.status + ' 인증 만료 — ProjectView 재로그인 후 다시 실행');
      throw new Error(r.status + ' ' + r.statusText + (t ? ' · ' + t.slice(0, 160) : ''));
    }
    return t ? JSON.parse(t) : null;
  };

  // ---------- code → workId 매핑 ----------
  // 목록 응답 원본도 남겨 둔다 (날짜가 함께 오면 건별 조회를 건너뛸 수 있다)
  let LAST_LIST = [];
  async function buildMap() {
    const cands = LIST_URL
      ? [LIST_URL]
      : Array.from({ length: 10 }, (_, i) =>
          BASE + '/projects/' + projectId + '/works?pageId=' + PAGE +
          '&dataServiceId=DTS_PSW_' + String(i + 1).padStart(5, '0'));

    for (const u of cands) {
      const dts = (u.match(/dataServiceId=([^&]+)/) || [])[1] || DTS_GET;
      try {
        const d = await call(u, dts);
        const arr = Array.isArray(d) ? d : (d?.content || d?.list || d?.data || d?.items || []);
        const m = new Map(arr.filter((x) => x && x.code && x.id).map((x) => [x.code, x.id]));
        if (m.size) { LAST_LIST = arr; console.log('%c목록 조회 성공', 'color:#2f6fd0', u, m.size + '건'); return m; }
      } catch (e) { /* 다음 후보 시도 */ }
    }
    throw new Error(
      'work 목록 URL 을 찾지 못했습니다.\\n' +
      'F12 → Network 에서 WBS 목록 조회 요청 URL 을 복사해 스크립트 상단 LIST_URL 에 넣고 다시 실행하세요.');
  }

  // 대시보드 캐시(hhip_wbs_pv)에 workId 가 있으면 목록 조회를 통째로 건너뛴다.
  // IDS 를 선언하지 않은 스크립트에서는 typeof 가 'undefined' 를 돌려주므로 안전하다.
  let map;
  if (typeof IDS !== 'undefined' && IDS && Object.keys(IDS).length) {
    map = new Map(Object.entries(IDS));
    console.log('%c캐시의 workId 를 사용합니다 — 목록 조회 생략 (' + map.size + '건)',
                'color:#1f9254;font-weight:bold');
  } else {
    try { map = await buildMap(); }
    catch (e) { console.error(e.message); return; }
  }

  // ---------- 동시 실행 ----------
  // 지금까지 건별 처리를 순차로 돌아 211건이면 400회 넘는 왕복을 줄줄이 기다렸다.
  async function runPool(items, n, fn) {
    const q = items.slice();
    let done = 0;
    const worker = async () => {
      while (q.length) {
        const it = q.shift();
        if (it === undefined) break;
        await fn(it);
        done++;
        if (done % 25 === 0) console.log('   ... ' + done + '/' + items.length);
      }
    };
    await Promise.all(Array.from({ length: Math.max(1, n) }, worker));
  }
`

const TEMPLATE = (dataJson, idsJson, meta) => `/* ============================================================
 * WBS 대시보드 → ProjectView 진척률 전송
 * 생성: ${meta.stamp}
 * 대상: ${meta.count}건  ·  보낼 값: ${meta.label}
 * 올림: ${meta.fullAt}% 이상은 ${meta.fullRate}% 로 올려 보낸다 — ${meta.fullRate}% 로 나가는 항목 ${meta.full}건
 * 캐시: ${meta.cached}
 *
 * [사용법]
 *  1) ProjectView 작업 화면(로그인 상태) 탭에서 F12 → Console 에 붙여넣고 Enter
 *     → 이때는 확인만 하고 전송하지 않는다 (DRY RUN)
 *  2) 출력된 대상/값이 맞으면 콘솔에  pvSend()  입력 + Enter → 실제 전송
 *     (또는 아래 DRY_RUN 을 false 로 바꿔 스크립트를 다시 실행)
 *  3) 전송 후 ProjectView 화면을 새로고침하면 값이 보인다.
 *     실패한 건이 있으면 이 스크립트를 한 번 더 돌리면 된다 — 이미 맞는 값은 건너뛴다.
 *
 * [주의] finishRate / actualProgressRate 는 ProjectView 상 '실적' 진척률이다.
 *        계획진척률을 보내면 실적으로 기록되니 값의 의미를 확인하고 사용할 것.
 *        ${meta.fullAt}% 이상은 ${meta.fullRate}% 로 올려 보낸다 — 그 미만은 계산된 값 그대로다.
 *        100% 로 올라간 건은 ProjectView 에서 <완료> 로 읽히니 대상 목록을 확인하고 보낼 것.
 * ============================================================ */
(async () => {
  const DRY_RUN = true;               // ← false 로 바꾸면 실제 PUT 전송
  const CONCURRENCY = 4;              // 동시 처리 수 (읽기+쓰기). 서버가 버거우면 1~2 로 낮춘다
  let   LIST_URL = '';                // work 목록 조회 URL 을 알고 있으면 여기에 붙여넣기

  let   PROJECT_ID = '${meta.projectId}';   // 대시보드 캐시의 값 (비면 자동 탐지)

  const PAGE    = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';    // 단건 조회
  const DTS_PUT = 'DTS_PSW_00003';    // 저장
  const BASE    = '/projectview/api/v1/promise';

  // 대시보드 캐시의 activity_id → workId. 있으면 목록 조회를 건너뛴다.
  const IDS = ${idsJson};

  // 전송할 값: [{ code: activity_id, rate: 진척률(%) }]
  const DATA = ${dataJson};
${COMMON}
  // ---------- 건별 읽고-고쳐-쓰기 ----------
  async function run(dry) {
    const ok = [], same = [], skip = [], fail = [];

    await runPool(DATA, dry ? CONCURRENCY : Math.min(CONCURRENCY, 3), async (it) => {
      const workId = map.get(it.code);
      if (!workId) { skip.push([it.code, 'workId 없음(프로젝트에 미등록)']); return; }

      const one = BASE + '/projects/' + projectId + '/works/' + workId;
      try {
        // taskList · outputList 는 ProjectView 가 주인인 값이라 캐시하지 않는다.
        // 오래된 값을 되돌려 보내면 그 사이의 일감 변경을 덮어쓰기 때문에 반드시 지금 읽는다.
        const got = await call(one + '?pageId=' + PAGE + '&dataServiceId=' + DTS_GET, DTS_GET);
        const w = Array.isArray(got) ? got[0] : got;
        if (!w) { fail.push([it.code, '조회 결과 없음']); return; }

        const rate = Number(it.rate);
        const cur = w.finishRate == null ? null : Number(w.finishRate);

        // 이미 같은 값이면 쓰지 않는다 — 불필요한 변경 이력을 남기지 않기 위한 것이다
        if (cur !== null && cur === rate) {
          same.push([it.code, w.title, rate]);
          return;
        }

        const body = {
          id: w.id,
          projectId: w.projectId,
          taskList: w.taskList || [],
          outputList: w.outputList || [],
          finishRate: rate,
          actualStartDate: w.actualStartDate || '',
          actualEndDate: w.actualEndDate || '',
          actualProgressRate: rate,
          progress: Number((rate / 100).toFixed(4)),
          actualProgressRateModified: 'Y',
        };

        if (!dry) {
          await call(one + '?pageId=' + PAGE + '&dataServiceId=' + DTS_PUT + '&actualProgressRateModified=Y',
                     DTS_PUT, { method: 'PUT', body: JSON.stringify(body) });
        }
        ok.push([it.code, w.title, (cur == null ? '-' : cur) + ' → ' + rate, body.actualStartDate || '(실적시작 없음)', rate]);
      } catch (e) {
        fail.push([it.code, String(e.message || e)]);
      }
    });

    // ---------- 결과 ----------
    console.log('%c' + (dry ? '[DRY RUN] 확인만 했습니다 — 아직 전송되지 않았습니다' : '[전송 완료] ProjectView 에 저장했습니다'),
                'font-size:13px;font-weight:bold;color:' + (dry ? '#c4692a' : '#1f9254'));
    console.table(ok.map(([code, title, change, actStart]) => ({ code, title, finishRate: change, 실적시작: actStart })));
    if (same.length) { console.log('이미 같은 값이라 쓰지 않은 건 ' + same.length + '건'); console.table(same.map(([c, t, v]) => ({ code: c, title: t, 값: v }))); }
    if (skip.length) { console.warn('건너뜀 ' + skip.length + '건'); console.table(skip.map(([c, r]) => ({ code: c, 사유: r }))); }
    if (fail.length) { console.error('실패 ' + fail.length + '건'); console.table(fail.map(([c, r]) => ({ code: c, 오류: r }))); }
    console.log('요약 — 대상 ' + DATA.length + ' / 처리 ' + ok.length + ' / 동일 ' + same.length +
                ' / 건너뜀 ' + skip.length + ' / 실패 ' + fail.length);

    if (dry) {
      window.pvSend = () => run(false);
      console.log('%c▶ 실제로 전송하려면 콘솔에  pvSend()  를 입력하고 Enter 를 누르세요.',
                  'font-size:14px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
      console.log('   (전송 후 ProjectView 화면은 새로고침하면 반영된 값이 보입니다)');
    } else {
      console.log('%c화면을 새로고침(F5)하면 반영된 값이 보입니다.', 'color:#1f9254');
      if (fail.length) {
        console.log('%c실패한 건은 이 스크립트를 한 번 더 돌리면 됩니다 — 이미 맞는 값은 건너뜁니다.',
                    'color:#c4692a;font-weight:bold');
      }
    }
    return { ok: ok.length, same: same.length, skip: skip.length, fail: fail.length };
  }

  await run(DRY_RUN);
})();
`

const TEMPLATE_DATE = (dataJson, idsJson, meta) => `/* ============================================================
 * WBS 대시보드 → ProjectView 실적시작일 전송
 * 생성: ${meta.stamp}
 * 대상: ${meta.count}건  ·  보낼 값: ${meta.label}
 *
 * 계획시작일은 지났는데 실적시작일이 비어 있는(착수지연) 항목에,
 * WBS 의 계획시작일을 ProjectView 의 actualStartDate 로 채워 넣는다.
 *
 * [사용법]
 *  1) ProjectView 작업 화면(로그인 상태) 탭에서 F12 → Console 에 붙여넣고 Enter
 *     → 이때는 확인만 하고 전송하지 않는다 (DRY RUN)
 *  2) 출력된 대상/날짜가 맞으면 콘솔에  pvSend()  입력 + Enter → 실제 전송
 *  3) 전송 후 ProjectView 화면을 새로고침하면 값이 보인다
 *
 * [안전장치] ProjectView 에 이미 실적시작일이 있는 항목은 건너뛴다.
 *            덮어쓰려면 아래 OVERWRITE 를 true 로 바꾼다.
 *            진척률(finishRate·actualProgressRate)은 조회한 값을 그대로 되돌려 보내
 *            건드리지 않는다(actualProgressRateModified=N).
 * ============================================================ */
(async () => {
  const DRY_RUN   = true;             // ← false 로 바꾸면 실제 PUT 전송
  const OVERWRITE = false;            // ← true 면 기존 실적시작일도 덮어씀
  const CONCURRENCY = 4;              // 동시 처리 수. 서버가 버거우면 1~2 로 낮춘다
  let   LIST_URL  = '';               // work 목록 조회 URL 을 알고 있으면 여기에 붙여넣기

  let   PROJECT_ID = '${meta.projectId}';   // 대시보드 캐시의 값 (비면 자동 탐지)

  const PAGE    = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';    // 단건 조회
  const DTS_PUT = 'DTS_PSW_00003';    // 저장
  const BASE    = '/projectview/api/v1/promise';

  // 대시보드 캐시의 activity_id → workId. 있으면 목록 조회를 건너뛴다.
  const IDS = ${idsJson};

  // 전송할 값: [{ code: activity_id, date: 'YYYY-MM-DD' }]
  const DATA = ${dataJson};
${COMMON}
  // ---------- 건별 읽고-고쳐-쓰기 ----------
  async function run(dry) {
    const ok = [], skip = [], fail = [];

    await runPool(DATA, dry ? CONCURRENCY : Math.min(CONCURRENCY, 3), async (it) => {
      const workId = map.get(it.code);
      if (!workId) { skip.push([it.code, 'workId 없음(프로젝트에 미등록)']); return; }

      const one = BASE + '/projects/' + projectId + '/works/' + workId;
      try {
        const got = await call(one + '?pageId=' + PAGE + '&dataServiceId=' + DTS_GET, DTS_GET);
        const w = Array.isArray(got) ? got[0] : got;
        if (!w) { fail.push([it.code, '조회 결과 없음']); return; }

        const had = (w.actualStartDate || '').slice(0, 10);
        if (had && !OVERWRITE) { skip.push([it.code, '이미 실적시작일 있음(' + had + ')']); return; }

        // 진척률 관련 값은 조회한 그대로 되돌려 보낸다 (실적시작일만 바꾸는 것이 목적)
        const body = {
          id: w.id,
          projectId: w.projectId,
          taskList: w.taskList || [],
          outputList: w.outputList || [],
          actualStartDate: it.date,
          finishRate: w.finishRate ?? 0,
          actualProgressRate: w.actualProgressRate ?? '0.00',
        };
        if (w.actualEndDate) body.actualEndDate = w.actualEndDate;

        if (!dry) {
          await call(one + '?pageId=' + PAGE + '&dataServiceId=' + DTS_PUT + '&actualProgressRateModified=N',
                     DTS_PUT, { method: 'PUT', body: JSON.stringify(body) });
        }
        ok.push([it.code, w.title, (had || '(비어 있음)') + ' → ' + it.date, it.date]);
      } catch (e) {
        fail.push([it.code, String(e.message || e)]);
      }
    });

    // ---------- 결과 ----------
    console.log('%c' + (dry ? '[DRY RUN] 확인만 했습니다 — 아직 전송되지 않았습니다' : '[전송 완료] ProjectView 에 저장했습니다'),
                'font-size:13px;font-weight:bold;color:' + (dry ? '#c4692a' : '#1f9254'));
    console.table(ok.map(([code, title, change]) => ({ code, title, 실적시작일: change })));
    if (skip.length) { console.warn('건너뜀 ' + skip.length + '건'); console.table(skip.map(([c, r]) => ({ code: c, 사유: r }))); }
    if (fail.length) { console.error('실패 ' + fail.length + '건'); console.table(fail.map(([c, r]) => ({ code: c, 오류: r }))); }
    console.log('요약 — 대상 ' + DATA.length + ' / 처리 ' + ok.length + ' / 건너뜀 ' + skip.length + ' / 실패 ' + fail.length);

    if (dry) {
      window.pvSend = () => run(false);
      console.log('%c▶ 실제로 전송하려면 콘솔에  pvSend()  를 입력하고 Enter 를 누르세요.',
                  'font-size:14px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
      console.log('   (전송 후 ProjectView 화면은 새로고침하면 반영된 값이 보입니다)');
    } else {
      console.log('%c화면을 새로고침(F5)하면 반영된 값이 보입니다.', 'color:#1f9254');
      if (fail.length) {
        console.log('%c실패한 건은 이 스크립트를 한 번 더 돌리면 됩니다 — 이미 실적시작일이 있는 건은 건너뜁니다.',
                    'color:#c4692a;font-weight:bold');
      }
    }
    return { ok: ok.length, skip: skip.length, fail: fail.length };
  }

  await run(DRY_RUN);
})();
`

/**
 * 이 값 이상이면 100% 로 올려 보낸다 (그 미만은 계산된 값 그대로).
 * 계획진척률은 종료일 직전에 89.7% 같은 어정쩡한 값이 나오는데,
 * ProjectView 에서는 그런 값이 <거의 다 됨> 이 아니라 <안 끝남> 으로 읽힌다.
 */
export const FULL_AT = 90
/** 올려 보낼 값 */
export const FULL_RATE = 100

/**
 * 대시보드 캐시의 workId 를 스크립트에 박을 형태로 만든다.
 * 값이 없으면 빈 객체를 돌려주고, 스크립트는 예전처럼 목록을 조회한다.
 * @param items  대상 항목 (activity_id 를 가진 것)
 * @param cache  { [activity_id]: pv_work_id }
 */
function idsFor(items, cache) {
  if (!cache) return { json: '{}', hit: 0, projectId: '' }
  const out = []
  for (const r of items) {
    const id = cache[r.activity_id]
    if (id) out.push(`    '${r.activity_id}': '${id}',`)
  }
  return {
    json: out.length ? '{\n' + out.join('\n') + '\n  }' : '{}',
    hit: out.length,
  }
}

/** 캐시 적용 상태를 스크립트 머리말에 한 줄로 적는다. */
function cacheNote(hit, total) {
  if (!hit) return '없음 — 예전처럼 work 목록을 통째로 조회한다'
  if (hit >= total) return `${hit}건 모두 workId 보유 — 목록 조회를 건너뛴다`
  return `${hit}/${total}건만 workId 보유 — 부족하므로 목록을 조회한다 (캐시를 다시 수집할 것)`
}

/**
 * 진척률 전송 스크립트 문자열을 만든다.
 * @param items  [{ activity_id, rate }]
 * @param label  보낼 값 설명 (예: '계획진척률')
 * @param stamp  생성 시각 문자열
 * @param cache  { ids: {activity_id: workId}, projectId } — 없으면 예전 방식 그대로
 */
export function buildPvScript(items, label, stamp, cache) {
  // 100% 로 나가는 건수를 센다 — ProjectView 에서 <완료> 로 읽히므로 보내기 전에 알아야 한다
  let fullCnt = 0
  const data = items
    .filter((r) => r.activity_id && r.rate != null)
    .map((r) => {
      const n = Number(r.rate)
      if (n < FULL_AT) return { code: r.activity_id, rate: n }
      fullCnt++
      return { code: r.activity_id, rate: FULL_RATE }   // 90% 이상은 100% 로 올려 보낸다
    })
  const json =
    '[\n' + data.map((d) => `    { code: '${d.code}', rate: ${d.rate} },`).join('\n') + '\n  ]'

  // workId 는 전부 있어야 목록 조회를 건너뛸 수 있다. 하나라도 비면 예전 경로를 쓴다.
  const ids = idsFor(data.map((d) => ({ activity_id: d.code })), cache?.ids)
  const full = ids.hit >= data.length && data.length > 0

  return TEMPLATE(json, full ? ids.json : '{}', {
    count: data.length, label, stamp, full: fullCnt, fullAt: FULL_AT, fullRate: FULL_RATE,
    cached: cacheNote(ids.hit, data.length),
    projectId: (full && cache?.projectId) || '',
  })
}

/**
 * 실적시작일 전송 스크립트 문자열을 만든다.
 * @param items  [{ activity_id, date }]  date 는 'YYYY-MM-DD'
 * @param label  보낼 값 설명 (예: '계획시작일 → 실적시작일')
 * @param stamp  생성 시각 문자열
 * @param cache  { ids, projectId } — 없으면 예전 방식 그대로
 */
export function buildPvDateScript(items, label, stamp, cache) {
  const data = items
    .filter((r) => r.activity_id && r.date)
    .map((r) => ({ code: r.activity_id, date: String(r.date).slice(0, 10) }))
  const json =
    '[\n' + data.map((d) => `    { code: '${d.code}', date: '${d.date}' },`).join('\n') + '\n  ]'

  const ids = idsFor(data.map((d) => ({ activity_id: d.code })), cache?.ids)
  const full = ids.hit >= data.length && data.length > 0

  return TEMPLATE_DATE(json, full ? ids.json : '{}', {
    count: data.length, label, stamp,
    projectId: (full && cache?.projectId) || '',
  })
}

const TEMPLATE_PUSH = (dataJson, idsJson, meta) => `/* ============================================================
 * WBS 대시보드 → ProjectView 전송 (진척률 + 실적시작일 한 번에)
 * 생성: ${meta.stamp}
 * 대상: ${meta.count}건  ·  진척률 ${meta.rateCount}건 · 실적시작일 ${meta.dateCount}건
 * 올림: ${meta.fullAt}% 이상은 ${meta.fullRate}% 로 올려 보낸다 — ${meta.fullRate}% 로 나가는 항목 ${meta.full}건
 * 캐시: ${meta.cached}
 *
 * 한 건을 한 번만 읽고 한 번만 쓴다 — 두 값이 같은 work 에 걸려도 왕복이 늘지 않는다.
 *
 * [사용법]
 *  1) ProjectView 작업 화면(로그인 상태) 탭에서 F12 → Console 에 붙여넣고 Enter
 *     → 이때는 확인만 하고 전송하지 않는다 (DRY RUN)
 *  2) 출력된 대상/값이 맞으면 콘솔에  pvSend()  입력 + Enter → 실제 전송
 *  3) 전송 후 ProjectView 화면을 새로고침하면 값이 보인다.
 *     실패한 건이 있으면 이 스크립트를 한 번 더 돌리면 된다 — 이미 맞는 값은 건너뛴다.
 *
 * [주의] finishRate / actualProgressRate 는 ProjectView 상 '실적' 진척률이다.
 *        ${meta.fullAt}% 이상은 ${meta.fullRate}% 로 올려 보낸다 — 그 미만은 계산된 값 그대로다.
 *        ${meta.fullRate}% 로 올라간 건은 ProjectView 에서 <완료> 로 읽히니 대상 목록을 확인하고 보낼 것.
 *
 * [안전장치] ProjectView 에 이미 실적시작일이 있으면 날짜는 건드리지 않는다(진척률만 처리).
 *            덮어쓰려면 아래 OVERWRITE_DATE 를 true 로 바꾼다.
 * ============================================================ */
(async () => {
  const DRY_RUN = true;               // ← false 로 바꾸면 실제 PUT 전송
  const OVERWRITE_DATE = false;       // ← true 면 기존 실적시작일도 덮어씀
  const CONCURRENCY = 4;              // 동시 처리 수. 서버가 버거우면 1~2 로 낮춘다
  let   LIST_URL = '';                // work 목록 조회 URL 을 알고 있으면 여기에 붙여넣기

  let   PROJECT_ID = '${meta.projectId}';   // 대시보드 캐시의 값 (비면 자동 탐지)

  const PAGE    = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';    // 단건 조회
  const DTS_PUT = 'DTS_PSW_00003';    // 저장
  const BASE    = '/projectview/api/v1/promise';

  // 대시보드 캐시의 activity_id → workId. 있으면 목록 조회를 건너뛴다.
  const IDS = ${idsJson};

  // 전송할 값: [{ code, rate?, date? }] — 둘 중 하나만 있을 수도 있다
  const DATA = ${dataJson};
${COMMON}
  // ---------- 건별 읽고-고쳐-쓰기 ----------
  async function run(dry) {
    const ok = [], same = [], skip = [], fail = [];

    await runPool(DATA, dry ? CONCURRENCY : Math.min(CONCURRENCY, 3), async (it) => {
      const workId = map.get(it.code);
      if (!workId) { skip.push([it.code, 'workId 없음(프로젝트에 미등록)']); return; }

      const one = BASE + '/projects/' + projectId + '/works/' + workId;
      try {
        // taskList · outputList 는 ProjectView 가 주인인 값이라 캐시하지 않는다.
        // 오래된 값을 되돌려 보내면 그 사이의 일감 변경을 덮어쓰기 때문에 반드시 지금 읽는다.
        const got = await call(one + '?pageId=' + PAGE + '&dataServiceId=' + DTS_GET, DTS_GET);
        const w = Array.isArray(got) ? got[0] : got;
        if (!w) { fail.push([it.code, '조회 결과 없음']); return; }

        // --- 진척률: 이미 같은 값이면 두지 않는다 ---
        const cur = w.finishRate == null ? null : Number(w.finishRate);
        const setRate = (it.rate != null && cur !== Number(it.rate)) ? Number(it.rate) : null;

        // --- 실적시작일: 이미 있으면 건드리지 않는다 ---
        const had = (w.actualStartDate || '').slice(0, 10);
        const setDate = (it.date && (!had || OVERWRITE_DATE)) ? it.date : null;

        if (setRate === null && setDate === null) {
          const why = [];
          if (it.rate != null) why.push('진척률 ' + cur + ' 동일');
          if (it.date && had) why.push('실적시작일 ' + had + ' 있음');
          same.push([it.code, w.title, why.join(' · ')]);
          return;
        }

        const body = {
          id: w.id,
          projectId: w.projectId,
          taskList: w.taskList || [],
          outputList: w.outputList || [],
          actualStartDate: setDate != null ? setDate : (w.actualStartDate || ''),
          actualEndDate: w.actualEndDate || '',
          finishRate: setRate != null ? setRate : (w.finishRate ?? 0),
          actualProgressRate: setRate != null ? setRate : (w.actualProgressRate ?? '0.00'),
        };
        // 진척률을 실제로 바꿀 때만 progress 와 modified 플래그를 붙인다.
        // 날짜만 보낼 때 Y 로 보내면 ProjectView 가 진척률을 손댄 것으로 기록한다.
        if (setRate != null) {
          body.progress = Number((setRate / 100).toFixed(4));
          body.actualProgressRateModified = 'Y';
        }
        const q = setRate != null ? '' : '&actualProgressRateModified=N';

        if (!dry) {
          await call(one + '?pageId=' + PAGE + '&dataServiceId=' + DTS_PUT + q,
                     DTS_PUT, { method: 'PUT', body: JSON.stringify(body) });
        }
        ok.push([
          it.code, w.title,
          setRate != null ? (cur == null ? '-' : cur) + ' → ' + setRate : '',
          setDate != null ? (had || '(비어 있음)') + ' → ' + setDate : '',
        ]);
      } catch (e) {
        fail.push([it.code, String(e.message || e)]);
      }
    });

    // ---------- 결과 ----------
    console.log('%c' + (dry ? '[DRY RUN] 확인만 했습니다 — 아직 전송되지 않았습니다' : '[전송 완료] ProjectView 에 저장했습니다'),
                'font-size:13px;font-weight:bold;color:' + (dry ? '#c4692a' : '#1f9254'));
    console.table(ok.map(([code, title, rate, date]) => ({ code, title, 진척률: rate || '-', 실적시작일: date || '-' })));
    if (same.length) { console.log('바꿀 것이 없어 쓰지 않은 건 ' + same.length + '건'); console.table(same.map(([c, t, r]) => ({ code: c, title: t, 사유: r }))); }
    if (skip.length) { console.warn('건너뜀 ' + skip.length + '건'); console.table(skip.map(([c, r]) => ({ code: c, 사유: r }))); }
    if (fail.length) { console.error('실패 ' + fail.length + '건'); console.table(fail.map(([c, r]) => ({ code: c, 오류: r }))); }
    console.log('요약 — 대상 ' + DATA.length + ' / 처리 ' + ok.length + ' / 그대로 ' + same.length +
                ' / 건너뜀 ' + skip.length + ' / 실패 ' + fail.length);

    if (dry) {
      window.pvSend = () => run(false);
      console.log('%c▶ 실제로 전송하려면 콘솔에  pvSend()  를 입력하고 Enter 를 누르세요.',
                  'font-size:14px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
      console.log('   (전송 후 ProjectView 화면은 새로고침하면 반영된 값이 보입니다)');
    } else {
      console.log('%c화면을 새로고침(F5)하면 반영된 값이 보입니다.', 'color:#1f9254');
      if (fail.length) {
        console.log('%c실패한 건은 이 스크립트를 한 번 더 돌리면 됩니다 — 이미 맞는 값은 건너뜁니다.',
                    'color:#c4692a;font-weight:bold');
      }
    }
    return { ok: ok.length, same: same.length, skip: skip.length, fail: fail.length };
  }

  await run(DRY_RUN);
})();
`

/**
 * 진척률과 실적시작일을 한 스크립트로 함께 보낸다.
 * 같은 activity_id 에 둘 다 걸리면 한 건으로 합쳐 왕복을 한 번만 돈다.
 *
 * @param items  [{ activity_id, rate?, date? }]
 * @param stamp  생성 시각 문자열
 * @param cache  { ids, projectId } — 없으면 목록을 조회하는 예전 경로
 */
export function buildPvPushScript(items, stamp, cache) {
  // activity_id 로 합친다 — 진척률과 실적시작일이 같은 work 에 걸리는 일이 흔하다
  const merged = new Map()
  let fullCnt = 0
  let rateCount = 0
  let dateCount = 0

  for (const r of items) {
    if (!r.activity_id) continue
    const cur = merged.get(r.activity_id) ?? { code: r.activity_id }

    if (r.rate != null && cur.rate === undefined) {
      const n = Number(r.rate)
      if (n >= FULL_AT) fullCnt++
      cur.rate = n < FULL_AT ? n : FULL_RATE   // 90% 이상은 100% 로 올려 보낸다
      rateCount++
    }
    if (r.date && cur.date === undefined) {
      cur.date = String(r.date).slice(0, 10)
      dateCount++
    }
    if (cur.rate !== undefined || cur.date !== undefined) merged.set(r.activity_id, cur)
  }

  const data = [...merged.values()]
  const json =
    '[\n' +
    data
      .map((d) => {
        const parts = [`code: '${d.code}'`]
        if (d.rate !== undefined) parts.push(`rate: ${d.rate}`)
        if (d.date !== undefined) parts.push(`date: '${d.date}'`)
        return `    { ${parts.join(', ')} },`
      })
      .join('\n') +
    '\n  ]'

  const ids = idsFor(data.map((d) => ({ activity_id: d.code })), cache?.ids)
  const full = ids.hit >= data.length && data.length > 0

  return TEMPLATE_PUSH(json, full ? ids.json : '{}', {
    count: data.length, rateCount, dateCount, stamp,
    full: fullCnt, fullAt: FULL_AT, fullRate: FULL_RATE,
    cached: cacheNote(ids.hit, data.length),
    projectId: (full && cache?.projectId) || '',
  })
}

const TEMPLATE_TEST = (idsJson, meta) => `/* ============================================================
 * WBS 대시보드 → ProjectView 단위테스트 상태 변경 (여러 화면을 한 번에)
 * 생성: ${meta.stamp}
 * 바꿀 값: status = '${meta.status}'
 * 대상 화면: ${meta.count}건${meta.scopeText}
 * 절차 범위: ${meta.onlyText}
 *
 * [사용법]
 *  1) ProjectView 에 로그인된 탭이면 어디서 실행해도 된다 (테스트 화면일 필요 없다).
 *  2) F12 → Console 에 붙여넣고 Enter
 *     → 단위테스트 목록을 찾아 화면별 절차 상태를 <조회만> 해서 표로 보여 준다
 *  3) 대상과 값이 맞으면 콘솔에  pvSend()  입력 + Enter → 실제 변경
 *
 * [보내는 것] 목록이 준 절차 객체에서 status 만 바꿔 그대로 되돌려 보낸다.
 *            DRY RUN 이 첫 건의 본문 전체를 찍어 주니 눈으로 맞춰 본 뒤 보낼 것.
 *
 * [쓰는 주소]
 *   단위테스트 목록  POST /projects/{pjt}/tests?pageId=PGE_PSU_TestPage&dataServiceId=DTS_PSU_00012
 *   절차 목록        GET  /projects/{pjt}/test-procedures?…&dataServiceId=DTS_PSU_00009&testId={UT}
 *   절차 저장        PUT  /projects/{pjt}/test-procedures/{TP}?…&dataServiceId=DTS_PSU_00010
 * ============================================================ */
(async () => {
  const DRY_RUN = true;                 // ← false 로 바꾸면 실제 PUT 전송
  const STATUS  = '${meta.status}';     // 바꿀 상태 값
  const ONLY    = ${meta.onlyJson};     // 바꿀 절차의 sortOrder. 빈 배열이면 전부
  const SKIP_SAME = true;               // 이미 같은 상태면 건너뛴다
  const CONCURRENCY = 3;                // 동시 처리 수. 서버가 버거우면 1~2 로

  let PROJECT_ID = '${meta.projectId}'; // 비면 자동 탐지

  // ── 단위테스트 목록 조회 (화면 ↔ 테스트를 잇는 데 쓴다) ──────────────────
  // 확인된 호출:  POST /projects/{pjt}/tests?pageId=…&dataServiceId=DTS_PSU_00012
  //   (GET 은 405 — 경로는 있으나 그 메서드가 아니다)
  // 조건을 본문으로 넘기는 목록 조회다. 응답은 { totalCount, tests: [...] }.
  //
  // 아래 본문은 실제 호출에서 딴 것이다. 화면을 가리는 칸만 비워 <전부> 가져오게 했다.
  //   testCaseIdOrTitle  제목 검색어 — 비운다
  //   pageSize           한 번에 가져올 개수 — 늘리고, 모자라면 currentPage 를 올려 가며 더 받는다
  // workCategoryIds(자재)·testOrderId(테스트 차수)는 그대로 둔다. 다른 차수를 보려면 여기서 바꾼다.
  let TESTS_URL    = '';                // 비면 아래 확정 경로
  let TESTS_METHOD = 'POST';
  let TESTS_BODY   = {
    workCategoryIds: ['c03324e4-1914-4f8d-8e9a-3206ce91215a'],
    testOrderId: 'TO_a8a8b241-be5f-4cd0-90ed-9a69453aefd6',
    testCaseIdOrTitle: '',
    testerId: '', workerId: '', showMyTest: false, showDelay: false,
    statusIds: [], tcGroups: [],
    currentPage: 1, pageSize: 200,
    sortColumn: '', sortCustomColumn: '', sortOrder: '',
    dataType: '', dataCodeId: '', periodTarget: '',
    fromDueDate: '', toDueDate: '', managementId: '', procedureName: '',
    searchListCustom: [],
  };

  const PAGE     = 'PGE_PSU_TestPage';
  const DTS_TEST = 'DTS_PSU_00012';     // 단위테스트 목록 (POST)
  const DTS_LIST = 'DTS_PSU_00009';     // 절차 목록
  const DTS_PUT  = 'DTS_PSU_00010';     // 절차 저장
  const BASE     = '/projectview/api/v1/promise';

  // 대시보드가 박아 준 activity_id → 화면명 (지금 목록에 보이는 화면들).
  // 단위테스트의 title 이 이 화면명과 같아서, 이것으로 잇는다.
  const SCREENS = ${idsJson};

  const PJT = /PJT_[0-9a-fA-F-]{36}/;
  PROJECT_ID = PROJECT_ID || (() => {
    try {
      for (const e of performance.getEntriesByType('resource')) {
        const m = String(e.name).match(PJT);
        if (m) return m[0];
      }
    } catch { /* 무시 */ }
    const m = location.href.match(PJT);
    return m ? m[0] : null;
  })();
  if (!PROJECT_ID) { console.error('projectId 를 찾지 못했습니다. ProjectView 화면을 연 탭에서 실행하세요.'); return; }
  console.log('projectId =', PROJECT_ID, '· 대상 화면', Object.keys(SCREENS).length, '건');

  const H = (dts) => ({
    'accept': 'application/json',
    'content-type': 'application/json',
    'x-portalid': 'AAC',
    'x-pageid': PAGE,
    'x-dataserviceid': dts,
    'x-skiperrorhandler': 'N',
  });

  const call = async (url, dts, opt = {}) => {
    const r = await fetch(url, { credentials: 'include', headers: H(dts), ...opt });
    const t = await r.text();
    if (!r.ok) {
      if (r.status === 401 || r.status === 403) throw new Error(r.status + ' 인증 만료 — ProjectView 재로그인 후 다시 실행');
      if (r.status === 405) throw new Error('405 — 저장 메서드가 PUT 이 아닐 수 있습니다. Network 에서 실제 메서드를 확인해 주세요.');
      throw new Error(r.status + ' ' + r.statusText + (t ? ' · ' + t.slice(0, 160) : ''));
    }
    return t ? JSON.parse(t) : null;
  };

  // 단위테스트 목록은 { totalCount, tests: [...] } 로 온다. 다른 응답 모양도 함께 받아 둔다.
  const asArray = (d) =>
    Array.isArray(d) ? d : (d?.tests || d?.content || d?.list || d?.data || d?.items || d?.rows || []);

  async function runPool(items, n, fn) {
    const q = items.slice();
    let done = 0;
    const worker = async () => {
      while (q.length) {
        const it = q.shift();
        if (it === undefined) break;
        await fn(it);
        done++;
        if (done % 20 === 0) console.log('   ... ' + done + '/' + items.length);
      }
    };
    await Promise.all(Array.from({ length: Math.max(1, n) }, worker));
  }

  // ---------- 1) 단위테스트 목록 찾기 ----------
  // 화면마다 UT_ 를 손으로 넣을 수는 없으니 목록을 받아 workId 로 잇는다.
  // ① 확인된 주소를 먼저 부른다 (보통 이 한 번으로 끝난다)
  // ② 안 되면 이 페이지가 이미 부른 /tests URL
  // ③ 그래도 안 되면 흔한 후보를 조회만 해 본다 (버전이 올라가 주소가 바뀐 경우 대비)
  async function findTests() {
    const tried = [];
    const cands = [];

    const CONFIRMED = TESTS_URL ||
      BASE + '/projects/' + PROJECT_ID + '/tests?pageId=' + PAGE + '&dataServiceId=' + DTS_TEST;
    try {
      for (const e of performance.getEntriesByType('resource')) {
        const u = String(e.name);
        if (/\\/tests\\b/.test(u) && !/test-procedures/.test(u)) cands.push(u);
      }
    } catch { /* 무시 */ }
    for (const path of ['tests', 'unit-tests']) {
      for (let i = 1; i <= 12; i++) {
        cands.push(BASE + '/projects/' + PROJECT_ID + '/' + path +
                   '?pageId=' + PAGE + '&dataServiceId=DTS_PSU_' + String(i).padStart(5, '0'));
      }
    }

    // 같은 곳을 두 번 두드리지 않도록 주소를 한 모양으로 맞춘다.
    // performance 가 주는 것은 https://호스트/... 절대 주소고 우리가 만든 것은 /projectview/... 라서,
    // 그냥 비교하면 <같은 주소를 POST 로 한 번, GET 으로 또 한 번> 두드리게 된다 (그래서 405 가 났다).
    const norm = (u) => String(u).replace(/^https?:\\/\\/[^/]+/, '');

    // ① 확인된 호출을 그 메서드·본문 그대로 먼저 (보통 이 한 번으로 끝난다)
    // ② 안 되면 나머지는 GET 으로만 더듬는다 — 함부로 POST 를 쏘지 않기 위해서다
    const attempts = [{ u: CONFIRMED, m: (TESTS_METHOD || 'GET').toUpperCase(), b: TESTS_BODY }];
    const seenUrl = new Set([norm(CONFIRMED)]);
    for (const u of cands) {
      if (seenUrl.has(norm(u))) continue;
      seenUrl.add(norm(u));
      attempts.push({ u, m: 'GET', b: null });
    }

    let sawWrongMethod = null;
    for (const a of attempts) {
      const first = a === attempts[0];
      tried.push(a.m + ' ' + a.u);
      const dts = (a.u.match(/dataServiceId=([^&]+)/) || [])[1] || DTS_TEST;
      const opt = a.m === 'GET' ? {} : {
        method: a.m,
        body: typeof a.b === 'string' ? a.b : JSON.stringify(a.b ?? {}),
      };
      try {
        const raw = await call(a.u, dts, opt);
        const arr = asArray(raw);
        const hit = arr.filter((x) => x && typeof x.id === 'string' && x.id.startsWith('UT_'));
        if (hit.length) {
          // 한 쪽(pageSize)에 다 안 들어오면 쪽수를 올려 가며 마저 받는다.
          // 211개 화면이라 기본 100 으로는 한 번에 안 온다.
          const total = Number(raw?.totalCount ?? 0);
          const all = hit.slice();
          if (a.m !== 'GET' && total > all.length && typeof a.b === 'object' && a.b) {
            for (let page = 2; all.length < total && page <= 50; page++) {
              const body = JSON.stringify({ ...a.b, currentPage: page });
              const more = asArray(await call(a.u, dts, { method: a.m, body }))
                .filter((x) => x && typeof x.id === 'string' && x.id.startsWith('UT_'));
              if (!more.length) break;
              all.push(...more);
            }
          }
          console.log('%c단위테스트 목록 ' + all.length + '건' +
                      (total ? ' / 전체 ' + total : '') + ' — ' + a.m + ' ' + a.u, 'color:#2f6fd0');
          if (total && all.length < total) {
            console.warn('전체 ' + total + '건 중 ' + all.length + '건만 받았습니다. ' +
                         'TESTS_BODY 의 pageSize 를 늘려 주세요.');
          }
          return { list: all, url: a.u };
        }
        // 확인된 호출은 <성공했는데 쓸 것이 없는> 경우도 알려 준다.
        // 조용히 다음 후보로 넘어가면 무엇이 잘못됐는지 알 길이 없다.
        if (first) {
          console.warn('확인된 호출은 200 인데 단위테스트(UT_)를 찾지 못했습니다 — ' + a.m + ' ' + a.u);
          console.warn('보낸 본문:', opt.body ?? '(없음)');
          console.warn('받은 것: 배열 ' + arr.length + '건' +
                       (arr.length ? ' · 첫 건의 칸 이름: ' + Object.keys(arr[0]).join(', ') : ''));
          console.log('응답 원본(앞부분):', JSON.stringify(raw).slice(0, 800));
          console.warn('목록이 비었다면 본문(TESTS_BODY)이 필요한 조회일 수 있습니다. ' +
                       'Network 에서 그 POST 의 Request Payload 를 복사해 TESTS_BODY 에 넣어 주세요.');
        }
      } catch (e) {
        if (first) console.warn('확인된 호출 실패 — ' + a.m + ' ' + a.u + ' · ' + e.message);
        // 405 는 <경로는 맞는데 메서드가 다르다> 는 뜻이라 따로 기억해 둔다
        if (String(e.message || '').startsWith('405')) sawWrongMethod = a.m + ' ' + a.u;
      }
    }
    console.error('단위테스트 목록을 찾지 못했습니다.');
    if (sawWrongMethod) {
      console.error(
        '405 가 왔습니다 — 경로는 맞는데 <그 메서드가 아니라>는 뜻입니다:\\n  ' + sawWrongMethod + '\\n' +
        '조회가 POST 일 수 있습니다. 함부로 만들지 않으려고 탐색은 GET 만 합니다.\\n' +
        'F12 → Network 에서 <단위테스트 목록>을 부르는 실제 호출을 찾아,\\n' +
        '스크립트 위쪽 TESTS_URL · TESTS_METHOD · TESTS_BODY 에 넣고 다시 실행해 주세요.');
    } else {
      console.error(
        'ProjectView 의 <테스트> 목록 화면을 연 뒤 다시 실행하거나,\\n' +
        'F12 → Network 에서 그 목록 조회 호출을 TESTS_URL · TESTS_METHOD · TESTS_BODY 에 넣어 주세요.');
    }
    console.log('시도한 것 ' + tried.length + '개:');
    console.log(tried.join('\\n'));
    return null;
  }

  const found = await findTests();
  if (!found) return;

  // ---------- 2) 단위테스트 ↔ 화면(activity) 잇기 ----------
  // 단위테스트에는 work 를 가리키는 칸이 없다. 대신 title 이 화면명 그대로다
  //   예) title "[1120]자재코드 관리"  =  대시보드 menu_nm "[1120]자재코드 관리"
  // 그래서 <대괄호 안의 화면코드> 를 먼저 맞추고, 없으면 제목 전체로 맞춘다.
  // 코드를 우선하는 까닭 — 제목의 띄어쓰기나 뒷말은 한쪽만 고쳐질 수 있어도 [1120] 은 잘 안 바뀐다.
  const codeOf = (s) => { const m = String(s || '').match(/\\[\\s*([0-9A-Za-z_-]+)\\s*\\]/); return m ? m[1] : null; };
  const flat = (s) => String(s || '').replace(/\\s+/g, '').toLowerCase();

  const byCode = new Map();
  const byTitle = new Map();
  for (const [act, nm] of Object.entries(SCREENS)) {
    const c = codeOf(nm);
    if (c && !byCode.has(c)) byCode.set(c, act);
    const f = flat(nm);
    if (f && !byTitle.has(f)) byTitle.set(f, act);
  }

  const tests = [];
  const usedAct = new Set();
  const unmatched = [];
  for (const t of found.list) {
    const title = t.title || t.name || '';
    const act = byCode.get(codeOf(title)) || byTitle.get(flat(title));
    if (!act) { unmatched.push(title || t.id); continue; }
    if (usedAct.has(act)) continue;         // 한 화면에 테스트가 여럿이면 첫 것만
    usedAct.add(act);
    tests.push({ id: t.id, act, title });
  }

  if (!tests.length) {
    console.error('지금 목록의 화면과 이어지는 단위테스트가 없습니다.');
    console.log('ProjectView 테스트 제목 예:', found.list.slice(0, 5).map((x) => x.title));
    console.log('대시보드 화면명 예:', Object.values(SCREENS).slice(0, 5));
    return;
  }
  console.log('%c이어진 화면 ' + tests.length + '건 / 대상 ' + Object.keys(SCREENS).length + '건',
              'font-weight:bold;color:#1f9254');
  if (unmatched.length) {
    console.log('대시보드 목록과 이어지지 않은 테스트 ' + unmatched.length + '건 (건드리지 않는다):');
    console.log(unmatched.slice(0, 20).join(' · ') + (unmatched.length > 20 ? ' …' : ''));
  }
  const missing = Object.entries(SCREENS).filter(([a]) => !usedAct.has(a));
  if (missing.length) {
    console.warn('단위테스트를 찾지 못한 화면 ' + missing.length + '건 (건너뛴다):');
    console.table(missing.slice(0, 30).map(([a, nm]) => ({ activity: a, 화면명: nm })));
  }

  // ---------- 3) 화면별 절차 조회 ----------
  const rows = [];      // 바꿀 것
  const sameRows = [];  // 이미 같은 값
  const seenStatus = new Set();
  const listFail = [];

  console.log('절차를 읽는 중…');
  await runPool(tests, CONCURRENCY, async (t) => {
    const u = BASE + '/projects/' + PROJECT_ID + '/test-procedures'
            + '?pageId=' + PAGE + '&dataServiceId=' + DTS_LIST + '&testId=' + t.id;
    try {
      const arr = asArray(await call(u, DTS_LIST));
      for (const p of arr) {
        if (p.status !== null && p.status !== undefined) seenStatus.add(p.status);
        if (ONLY.length && !ONLY.includes(p.sortOrder)) continue;
        if (SKIP_SAME && p.status === STATUS) { sameRows.push({ act: t.act, p }); continue; }
        rows.push({ act: t.act, p });
      }
    } catch (e) {
      listFail.push([t.act, t.id, String(e.message || e)]);
    }
  });

  console.log('%c지금 쓰이는 상태값: ' + ([...seenStatus].join(' · ') || '(없음)'), 'color:#2f6fd0');
  if (seenStatus.size && !seenStatus.has(STATUS)) {
    console.warn('보내려는 STATUS(\\'' + STATUS + '\\') 는 지금 목록에 없는 값입니다. 받는 값이 맞는지 확인하세요.');
  }
  if (listFail.length) { console.error('절차 조회 실패 ' + listFail.length + '건'); console.table(listFail.map(([a, i, r]) => ({ 화면: a, testId: i, 오류: r }))); }
  console.log('바꿀 것 ' + rows.length + '건 / 이미 같음 ' + sameRows.length + '건 / 화면 ' + tests.length + '개');
  if (!rows.length) { console.log('%c바꿀 것이 없습니다.', 'color:#1f9254'); return; }

  // 화면별로 몇 건인지 먼저 보여 준다 — 한 번에 여러 화면을 건드리는 작업이라 그렇다
  const byAct = {};
  for (const r of rows) byAct[r.act] = (byAct[r.act] || 0) + 1;
  console.table(Object.entries(byAct).map(([act, n]) => ({ 화면: act, 바꿀절차: n })));

  const bodyOf = (p) => ({ ...p, status: STATUS });

  // ---------- 4) 실행 ----------
  async function run(dry) {
    if (dry) {
      console.log('%c[DRY RUN] 아직 아무것도 바꾸지 않았습니다.', 'font-size:13px;font-weight:bold;color:#c4692a');
      console.log('첫 건에 보낼 본문 — 개발자도구에서 직접 바꿨을 때와 모양이 같은지 확인하세요:');
      console.log(JSON.stringify(bodyOf(rows[0].p), null, 2));
      console.table(rows.slice(0, 200).map((r) => ({
        화면: r.act, 번호: r.p.sortOrder, 코드: r.p.testProcedureId,
        제목: r.p.title, 바뀜: r.p.status + ' → ' + STATUS,
      })));
      if (rows.length > 200) console.log('… 위 표는 앞 200건만. 전체는 ' + rows.length + '건');
      window.pvSend = () => run(false);
      console.log('%c▶ 실제로 바꾸려면 콘솔에  pvSend()  를 입력하고 Enter 를 누르세요.',
                  'font-size:14px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
      return;
    }

    const ok = [], fail = [];
    await runPool(rows, CONCURRENCY, async (r) => {
      const u = BASE + '/projects/' + PROJECT_ID + '/test-procedures/' + r.p.id
              + '?pageId=' + PAGE + '&dataServiceId=' + DTS_PUT;
      try {
        await call(u, DTS_PUT, { method: 'PUT', body: JSON.stringify(bodyOf(r.p)) });
        ok.push([r.act, r.p.sortOrder, r.p.title, r.p.status + ' → ' + STATUS]);
      } catch (e) {
        fail.push([r.act, r.p.sortOrder, r.p.title, String(e.message || e)]);
      }
    });

    console.log('%c[변경 완료] ' + ok.length + '건 · 실패 ' + fail.length + '건',
                'font-size:13px;font-weight:bold;color:' + (fail.length ? '#c4692a' : '#1f9254'));
    console.table(ok.slice(0, 200).map(([a, n, t, c]) => ({ 화면: a, 번호: n, 제목: t, 바뀜: c })));
    if (fail.length) { console.error('실패'); console.table(fail.map(([a, n, t, r]) => ({ 화면: a, 번호: n, 제목: t, 오류: r }))); }
    console.log('%c화면을 새로고침(F5)하면 반영된 상태가 보입니다.', 'color:#1f9254');
    if (fail.length) console.log('%c실패한 건은 이 스크립트를 한 번 더 돌리면 됩니다 — 이미 맞는 값은 건너뜁니다.', 'color:#c4692a');
  }

  await run(DRY_RUN);
})();
`

/**
 * 단위테스트 절차의 상태를 여러 화면에 걸쳐 바꾸는 스크립트를 만든다.
 * @param status  바꿀 값 (예: 'SUCCESS')
 * @param opts    { ids: {activity_id: workId}, only: [1,2], projectId, scopeText }
 * @param stamp   생성 시각 문자열
 */
export function buildPvTestScript(status, opts, stamp) {
  const ids = opts?.ids ?? {}
  const keys = Object.keys(ids).filter((k) => ids[k])
  const lines = keys.sort().map((k) => `    '${k}': '${ids[k]}',`)
  const only = (opts?.only ?? []).filter((n) => Number.isFinite(n))

  return TEMPLATE_TEST(lines.length ? '{\n' + lines.join('\n') + '\n  }' : '{}', {
    status: String(status || '').replace(/'/g, ''),
    count: keys.length,
    scopeText: opts?.scopeText ? ` (${opts.scopeText})` : '',
    onlyJson: only.length ? `[${only.join(', ')}]` : '[]',
    onlyText: only.length ? `번호 ${only.join(', ')} 만` : '절차 전부',
    projectId: (opts?.projectId || '').replace(/'/g, ''),
    stamp,
  })
}

/** HTTP(비보안 컨텍스트)에서는 navigator.clipboard 를 쓸 수 없어 textarea 폴백을 함께 둔다. */
export async function copyText(text) {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch { /* 폴백으로 진행 */ }
  const ta = document.createElement('textarea')
  ta.value = text
  ta.style.position = 'fixed'
  ta.style.opacity = '0'
  document.body.appendChild(ta)
  ta.select()
  const done = document.execCommand('copy')
  document.body.removeChild(ta)
  return done
}

const TEMPLATE_COLLECT = (dataJson, meta) => `/* ============================================================
 * ProjectView → WBS 대시보드 날짜 수집 (읽기 전용)
 * 생성: ${meta.stamp}
 * 대상: ${meta.count}건의 activity_id
 *
 * ProjectView 의 계획/실적 날짜를 모아 JSON 으로 만들어 준다.
 * ProjectView 데이터는 조회만 하고 절대 고치지 않는다 (GET 만 사용).
 *
 * [사용법]
 *  1) ProjectView 작업 화면(로그인 상태) 탭에서 F12 → Console 에 붙여넣고 Enter
 *  2) 수집이 끝나면 자동으로 클립보드에 복사된다.
 *     복사가 막히면 콘솔에  copy(pvJson)  을 입력하면 된다.
 *  3) WBS 대시보드 → ProjectView 동기화 화면에 붙여넣고 [미리보기] → [적용]
 * ============================================================ */
(async () => {
  let   LIST_URL = '';                // work 목록 조회 URL 을 알고 있으면 여기에 붙여넣기
  let   PROJECT_ID = '';              // 자동 탐지가 실패할 때만 PJT_... 를 직접 넣기
  const CONCURRENCY = 5;              // 건별 조회 동시 실행 수

  const PAGE    = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';    // 단건 조회
  const BASE    = '/projectview/api/v1/promise';

  // 대시보드가 관리하는 activity_id (비우면 프로젝트의 모든 work)
  const CODES = ${dataJson};
${COMMON}
  const want = new Set(CODES);
  const day = (v) => (v ? String(v).slice(0, 10) : null);
  const pick = (w) => ({
    code: w.code,
    title: w.title,
    planStartDate: day(w.planStartDate),
    planEndDate: day(w.planEndDate),
    actualStartDate: day(w.actualStartDate),
    actualEndDate: day(w.actualEndDate),
  });

  // 목록 응답에 이미 날짜가 있으면 건별 조회 없이 끝난다
  const listRows = LAST_LIST.filter((x) => x && x.code && (!want.size || want.has(x.code)));
  const listHasDates = listRows.some((x) => x.planStartDate !== undefined || x.actualStartDate !== undefined);

  let out = [];
  if (listHasDates) {
    console.log('목록 응답에 날짜가 포함되어 있어 건별 조회를 건너뜁니다.');
    out = listRows.map(pick);
  } else {
    const targets = listRows.map((x) => [x.code, x.id]);
    console.log('건별 조회 시작 —', targets.length + '건 (동시 ' + CONCURRENCY + ')');
    let done = 0;
    const fails = [];
    const worker = async () => {
      while (targets.length) {
        const t = targets.shift();
        if (!t) break;
        const url = BASE + '/projects/' + projectId + '/works/' + t[1] +
                    '?pageId=' + PAGE + '&dataServiceId=' + DTS_GET;
        try {
          const got = await call(url, DTS_GET);
          const w = Array.isArray(got) ? got[0] : got;
          if (w) out.push(pick(w));
          else fails.push([t[0], '조회 결과 없음']);
        } catch (e) {
          fails.push([t[0], String(e.message || e)]);
        }
        done++;
        if (done % 25 === 0) console.log('  ...', done + '건 완료');
      }
    };
    await Promise.all(Array.from({ length: CONCURRENCY }, worker));
    if (fails.length) { console.warn('조회 실패 ' + fails.length + '건'); console.table(fails.map((f) => ({ code: f[0], 오류: f[1] }))); }
  }

  out.sort((a, b) => String(a.code).localeCompare(String(b.code)));
  const missing = CODES.filter((c) => !out.some((o) => o.code === c));

  console.log('%c수집 완료 ' + out.length + '건', 'font-size:13px;font-weight:bold;color:#1f9254');
  console.table(out.slice(0, 20));
  if (out.length > 20) console.log('  ... 표에는 앞 20건만 표시했습니다.');
  if (missing.length) {
    console.warn('ProjectView 에서 찾지 못한 코드 ' + missing.length + '건');
    console.log(missing.join(', '));
  }

  window.pvJson = JSON.stringify(out);
  try {
    await navigator.clipboard.writeText(window.pvJson);
    console.log('%c클립보드에 복사했습니다 — WBS 대시보드의 [ProjectView 동기화] 화면에 붙여넣으세요.',
                'font-size:13px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
  } catch (e) {
    console.log('%c자동 복사가 막혔습니다. 콘솔에  copy(pvJson)  을 입력하면 복사됩니다.',
                'font-size:13px;font-weight:bold;color:#c4692a');
  }
  return { 수집: out.length, 미발견: missing.length };
})();
`

/**
 * ProjectView 날짜 수집 스크립트를 만든다 (읽기 전용).
 * @param codes 대시보드가 관리하는 activity_id 배열
 * @param stamp 생성 시각 문자열
 */
export function buildPvCollectScript(codes, stamp) {
  const list = [...new Set(codes.filter(Boolean))].sort()
  const lines = []
  for (let i = 0; i < list.length; i += 6) {
    lines.push('    ' + list.slice(i, i + 6).map((c) => `'${c}'`).join(', ') + ',')
  }
  const json = '[\n' + lines.join('\n') + '\n  ]'
  return TEMPLATE_COLLECT(json, { count: list.length, stamp })
}

const TEMPLATE_EXPORT = (meta) => `/* ============================================================
 * ProjectView Excel Export 내려받기
 * 생성: ${meta.stamp}
 *
 * ProjectView 화면의 [Excel Export] 버튼과 같은 요청을 보내 파일을 내려받는다.
 * 받은 파일은 브라우저 다운로드 폴더에 저장되고, 대시보드의
 * [자동 동기화] 버튼이 그 파일을 주워 처리한다.
 *
 * [사용법] ProjectView 로그인 탭 → F12 → Console 에 붙여넣고 Enter
 * ============================================================ */
(async () => {
  let PROJECT_ID = '';                // 자동 탐지가 안 되면 PJT_... 를 직접 넣기
  const PAGE = 'PGE_PSW_WorkPage';
  const DTS  = 'DTS_PSW_00021';       // Excel Export
  const BASE = '/projectview/api/v1/promise';

  const PJT = /PJT_[0-9a-fA-F-]{36}/;
  const projectId = (() => {
    if (PROJECT_ID) return PROJECT_ID;
    try {
      for (const e of performance.getEntriesByType('resource')) {
        const m = String(e.name).match(PJT);
        if (m) return m[0];
      }
    } catch { /* 무시 */ }
    const m = location.href.match(PJT);
    return m ? m[0] : null;
  })();

  if (!projectId) {
    console.error('projectId 를 찾지 못했습니다. 작업 화면에 들어간 뒤 다시 실행하거나 PROJECT_ID 를 직접 넣으세요.');
    return;
  }

  const url = BASE + '/projects/' + projectId + '/works/export-excel?pageId=' + PAGE + '&dataServiceId=' + DTS;
  console.log('요청:', url);

  const r = await fetch(url, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'accept': 'application/json, text/plain, */*',
      'content-type': 'application/json',
      'x-portalid': 'AAC',
      'x-pageid': PAGE,
      'x-dataserviceid': DTS,
      'x-skiperrorhandler': 'N',
    },
  });

  if (!r.ok) {
    const t = await r.text().catch(() => '');
    console.error('Export 실패:', r.status, r.statusText, t.slice(0, 300));
    console.log('%c본문(Request Payload)이 필요한 요청일 수 있습니다. ProjectView 화면의 Excel Export 버튼을 눌러 ' +
                'F12 → Network 에서 export-excel 요청의 Payload 를 확인해 알려주세요.', 'color:#c4692a');
    return;
  }

  const blob = await r.blob();
  const now = new Date();
  const pad = (n) => String(n).padStart(2, '0');
  const name = 'WBS_' + now.getFullYear() + pad(now.getMonth() + 1) + pad(now.getDate()) +
               '_' + pad(now.getHours()) + pad(now.getMinutes()) + '.xlsx';

  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(a.href), 5000);

  console.log('%c내려받았습니다: ' + name + ' (' + Math.round(blob.size / 1024) + ' KB)',
              'font-size:13px;font-weight:bold;color:#1f9254');
  console.log('%c이제 WBS 대시보드 → ProjectView 동기화 → [자동 동기화] 를 누르세요.',
              'font-size:13px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
})();
`

/** ProjectView Excel Export 를 내려받는 콘솔 스크립트 */
export function buildPvExportScript(stamp) {
  return TEMPLATE_EXPORT({ stamp })
}

// ---------------------------------------------------------------- 캐시 수집
//
// 한 번 돌면 대시보드가 필요로 하는 것을 <모두> 걷어 온다.
//   · 신원   : workId  — 이게 있으면 다음부터 목록 조회를 통째로 건너뛴다
//   · 스냅샷 : finishRate · 계획/실적 날짜 — 이게 있어야 '바뀐 건만' 보낼 수 있다
//   · 일감   : taskList 의 id/code — 워크플로 채우기의 첫 조회가 사라진다
//
// 지금까지는 조회할 때마다 workId 와 finishRate 를 받아 놓고 그냥 버렸다.

const TEMPLATE_CACHE = (dataJson, meta) => `/* ============================================================
 * ProjectView → WBS 대시보드 캐시 수집 (읽기 전용)
 * 생성: ${meta.stamp}
 * 대상: ${meta.count}건의 activity_id
 *
 * ProjectView 는 조회만 하고 절대 고치지 않는다 (GET 만 사용).
 *
 * [사용법]
 *  1) ProjectView 작업 화면(로그인 상태) 탭에서 F12 → Console 에 붙여넣고 Enter
 *  2) 수집이 끝나면 자동으로 클립보드에 복사된다.
 *     복사가 막히면 콘솔에  copy(pvCache)  를 입력하면 된다.
 *  3) WBS 대시보드 → [상세 목록] 의 ⛁ 창에 붙여넣고 [저장]
 *
 * 걷어 오는 것 — Rows 화면 한 줄을 한 번에 맞추기 위한 것들이다.
 *   액티비티 : workId · 진척률 · 계획/실적 날짜
 *   일감     : id · 코드 · 제목 · 완료일(planEndDate) · 담당자
 *   워크플로 : 단계별 일자 · 담당자, 그리고 <현재단계> (WITH_FLOW = false 로 끄면 건너뛴다)
 * ============================================================ */
(async () => {
  let   LIST_URL = '';                // work 목록 조회 URL 을 알고 있으면 여기에 붙여넣기
  let   PROJECT_ID = '';              // 자동 탐지가 실패할 때만 PJT_... 를 직접 넣기
  const CONCURRENCY = 5;              // 건별 조회 동시 실행 수
  const WITH_FLOW = true;             // 일감 워크플로(단계별 일자·담당자)까지 걷는다

  const PAGE    = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';    // 단건 조회
  const PAGE_T  = 'PGE_PST_TaskDetailPage';
  const DTS_TASK = 'DTS_PST_00052';   // 일감 상세(워크플로 노드가 들어 있다)
  const DTS_HIST = 'DTS_PST_00040';   // 일감 변경이력 — <현재단계> 를 여기서 읽는다
  const DTS_USER = 'DTS_PSZ_00014';   // 프로젝트 사용자 목록 — USR- id → 이름
  const DTS_WFNODE = 'DTS_PST_00004'; // 워크플로 노드 정의 — WFN_ id → 단계
  const DTS_WFSTAT = 'DTS_PST_00018'; // 워크플로 상태 정의 — 단계 이름
  const BASE    = '/projectview/api/v1/promise';

  // 대시보드가 관리하는 activity_id (비우면 프로젝트의 모든 work)
  const CODES = ${dataJson};
${COMMON}
  const want = new Set(CODES);
  const day = (v) => {
    if (v === null || v === undefined || v === '') return null;
    const s = String(v).trim();
    // '20260825' 처럼 붙여 쓴 것도 받아 준다
    if (s.length === 8 && !isNaN(Number(s))) return s.slice(0, 4) + '-' + s.slice(4, 6) + '-' + s.slice(6, 8);
    return s.slice(0, 10);
  };
  const num = (v) => (v === null || v === undefined || v === '' ? null : Number(v));
  const arrOf = (d) => (Array.isArray(d) ? d : (d && (d.content || d.list || d.data || d.items)) || []);

  // 워크플로 조회는 pageId 가 달라 헤더를 따로 쓴다
  const HT = (dts) => ({
    'accept': 'application/json',
    'content-type': 'application/json',
    'x-portalid': 'AAC',
    'x-pageid': PAGE_T,
    'x-dataserviceid': dts,
    'x-skiperrorhandler': 'N',
  });
  const callT = async (url, dts) => {
    const r = await fetch(url, { credentials: 'include', headers: HT(dts) });
    const t = await r.text();
    if (!r.ok) {
      if (r.status === 401 || r.status === 403) throw new Error(r.status + ' 인증 만료 — ProjectView 재로그인 후 다시 실행');
      throw new Error(r.status + ' ' + r.statusText + (t ? ' · ' + t.slice(0, 120) : ''));
    }
    return t ? JSON.parse(t) : null;
  };

  // ---------------------------------------------------------------- 값 가려내기
  // 워크플로 노드의 필드 이름을 우리가 모른다. 그래서 <값의 모양>으로 가린다.
  // 읽기만 하므로 잘못 집어도 위험하지 않다 (쓰기는 [워크플로 채우기] 가 학습해서 한다).
  const isDateish = (v) => {
    if (v === null || v === undefined || typeof v === 'object') return false;
    const s = String(v).trim();
    if (s.length >= 10 && s.charAt(4) === '-' && s.charAt(7) === '-') return true;
    if (s.length === 8 && !isNaN(Number(s))) return true;
    return false;
  };

  /** 그 객체가 가진 날짜 값 하나. 완료(end)에 해당하는 키를 먼저 본다. */
  const dateIn = (o) => {
    const keys = Object.keys(o).filter((k) => isDateish(o[k]));
    if (!keys.length) return null;
    const end = keys.filter((k) => k.toLowerCase().indexOf('end') >= 0);
    return day(o[(end.length ? end : keys)[0]]);
  };

  /** 그 객체가 가진 USR- 값 하나 (담당자). */
  const usrIn = (o) => {
    for (const k of Object.keys(o)) {
      const v = o[k];
      if (typeof v === 'string' && v.indexOf('USR-') === 0) return v;
    }
    return null;
  };

  // ---------------------------------------------------------------- 변경이력
  // changeDtm 은 'YYYYMMDDHHmmssSSS' 인데 밀리초가 없고 공백으로 채워진 것도 섞여 있다
  // ('20260814144549   '). 그래서 다듬고 17자리로 채워 문자열 비교로 정렬한다.
  const dtm = (h) => String((h && h.changeDtm) || '').trim().padEnd(17, '0');
  const dtmDay = (v) => {
    const s = String(v || '').trim();
    return s.length >= 8 ? s.slice(0, 4) + '-' + s.slice(4, 6) + '-' + s.slice(6, 8) : null;
  };
  /** '현재상태' 변경 항목인가 (한국어·영어 응답 모두 받는다) */
  const isCurStatus = (h) =>
    !!h && (h.changeFieldEn === 'Current Status' ||
            h.changeFieldKo === '현재상태' ||
            h.changeField === '현재상태');

  // ---------------------------------------------------------------- 단계 이름
  // 노드에는 단계 이름이 없고 nodeId(WFN_...) 만 있다. 정의를 받아 이름을 붙인다.
  // 'name' 이 든 키를 아무거나 집으면 userGroupName('전체') 같은 것을 집는다 — 실제로 그랬다.
  const NAME_KEYS = ['statusName', 'nodeName', 'stageName', 'workflowStatusName',
                     'statusNm', 'nodeNm', 'name', 'title', 'label', 'displayName'];
  const pickName = (o) => {
    for (const k of NAME_KEYS) {
      const v = o[k];
      if (typeof v === 'string' && v.trim() && v.indexOf('WF') !== 0) return v.trim();
    }
    return '';
  };

  const STAGE = { byNode: {}, names: [] };
  async function loadStages() {
    const q = '?pageId=loadProjectInitData&dataServiceId=';
    const st = arrOf(await callT(BASE + '/projects/' + projectId + '/workflows/status' + q + DTS_WFSTAT, DTS_WFSTAT));
    const stName = {};
    for (const x of st) if (x && x.id) stName[x.id] = pickName(x);

    const nd = arrOf(await callT(BASE + '/projects/' + projectId + '/workflows/nodes' + q + DTS_WFNODE, DTS_WFNODE));
    for (const x of nd) {
      if (!x || !x.id) continue;
      // 단계 이름은 상태(status)에 붙어 있다 — 노드 정의의 이름보다 우선한다
      STAGE.byNode[x.id] = stName[x.statusId] || pickName(x) || '';
    }
    const seen = {};
    for (const id of Object.keys(STAGE.byNode)) if (STAGE.byNode[id]) seen[STAGE.byNode[id]] = true;
    STAGE.names = Object.keys(seen);
  }
  const stageOf = (nd) =>
    STAGE.byNode[nd.nodeId] || STAGE.byNode[nd.taskNodeId] || STAGE.byNode[nd.statusId] || '';
  const nodeLabel = (nd, i) => nd.taskNodeNm || nd.nodeNm || nd.title || '#' + (i + 1);

  // ---------------------------------------------------------------- 사용자
  // 이름을 <아무 짧은 문자열>로 집으면 안 된다. 사용자 목록에는 projectId 도 들어 있어서
  // 'USR- 로 시작하지 않는 40자 이하 문자열' 이라는 규칙이 PJT_... 를 이름으로 집었다.
  // (단계 이름에서 userGroupName='전체' 를 집었던 것과 같은 함정이다.)
  // 그래서 ① 이름일 법한 키를 순서대로 보고 ② id 모양인 값은 버린다.
  const USER_NAME_KEYS = ['userName', 'userNm', 'name', 'nm', 'empName', 'empNm',
                          'korName', 'userKorName', 'displayName', 'fullName', 'label'];
  const ID_PREFIX = ['USR-', 'PJT_', 'WORK_', 'TASK_', 'WFN_', 'STS_', 'TH_', 'PGE_', 'DTS_'];
  const looksId = (v) => {
    for (const p of ID_PREFIX) if (v.indexOf(p) === 0) return true;
    // 8-4-4-4-12 꼴(UUID)이 섞여 있으면 이름이 아니다
    return v.length >= 32 && v.split('-').length >= 5;
  };
  const nameIn = (o) => {
    for (const k of USER_NAME_KEYS) {
      const v = o[k];
      if (typeof v === 'string' && v.trim() && v.length <= 30 && !looksId(v)) return v.trim();
    }
    return null;
  };

  const USERS = { count: 0, byId: {}, named: 0 };
  async function loadUsers() {
    const d = await callT(BASE + '/projects/' + projectId + '/user?dataServiceId=' + DTS_USER, DTS_USER);
    const arr = arrOf(d);
    for (const x of arr) {
      if (!x || typeof x !== 'object') continue;
      const id = usrIn(x);
      if (!id) continue;
      const nm = nameIn(x);
      if (nm) { USERS.byId[id] = nm; USERS.named++; }
    }
    USERS.count = arr.length;
    // 이름을 하나도 못 찾았으면 규칙이 안 맞는 것이다 — 엉뚱한 값을 넣지 않고 알린다
    if (arr.length && !USERS.named) {
      console.warn('사용자 이름 필드를 찾지 못했습니다 — 담당자는 USR- id 로만 담깁니다.');
      console.log('   사용자 목록 첫 항목의 키:', Object.keys(arr[0] || {}).join(', '));
    }
  }

  // 응답 어딘가에 있는 '노드 배열' 을 찾는다 (이름에 node 가 든 키를 먼저 본다)
  function findNodes(d) {
    const looks = (a) =>
      Array.isArray(a) && a.length && a[0] && typeof a[0] === 'object' &&
      !Array.isArray(a[0]) && Object.keys(a[0]).length >= 4;
    function walk(o, depth, onlyNode) {
      if (!o || typeof o !== 'object' || depth > 4) return null;
      if (Array.isArray(o)) return !onlyNode && looks(o) ? o : null;
      const keys = Object.keys(o);
      const pri = keys.filter((k) => k.toLowerCase().indexOf('node') >= 0);
      for (const k of pri) if (looks(o[k])) return o[k];
      for (const k of (onlyNode ? pri : keys)) {
        const r = walk(o[k], depth + 1, onlyNode);
        if (r) return r;
      }
      return null;
    }
    return walk(d, 0, true) || walk(d, 0, false) || [];
  }

  // 목록에서 신원을 먼저 확보한다 — 요청 1회로 전부 얻는다
  const listRows = LAST_LIST.filter((x) => x && x.code && x.id && (!want.size || want.has(x.code)));
  console.log('%c[1/3] 신원 확보 ' + listRows.length + '건 (목록 조회 1회)',
              'font-size:13px;font-weight:bold;color:#1f9254');

  const shape = (w, base) => ({
    code: w.code || base.code,
    id: w.id || base.id,
    title: w.title || base.title || null,
    finishRate: num(w.finishRate),
    actualRate: num(w.actualProgressRate),
    planStartDate: day(w.planStartDate),
    planEndDate: day(w.planEndDate),
    actualStartDate: day(w.actualStartDate),
    actualEndDate: day(w.actualEndDate),
    tasks: (w.taskList || w.tasks || [])
      .map((t) => ({
        id: t.id || t.taskId || t.taskKey,
        code: t.code || t.taskCode || null,
        title: t.title || t.name || t.taskNm || null,
        planStartDate: day(t.planStartDate),
        planEndDate: day(t.planEndDate || t.endDate),
        chargerId: usrIn(t),
      }))
      .filter((t) => t.id),
  });

  // 진척률과 일감은 목록 응답에 없는 것이 보통이라 건별로 읽는다
  const out = [];
  const fails = [];
  const targets = listRows.slice();
  console.log('[2/3] 건별 조회 시작 — ' + targets.length + '건 (동시 ' + CONCURRENCY + ')');

  await runPool(targets, CONCURRENCY, async (base) => {
    const url = BASE + '/projects/' + projectId + '/works/' + base.id +
                '?pageId=' + PAGE + '&dataServiceId=' + DTS_GET;
    try {
      const got = await call(url, DTS_GET);
      const w = Array.isArray(got) ? got[0] : got;
      if (w) out.push(shape(w, base));
      else fails.push([base.code, '조회 결과 없음']);
    } catch (e) {
      fails.push([base.code, String(e.message || e)]);
    }
  });

  out.sort((a, b) => String(a.code).localeCompare(String(b.code)));
  const missing = CODES.filter((c) => !out.some((o) => o.code === c));
  const taskCnt = out.reduce((a, o) => a + o.tasks.length, 0);

  // ---------------------------------------------------------------- 워크플로
  // 일감마다 상세를 한 번씩 읽어 단계별 <일자·담당자> 를 걷는다.
  // 필드 이름을 모르므로 값 모양으로 가린다 — 날짜처럼 생긴 값은 일자,
  // 'USR-' 로 시작하는 값은 담당자. 읽기만 하므로 이 정도로 충분하다.
  let nodeCnt = 0;
  let flowFail = 0;
  let statusCnt = 0;
  let statusFail = 0;
  if (WITH_FLOW && taskCnt) {
    console.log('[3/3] 워크플로 조회 — 일감 ' + taskCnt + '건 (동시 ' + CONCURRENCY + ')');
    try {
      await loadStages();
      await loadUsers();
      console.log('   단계 ' + STAGE.names.length + '종 · 사용자 ' + USERS.count + '명(이름 확인 ' + USERS.named + '명)');
    } catch (e) {
      console.warn('   단계/사용자 정의를 읽지 못했습니다 — 단계 이름 없이 진행합니다: ' + e.message);
    }

    const jobs = [];
    for (const o of out) for (const t of o.tasks) jobs.push(t);

    await runPool(jobs, CONCURRENCY, async (t) => {
      try {
        const url = BASE + '/projects/' + projectId + '/task?pageId=' + PAGE_T +
                    '&dataServiceId=' + DTS_TASK + '&taskCode=' + encodeURIComponent(t.code || '') +
                    (t.id ? '&taskId=' + t.id : '');
        const d = await callT(url, DTS_TASK);
        const arr = findNodes(d);
        t.nodes = arr.map((nd, i) => {
          const wid = usrIn(nd);
          return {
            id: nd.id || nd.taskNodeId || nd.nodeId || null,
            stage: stageOf(nd) || nodeLabel(nd, i),
            date: dateIn(nd),
            workerId: wid,
            workerNm: wid ? (USERS.byId[wid] || null) : null,
          };
        });
        nodeCnt += t.nodes.length;
      } catch (e) {
        flowFail++;
        fails.push([t.code || t.id, '워크플로: ' + String(e.message || e)]);
      }

      // ---------- 현재단계 ----------
      // 변경이력에서 '현재상태' 항목 중 <가장 나중> 것의 도착값이 지금 단계다.
      // 응답은 시간순이 아니다 — 배열의 마지막 원소를 그냥 쓰면 틀린 값이 나온다.
      if (!t.id) return;
      try {
        const hu = BASE + '/projects/' + projectId + '/task-history/' + t.id +
                   '?pageId=' + PAGE_T + '&dataServiceId=' + DTS_HIST;
        const hist = arrOf(await callT(hu, DTS_HIST));
        const cur = hist.filter(isCurStatus).sort((a, b) => dtm(a).localeCompare(dtm(b)));
        const last = cur[cur.length - 1];
        if (last) {
          t.status = (last.nextValueKo || last.nextValue || '').trim() || null;
          t.statusAt = dtmDay(last.changeDtm);
          if (t.status) statusCnt++;
        }
      } catch (e) {
        statusFail++;
        fails.push([t.code || t.id, '현재단계: ' + String(e.message || e)]);
      }
    });
  }

  const emptyCnt = out.reduce((a, o) =>
    a + o.tasks.reduce((b, t) => b + (t.nodes || []).filter((n) => !n.date || !n.workerId).length, 0), 0);

  console.log('%c수집 완료 — 액티비티 ' + out.length + '건 · 일감 ' + taskCnt + '건 · 노드 ' + nodeCnt +
              '개 (빈 노드 ' + emptyCnt + '개) · 현재단계 ' + statusCnt + '건',
              'font-size:13px;font-weight:bold;color:#1f9254');
  console.table(out.slice(0, 20).map((o) => ({
    code: o.code, title: o.title, 진척률: o.finishRate,
    계획시작: o.planStartDate, 실적시작: o.actualStartDate, 일감: o.tasks.length,
    노드: o.tasks.reduce((a, t) => a + (t.nodes || []).length, 0),
    현재단계: o.tasks.map((t) => t.status).filter(Boolean).join(', '),
  })));
  if (out.length > 20) console.log('  ... 표에는 앞 20건만 표시했습니다.');
  if (fails.length) { console.warn('조회 실패 ' + fails.length + '건'); console.table(fails.map((f) => ({ code: f[0], 오류: f[1] }))); }
  if (missing.length) {
    console.warn('ProjectView 에서 찾지 못한 코드 ' + missing.length + '건');
    console.log(missing.join(', '));
  }

  // works 를 마지막에 둔다 — [ProjectView 동기화] 화면의 붙여넣기(첫 '[' ~ 마지막 ']')와도 호환된다
  window.pvCache = JSON.stringify({ projectId: projectId, works: out });
  try {
    await navigator.clipboard.writeText(window.pvCache);
    console.log('%c클립보드에 복사했습니다 — 대시보드 [상세 목록] 의 ⛁ 창에 붙여넣으세요.',
                'font-size:13px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
  } catch (e) {
    console.log('%c자동 복사가 막혔습니다. 콘솔에  copy(pvCache)  를 입력하면 복사됩니다.',
                'font-size:13px;font-weight:bold;color:#c4692a');
  }
  return { 액티비티: out.length, 일감: taskCnt, 노드: nodeCnt, 현재단계: statusCnt,
           실패: fails.length, 미발견: missing.length };
})();
`

/**
 * 캐시 수집 스크립트를 만든다 (읽기 전용).
 * @param codes 대시보드가 관리하는 activity_id 배열
 * @param stamp 생성 시각 문자열
 */
export function buildPvCacheScript(codes, stamp) {
  const list = [...new Set(codes.filter(Boolean))].sort()
  const lines = []
  for (let i = 0; i < list.length; i += 6) {
    lines.push('    ' + list.slice(i, i + 6).map((c) => `'${c}'`).join(', ') + ',')
  }
  const json = list.length ? '[\n' + lines.join('\n') + '\n  ]' : '[]'
  return TEMPLATE_CACHE(json, { count: list.length, stamp })
}
