using System.Globalization;
using System.Text;
using JSini.Web.Components.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>진척률 현황의 「ProjectView 전송 스크립트」. 참고소스 pvScript.js 의 buildPvScript 를 글자 그대로 옮겼다.</summary>
/// 스크립트를 붙여 넣게 하는 까닭: ProjectView 는 사내망 HTTPS 이고 로그인 쿠키가 그 탭에만 있어, 포털이 직접 부를 길이 없다.
public static class WbsPvProgressScript
{
    /// <summary>이 값 이상은 <see cref="FullRate"/> 로 올려 보낸다 — 89.7% 같은 값은 ProjectView 에서 「안 끝남」으로 읽힌다.</summary>
    public const decimal FullAt = 90;

    public const decimal FullRate = 100;

    /// <summary>보낼 값. 90% 이상은 100 으로 올린다.</summary>
    public static decimal ToSend(decimal rate) => rate >= FullAt ? FullRate : rate;

    /// <param name="items">(Activity ID, 진척률). 올리기 전 값이다.</param>
    /// <param name="label">보낼 값의 이름(「계획진척률」).</param>
    /// <param name="ids">대시보드 캐시의 Activity ID → ProjectView workId. 모든 대상에 있어야 목록 조회를 건너뛴다.</param>
    /// <param name="projectId">캐시의 ProjectView 프로젝트 아이디.</param>
    public static string Build(
        IReadOnlyList<(string Code, decimal Rate)> items, string label,
        IReadOnlyDictionary<string, string> ids, string? projectId)
    {
        var data = items.Select(i => (i.Code, Rate: ToSend(i.Rate))).ToList();
        var full = items.Count(i => i.Rate >= FullAt);

        var dataJson = "[\n" + string.Join("\n", data.Select(d => $"    {{ code: '{d.Code}', rate: {Num(d.Rate)} }},")) + "\n  ]";

        var hits = data.Where(d => ids.ContainsKey(d.Code)).Select(d => $"    '{d.Code}': '{ids[d.Code]}',").ToList();
        var allHit = data.Count > 0 && hits.Count >= data.Count;
        var idsJson = allHit ? "{\n" + string.Join("\n", hits) + "\n  }" : "{}";

        var cached = hits.Count == 0 ? "없음 — 예전처럼 work 목록을 통째로 조회한다"
            : hits.Count >= data.Count ? $"{hits.Count}건 모두 workId 보유 — 목록 조회를 건너뛴다"
            : $"{hits.Count}/{data.Count}건만 workId 보유 — 부족하므로 목록을 조회한다 (캐시를 다시 수집할 것)";

        return new StringBuilder(Template)
            .Replace("@@meta.stamp@@", AppTime.UtcNow.Kst("yyyy-MM-dd HH:mm:ss"))
            .Replace("@@meta.count@@", data.Count.ToString(CultureInfo.InvariantCulture))
            .Replace("@@meta.label@@", label)
            .Replace("@@meta.fullAt@@", Num(FullAt))
            .Replace("@@meta.fullRate@@", Num(FullRate))
            .Replace("@@meta.full@@", full.ToString(CultureInfo.InvariantCulture))
            .Replace("@@meta.cached@@", cached)
            .Replace("@@meta.projectId@@", allHit ? projectId ?? "" : "")
            .Replace("@@idsJson@@", idsJson)
            .Replace("@@dataJson@@", dataJson)
            .ToString();
    }

    private static string Num(decimal v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    private const string Template = """
/* ============================================================
 * WBS 대시보드 → ProjectView 진척률 전송
 * 생성: @@meta.stamp@@
 * 대상: @@meta.count@@건  ·  보낼 값: @@meta.label@@
 * 올림: @@meta.fullAt@@% 이상은 @@meta.fullRate@@% 로 올려 보낸다 — @@meta.fullRate@@% 로 나가는 항목 @@meta.full@@건
 * 캐시: @@meta.cached@@
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
 *        @@meta.fullAt@@% 이상은 @@meta.fullRate@@% 로 올려 보낸다 — 그 미만은 계산된 값 그대로다.
 *        100% 로 올라간 건은 ProjectView 에서 <완료> 로 읽히니 대상 목록을 확인하고 보낼 것.
 * ============================================================ */
(async () => {
  const DRY_RUN = true;               // ← false 로 바꾸면 실제 PUT 전송
  const CONCURRENCY = 4;              // 동시 처리 수 (읽기+쓰기). 서버가 버거우면 1~2 로 낮춘다
  let   LIST_URL = '';                // work 목록 조회 URL 을 알고 있으면 여기에 붙여넣기
  let   LIST_BODY = null;             // 그 요청이 POST 면 Payload 도 함께 (넣으면 POST 로 부른다)
  let   LIST_METHOD = '';             // 메서드를 직접 정해야 할 때만 (기본: 본문이 있으면 POST)

  let   PROJECT_ID = '@@meta.projectId@@';   // 대시보드 캐시의 값 (비면 자동 탐지)

  const PAGE    = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';    // 단건 조회
  const DTS_PUT = 'DTS_PSW_00003';    // 저장
  const BASE    = '/projectview/api/v1/promise';

  // 대시보드 캐시의 activity_id → workId. 있으면 목록 조회를 건너뛴다.
  const IDS = @@idsJson@@;

  // 전송할 값: [{ code: activity_id, rate: 진척률(%) }]
  const DATA = @@dataJson@@;

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
""";
}
