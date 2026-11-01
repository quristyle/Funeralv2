using System.Text;
using JSini.Web.Components.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 지연 현황의 「ProjectView 실적시작일 전송」 콘솔 스크립트(참고소스 pvScript.js 의 buildPvDateScript).
/// 포털이 ProjectView API 를 직접 부르지 못해(사내망 · 로그인 쿠키가 ProjectView 쪽에만 있다) 값을 스크립트에 박아 그쪽 탭에서 돌린다.
/// 스크립트 본문은 원본과 글자 하나 다르지 않게 옮겼다 — ProjectView 의 저장 규약(진척률을 되돌려 보내기 · DRY RUN)이 거기 들어 있다.
/// </summary>
public static class WbsPvDateScript
{
    /// <summary>보낼 한 건. <paramref name="Date"/> 는 yyyy-MM-dd.</summary>
    public sealed record Item(string ActivityId, string Date);

    /// <param name="items">보낼 것.</param>
    /// <param name="label">머리말에 적을 「보낼 값」.</param>
    /// <param name="ids">캐시의 activity_id → workId. <b>전부</b> 있을 때만 박는다 — 하나라도 비면 스크립트가 목록을 통째로 조회해야 해서 반쪽 표는 쓸모가 없다.</param>
    /// <param name="projectId">캐시의 ProjectView 프로젝트 ID. ids 를 박을 때만 함께 박는다.</param>
    public static string Build(
        IReadOnlyList<Item> items, string label,
        IReadOnlyDictionary<string, string> ids, string? projectId)
    {
        var data = items
            .Where(i => !string.IsNullOrWhiteSpace(i.ActivityId) && !string.IsNullOrWhiteSpace(i.Date))
            .Select(i => (Code: i.ActivityId, Date: i.Date.Length > 10 ? i.Date[..10] : i.Date))
            .ToList();

        var dataJson = new StringBuilder("[\n");
        foreach (var d in data)
        {
            dataJson.Append($"    {{ code: '{d.Code}', date: '{d.Date}' }},\n");
        }
        dataJson.Append("  ]");

        var hits = data.Where(d => ids.ContainsKey(d.Code)).Select(d => $"    '{d.Code}': '{ids[d.Code]}',").ToList();
        var full = data.Count > 0 && hits.Count >= data.Count;
        var idsJson = full ? "{\n" + string.Join('\n', hits) + "\n  }" : "{}";

        return Template
            .Replace("__STAMP__", AppTime.UtcNow.Kst("yyyy-MM-dd HH:mm:ss"))
            .Replace("__COUNT__", data.Count.ToString())
            .Replace("__LABEL__", label)
            .Replace("__PROJECT_ID__", full ? projectId ?? "" : "")
            .Replace("__IDS__", idsJson)
            .Replace("__DATA__", dataJson.ToString())
            .Replace("__COMMON__", Common);
    }

    /// <summary>projectId 탐지 · 공통 호출 · code→workId 매핑. 원본의 COMMON 그대로다.</summary>
    private const string Common = """

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
      'projectId 를 찾지 못했습니다.\n' +
      'WBS 목록이 보이는 상태(작업 화면 진입 후)에서 다시 실행하거나,\n' +
      '스크립트 상단 PROJECT_ID 에 PJT_... 값을 직접 넣어주세요.');
    return;
  }
  console.log('projectId =', projectId);

  const qs = (u, k) => (String(u).match(new RegExp('[?&]' + k + '=([^&]*)')) || [])[1] || '';

  const H = (dts, pg) => ({
    'accept': 'application/json',
    'content-type': 'application/json',
    'x-portalid': 'AAC',
    'x-pageid': pg || PAGE,
    'x-dataserviceid': dts,
    'x-skiperrorhandler': 'N',
  });

  // x-pageid 헤더는 URL 의 pageId 와 같아야 한다. 화면이 이미 부른 주소를 그대로 쓸 때
  // 상수 PAGE 와 다를 수 있어, URL 에 pageId 가 있으면 그 값을 따라간다.
  const call = async (url, dts, opt = {}) => {
    const r = await fetch(url, { credentials: 'include', headers: H(dts, qs(url, 'pageId')), ...opt });
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

  // 응답 껍데기가 버전마다 다르다. { data: { content: [...] } } 처럼 한 겹 더 들어간 것도 받아 준다.
  const unwrap = (d) => {
    if (Array.isArray(d)) return d;
    if (!d || typeof d !== 'object') return [];
    for (const k of ['works', 'content', 'list', 'data', 'items', 'rows', 'result', 'resultList']) {
      const v = d[k];
      if (Array.isArray(v)) return v;
      if (v && typeof v === 'object') { const inner = unwrap(v); if (inner.length) return inner; }
    }
    return [];
  };

  // 필드 이름도 버전마다 다르다 — 같은 뜻의 이름을 함께 본다.
  // code/id 만 표준 이름으로 붙이고 나머지 필드는 원본 그대로 둔다
  // (뒤에서 LAST_LIST 의 날짜·진척률을 그대로 읽는다).
  const pickKey = (x, keys) => {
    for (const k of keys) {
      const v = x[k];
      if (v !== null && v !== undefined && v !== '') return String(v);
    }
    return '';
  };
  const normalize = (arr) => arr.map((x) => {
    if (!x || typeof x !== 'object') return null;
    const code = pickKey(x, ['code', 'workCode', 'activityCode', 'actCode', 'wbsCode']);
    const id   = pickKey(x, ['id', 'workId', 'activityId']);
    if (!code || !id) return null;
    return (x.code === code && x.id === id) ? x : { ...x, code, id };
  }).filter(Boolean);

  // 목록 주소 후보 — 확실한 순서대로.
  //  ① LIST_URL 을 직접 넣었으면 그것만 쓴다
  //  ② 이 화면이 방금 성공시킨 /works 목록 요청 (가장 확실하다)
  //  ③ 그래도 없으면 dataServiceId 를 더듬는다
  // 전에는 ③ 만 있었고 범위도 00001~00010 뿐이라, 목록 서비스 번호가 그 밖이거나
  // 목록 주소에 화면이 붙이는 조회조건이 필요하면 전부 빈손으로 끝났다.
  function listCandidates() {
    if (LIST_URL) return [LIST_URL];
    const out = [];
    const seen = new Set();
    // performance 는 절대주소를, 우리가 만드는 것은 /projectview/... 를 준다.
    // 한 모양으로 맞춰야 같은 주소를 두 번 두드리지 않는다.
    const add = (u) => {
      const n = String(u).replace(/^https?:[/][/][^/]+/, '');
      if (!seen.has(n)) { seen.add(n); out.push(n); }
    };
    try {
      for (const e of performance.getEntriesByType('resource')) {
        const u = String(e.name);
        // /works 목록만 — /works/WRK_xxx (단건) · /works/export-excel 은 뺀다
        if (/[/]works([?]|$)/.test(u) && u.indexOf(projectId) >= 0) add(u);
      }
    } catch { /* 무시 */ }
    for (let i = 1; i <= 24; i++) {
      add(BASE + '/projects/' + projectId + '/works?pageId=' + PAGE +
          '&dataServiceId=DTS_PSW_' + String(i).padStart(5, '0'));
    }
    return out;
  }

  async function buildMap() {
    const cands = listCandidates();
    // LIST_BODY 를 넣었을 때만 POST 로 부른다 — 함부로 POST 를 쏘지 않기 위해서다.
    const body = (typeof LIST_BODY !== 'undefined' && LIST_BODY) ? LIST_BODY : null;
    const method = ((typeof LIST_METHOD !== 'undefined' && LIST_METHOD) || (body ? 'POST' : 'GET')).toUpperCase();
    const tried = [];

    for (const u of cands) {
      const dts = qs(u, 'dataServiceId') || DTS_GET;
      // 확인된 주소(①) 에만 지정 메서드를 쓰고, 더듬는 후보는 GET 으로만 간다
      const opt = (u === cands[0] && method !== 'GET')
        ? { method, body: typeof body === 'string' ? body : JSON.stringify(body || {}) }
        : {};
      try {
        const d = await call(u, dts, opt);
        const rows = unwrap(d);
        const arr = normalize(rows);
        const m = new Map(arr.map((x) => [x.code, x.id]));
        if (m.size) { LAST_LIST = arr; console.log('%c목록 조회 성공', 'color:#2f6fd0', u, m.size + '건'); return m; }
        tried.push({ 주소: u, 결과: rows.length + '행 왔지만 code/id 를 찾지 못함' });
      } catch (e) {
        tried.push({ 주소: u, 결과: String((e && e.message) || e).slice(0, 90) });
      }
    }

    console.group('%cwork 목록 후보 ' + tried.length + '건 — 모두 실패', 'color:#c0392b;font-weight:bold');
    console.table(tried);
    console.groupEnd();
    throw new Error(
      'work 목록 URL 을 찾지 못했습니다.\n' +
      '1) WBS 목록이 화면에 보이는 상태(필요하면 F5 로 다시 조회)에서 다시 실행해 주세요.\n' +
      '   이 스크립트는 화면이 방금 부른 목록 주소를 가장 먼저 씁니다.\n' +
      '2) 그래도 안 되면 F12 → Network 에서 목록 조회 요청을 골라 주소를 복사해\n' +
      '   스크립트 상단 LIST_URL 에 넣고 다시 실행하세요.\n' +
      '   그 요청이 POST 라면 Payload 도 함께 LIST_BODY 에 넣어야 합니다.\n' +
      '   (위 표에 방금 두드려 본 주소와 응답이 그대로 찍혀 있습니다.)');
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

""";

    /// <summary>원본 TEMPLATE_DATE. 자리표시 __이름__ 을 Build 가 갈아 끼운다.</summary>
    private const string Template = """
/* ============================================================
 * WBS 대시보드 → ProjectView 실적시작일 전송
 * 생성: __STAMP__
 * 대상: __COUNT__건  ·  보낼 값: __LABEL__
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
  let   LIST_BODY = null;             // 그 요청이 POST 면 Payload 도 함께 (넣으면 POST 로 부른다)
  let   LIST_METHOD = '';             // 메서드를 직접 정해야 할 때만 (기본: 본문이 있으면 POST)

  let   PROJECT_ID = '__PROJECT_ID__';   // 대시보드 캐시의 값 (비면 자동 탐지)

  const PAGE    = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';    // 단건 조회
  const DTS_PUT = 'DTS_PSW_00003';    // 저장
  const BASE    = '/projectview/api/v1/promise';

  // 대시보드 캐시의 activity_id → workId. 있으면 목록 조회를 건너뛴다.
  const IDS = __IDS__;

  // 전송할 값: [{ code: activity_id, date: 'YYYY-MM-DD' }]
  const DATA = __DATA__;
__COMMON__
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

""";
}
