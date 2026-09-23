// ProjectView 워크플로(task-nodes) 의 빈 일자·담당자를 채우는 콘솔 스크립트 생성기.
//
// 왜 콘솔 스크립트인가 (pvScript.js 와 같은 이유):
//  - task-nodes 는 PUT + JSON 본문이라 CORS 프리플라이트(OPTIONS)가 도는데 ProjectView 가 405 를 준다.
//    그래서 대시보드 페이지에서 직접 부를 수 없고, ProjectView 탭에서 같은 출처로 실행해야 한다.
//  - ProjectView 탭에서 실행하면 로그인 쿠키(promise-token)가 자동으로 붙는다.
//
// 구조 (액티비티 → 일감 → 워크플로 노드)
//  work  WORK_xxx  = 액티비티 (AAC-A1022)  · GET  /works/{workId}            DTS_PSW_00005
//  task  TASK_xxx  = 일감     (AAC-T1241)  · work.taskList 로 얻는다
//  node            = 워크플로 단계 (상태)
//                    읽기 GET  /task?taskCode=AAC-T1241        DTS_PST_00052  (일감 상세)
//                    쓰기 PUT  /tasks/{taskId}/task-nodes     DTS_PST_00063
//                    ※ task-nodes 는 저장 전용이다. GET 하면 405 를 준다.
//
// 노드의 일자·담당자 필드 이름과 담당자 키를 우리가 모르므로 스크립트가 실행 시점에 배운다.
// 학습 규칙 — "템플릿 액티비티(모든 노드가 채워져 있음)에서는 값이 있고,
//              대상 노드에서는 비어 있는 키" = 채워야 할 키.
//   그중 값이 날짜 모양이면 일자 키, 나머지는 담당자 키로 보고 템플릿의 값을 그대로 쓴다.
//   → 담당자 키(사번/ID)를 몰라도 템플릿에서 그대로 복사되므로 사람 이름을 추측하지 않는다.

import { COMMON } from './pv-script-core.js'

const TEMPLATE_FLOW = (codesJson, meta) => `/* ============================================================
 * ProjectView 워크플로 일자 · 담당자 채우기
 * 생성: ${meta.stamp}
 * 대상: 액티비티 ${meta.count}건 (대시보드가 관리하는 전체)
 * 학습: ${meta.template}  ·  담당자 기본 ${meta.defaultNm} · 단계별 ${meta.stageJson}
 *
 * 하는 일 — 각 액티비티의 모든 일감(task)의 워크플로 노드를 읽어,
 *   일자와 담당자가 <비어 있는 노드만> 채운다. 이미 값이 있는 노드는 손대지 않는다.
 *     · 일자   ← 그 일감의 완료일(planEndDate). 시작일·실적일자는 건드리지 않는다.
 *     · 제외   ← SKIP_STAGES 에 든 단계는 아예 손대지 않는다 (비워 둔다).
 *     · 담당자 ← 프로젝트 사용자 목록에서 <이름으로 찾은 USR- id>
 *              (노드에는 담당자 이름이 없고 id 만 있다. 이름을 추측해 넣지 않는다.)
 *
 * [사용법]
 *  1) ProjectView 로그인 탭에서 F12 → Console 에 붙여넣고 Enter
 *     → 읽기만 하고 저장하지 않는다 (DRY RUN). 학습 결과와 바꿀 목록을 표로 보여 준다.
 *  2) 결과 JSON 이 클립보드에 복사된다. 대시보드의 [워크플로 채우기] 화면에 붙여넣어
 *     내용을 확인한다 (막히면 콘솔에  copy(pvFlowReport) ).
 *  3) 맞으면 콘솔에  pvSend()  입력 + Enter → 실제 PUT 전송
 *
 * [안전장치]
 *  · 빈 값만 채운다 (덮어쓰기 없음)
 *  · 일감의 계획일자는 바꾸지 않는다 (isChangePlanDate = false, 조회한 값을 그대로 되돌려 보냄)
 *  · 완료일이 없는 일감은 건너뛰고 사유를 남긴다
 *  · 처음에는 MAX_TASKS 를 1~2 로 두고 한두 건만 반영해 화면에서 확인하는 것을 권한다
 * ============================================================ */
(async () => {
  const DRY_RUN = true;               // ← 그대로 두고, 확인 후 콘솔에서 pvSend() 를 부른다
  const TEMPLATE_CODE = '${meta.template}';   // 일자·담당자가 모두 채워진 학습용 액티비티
  // 담당자 규칙 — 단계(상태) 이름별로 다르다. 여기 없는 단계는 기본 담당자를 쓴다.
  const WORKER_BY_STAGE = ${meta.stageJson};
  const WORKER_DEFAULT  = '${meta.defaultNm}';
  // 아예 건드리지 않을 단계 (비워 두기로 한 단계)
  const SKIP_STAGES = ${meta.skipJson};
  const MAX_TASKS = 0;                // 0 = 전체. 시험 반영은 1~2 로 줄여서 한다
  const ONLY_ACT = '';                // 특정 액티비티만 처리하려면 'AAC-A1023' 처럼 넣는다
  const CONCURRENCY = 5;              // 조회 동시 실행 수
  const CHANGE_PLAN_DATE = false;     // 일감의 계획일자는 건드리지 않는다

  let   LIST_URL = '';                // work 목록 조회 URL 을 알고 있으면 여기에 붙여넣기
  let   PROJECT_ID = '';              // 자동 탐지가 실패할 때만 PJT_... 를 직접 넣기

  const PAGE    = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';    // 작업(액티비티) 단건 조회
  const PAGE_T  = 'PGE_PST_TaskDetailPage';
  const DTS_NODE = 'DTS_PST_00063';   // 워크플로 노드 저장(PUT) — 캡처로 확인된 값
  const DTS_TASK = 'DTS_PST_00052';   // 일감 상세 조회(GET) — 화면이 워크플로를 그릴 때 쓰는 요청
  const DTS_USER = 'DTS_PSZ_00014';   // 프로젝트 사용자 목록 — 담당자 이름으로 USR- id 를 찾는다
  const DTS_WFNODE = 'DTS_PST_00004'; // 워크플로 노드 정의 — WFN_ id 로 단계를 알아낸다
  const DTS_WFSTAT = 'DTS_PST_00018'; // 워크플로 상태 정의 — 단계 이름
  const ONLY_END_DATE = true;         // 일자는 <완료일>만 채운다 (시작일은 건드리지 않는다)
  // 워크플로 조회 URL. 비우면 이 탭이 부른 요청 기록에서 찾아낸다.
  // 자동으로 못 찾으면 F12 → Network 에서 워크플로를 읽어 오는 GET 요청 URL 을 통째로 붙여넣는다.
  let   NODE_GET_URL = '';
  const BASE    = '/projectview/api/v1/promise';

  // 대시보드가 관리하는 activity_id
  const CODES = ${codesJson};
${COMMON}
  // ---------------------------------------------------------------- 도구
  const isEmpty = (v) => v === null || v === undefined || (typeof v === 'string' && v.trim() === '');

  // '2026-08-25' / '20260825' / '2026-08-25T00:00:00' → '2026-08-25'
  const day = (v) => {
    if (isEmpty(v)) return null;
    const s = String(v).trim();
    if (s.length === 8 && !isNaN(Number(s))) return s.slice(0, 4) + '-' + s.slice(4, 6) + '-' + s.slice(6, 8);
    return s.slice(0, 10);
  };
  const compact = (d) => (d ? d.split('-').join('') : d);

  // 날짜처럼 보이는 값인가 (정규식 없이 — 생성기 문자열 안에서 역슬래시를 피한다)
  const isDateish = (v) => {
    if (isEmpty(v)) return false;
    if (typeof v === 'object') return false;
    const s = String(v).trim();
    if (s.length >= 10 && s.charAt(4) === '-' && s.charAt(7) === '-') return true;
    if (s.length === 8 && !isNaN(Number(s))) return true;
    return false;
  };
  const sv = (v) => (v && typeof v === 'object' ? JSON.stringify(v) : String(v));

  const workUrl = (id) =>
    BASE + '/projects/' + projectId + '/works/' + id + '?pageId=' + PAGE + '&dataServiceId=' + DTS_GET;
  const nodeUrl = (id, dts) =>
    BASE + '/projects/' + projectId + '/tasks/' + id + '/task-nodes?pageId=' + PAGE_T +
    (dts ? '&dataServiceId=' + dts : '');
  const taskUrl = (id, dts) =>
    BASE + '/projects/' + projectId + '/tasks/' + id + '?pageId=' + PAGE_T +
    (dts ? '&dataServiceId=' + dts : '');

  // 워크플로 조회/저장은 pageId 가 달라 별도 헤더를 쓴다
  const HT = (dts) => ({
    'accept': 'application/json',
    'content-type': 'application/json',
    'x-portalid': 'AAC',
    'x-pageid': PAGE_T,
    'x-dataserviceid': dts || DTS_NODE,
    'x-skiperrorhandler': 'N',
  });
  const callT = async (url, dts, opt) => {
    const r = await fetch(url, Object.assign({ credentials: 'include', headers: HT(dts) }, opt || {}));
    const t = await r.text();
    if (!r.ok) {
      if (r.status === 401 || r.status === 403)
        throw new Error(r.status + ' 인증 만료 — ProjectView 재로그인 후 다시 실행');
      throw new Error(r.status + ' ' + r.statusText + (t ? ' · ' + t.slice(0, 160) : ''));
    }
    return t ? JSON.parse(t) : null;
  };

  async function pool(items, fn, n) {
    const q = items.slice();
    const out = [];
    let done = 0;
    const worker = async () => {
      while (q.length) {
        const it = q.shift();
        if (it === undefined) break;
        try {
          const r = await fn(it);
          if (r !== undefined) out.push(r);
        } catch (e) {
          out.push({ error: String(e.message || e), item: it });
        }
        done++;
        if (done % 25 === 0) console.log('   ... ' + done + '/' + items.length);
      }
    };
    await Promise.all(Array.from({ length: n }, worker));
    return out;
  }

  // ---------------------------------------------------------------- 조회
  async function tasksOf(code) {
    const workId = map.get(code);
    if (!workId) return { code: code, tasks: [], why: 'workId 없음(프로젝트에 미등록)' };
    const got = await call(workUrl(workId), DTS_GET);
    const w = Array.isArray(got) ? got[0] : got;
    if (!w) return { code: code, tasks: [], why: '작업 조회 결과 없음' };
    const list = w.taskList || w.tasks || [];
    const tasks = list
      .map((t) => ({
        id: t.id || t.taskId || t.taskKey,
        code: t.code || t.taskCode || '',
        title: t.title || t.name || t.taskNm || '',
        planStartDate: day(t.planStartDate),
        planEndDate: day(t.planEndDate || t.endDate),
      }))
      .filter((t) => t.id);
    return { code: code, workId: workId, title: w.title, tasks: tasks };
  }

  // ---------------------------------------------------------------- 사용자 목록
  // 노드에는 담당자 이름이 없고 workerId(USR-...) 만 들어 있다.
  // 그래서 프로젝트 사용자 목록에서 이름으로 id 를 찾는다. 이름을 추측해 넣지 않기 위한 것이다.
  const USERS = { count: 0, byName: {}, byId: {} };

  async function loadUsers() {
    const url = BASE + '/projects/' + projectId + '/user?dataServiceId=' + DTS_USER;
    const d = await callT(url, DTS_USER);
    const arr = Array.isArray(d) ? d : (d && (d.content || d.list || d.data || d.items)) || [];
    for (const x of arr) {
      if (!x || typeof x !== 'object') continue;
      let id = '';
      const names = [];
      for (const k of Object.keys(x)) {
        const v = x[k];
        if (typeof v !== 'string' || !v) continue;
        if (!id && v.indexOf('USR-') === 0) { id = v; continue; }
        if (v.length <= 40 && v.indexOf('USR-') !== 0) names.push(v);
      }
      if (!id) continue;
      USERS.byId[id] = names;
      for (const n of names) if (!(n in USERS.byName)) USERS.byName[n] = id;
    }
    USERS.count = arr.length;
    return arr.length;
  }

  const nameOf = (id) => {
    const n = USERS.byId[id];
    return n && n.length ? n[0] : '(이름 모름)';
  };

  // ---------------------------------------------------------------- 워크플로 단계
  // 일감의 노드에는 단계 이름이 없고 nodeId(WFN_...) 만 있다.
  // 프로젝트의 워크플로 정의에서 WFN_ → 단계 이름을 만든다.
  const STAGE = { byNode: {}, nodes: 0, status: 0, names: [], sample: {} };

  // 단계 이름은 아래 키에서만 찾는다.
  // 'name' 이 든 키를 아무거나 집으면 userGroupName('전체') 같은 것을 집게 된다 — 실제로 그랬다.
  const NAME_KEYS = ['statusName', 'nodeName', 'stageName', 'workflowStatusName',
                     'statusNm', 'nodeNm', 'name', 'title', 'label', 'displayName'];
  const pickName = (o) => {
    for (const k of NAME_KEYS) {
      const v = o[k];
      if (typeof v === 'string' && v.trim() && v.indexOf('WF') !== 0) return v.trim();
    }
    return '';
  };
  const arrOf = (d) => (Array.isArray(d) ? d : (d && (d.content || d.list || d.data || d.items)) || []);

  async function loadStages() {
    const q = '?pageId=loadProjectInitData&dataServiceId=';
    const st = arrOf(await callT(BASE + '/projects/' + projectId + '/workflows/status' + q + DTS_WFSTAT, DTS_WFSTAT));
    const stName = {};
    for (const x of st) if (x && x.id) stName[x.id] = pickName(x);
    STAGE.status = st.length;

    const nd = arrOf(await callT(BASE + '/projects/' + projectId + '/workflows/nodes' + q + DTS_WFNODE, DTS_WFNODE));
    for (const x of nd) {
      if (!x || !x.id) continue;
      // 노드 정의의 이름보다 상태(status) 이름이 우선이다 — 단계 이름은 상태에 붙어 있다
      STAGE.byNode[x.id] = stName[x.statusId] || pickName(x) || '';
    }
    STAGE.nodes = nd.length;
    STAGE.sample = { node: nd[0] || null, status: st[0] || null };
    const seenNm = {};
    for (const id of Object.keys(STAGE.byNode)) {
      const v = STAGE.byNode[id];
      if (v) seenNm[v] = true;
    }
    STAGE.names = Object.keys(seenNm);
  }

  const stageOf = (nd) =>
    STAGE.byNode[nd.nodeId] || STAGE.byNode[nd.taskNodeId] || STAGE.byNode[nd.statusId] || '';

  // 그 노드의 담당자 이름 (단계별 규칙)
  const workerNmOf = (nd) => {
    const st = stageOf(nd);
    return (st && WORKER_BY_STAGE[st]) || WORKER_DEFAULT;
  };

  // ---------------------------------------------------------------- 조회 경로 탐색
  // 화면이 워크플로를 그릴 때 쓰는 요청은 일감 상세 조회다 (로드 캡처로 확인).
  //   GET /projects/{pj}/task?pageId=PGE_PST_TaskDetailPage&dataServiceId=DTS_PST_00052&taskCode=AAC-T1241
  // task-nodes 로는 읽을 수 없다 — 그쪽은 저장(PUT) 전용이라 GET 이 405 다.
  // 위 요청을 1순위로 쓰고, 안 되면 몇 가지 변형과 이 탭이 부른 요청 재생을 차례로 시험한다.
  const NODE_SRC = { url: '', dts: '', tried: 0, seen: [], nodeCount: 0 };

  const qval = (u, k) => {
    const i = u.indexOf(k + '=');
    return i < 0 ? '' : u.slice(i + k.length + 1).split('&')[0];
  };

  // 절대 URL 에서 /projectview 부터만 남긴다 (같은 출처로만 부른다)
  const relOf = (u) => {
    const i = u.indexOf('/projectview');
    return i < 0 ? '' : u.slice(i);
  };

  // 쿼리 한 개의 값을 바꾼다
  function setQ(u, k, v) {
    const key = k + '=';
    const i = u.indexOf(key);
    if (i < 0 || !v) return u;
    const rest = u.slice(i + key.length);
    const amp = rest.indexOf('&');
    return u.slice(0, i + key.length) + encodeURIComponent(v) + (amp < 0 ? '' : rest.slice(amp));
  }

  // 어떤 일감의 URL 이든 우리 일감으로 바꿔 준다
  //  · 경로의 /tasks/TASK_xxx 자리
  //  · 쿼리의 taskCode= / taskId=
  function applyTask(url, task) {
    let u = url;
    const i = u.indexOf('/tasks/');
    if (i >= 0 && task.id) {
      const rest = u.slice(i + 7);
      let end = rest.length;
      const a = rest.indexOf('/');
      const b = rest.indexOf('?');
      if (a >= 0 && a < end) end = a;
      if (b >= 0 && b < end) end = b;
      u = u.slice(0, i + 7) + task.id + rest.slice(end);
    }
    u = setQ(u, 'taskCode', task.code);
    u = setQ(u, 'taskId', task.id);
    return u;
  }

  // 이 탭이 부른 요청 중 일감·워크플로 관련 URL 을 모은다
  function seenTaskUrls() {
    const out = [];
    try {
      for (const e of performance.getEntriesByType('resource')) {
        const u = relOf(String(e.name));
        if (!u) continue;
        if (u.indexOf('/tasks/TASK_') < 0) continue;
        if (u.indexOf('/works/') >= 0) continue;
        const key = applyTask(u, { id: 'X', code: 'X' }); // 일감을 지운 모양으로 중복 제거
        if (out.some((x) => x.key === key)) continue;
        out.push({ key: key, url: u, dts: qval(u, 'dataServiceId') || '(없음)' });
      }
    } catch (e) { /* 무시 */ }
    return out;
  }

  // 응답 어딘가에 있는 '노드 배열' 을 찾는다 (이름에 node 가 든 키를 먼저 본다)
  function findNodeArray(d) {
    const looksNode = (a) =>
      Array.isArray(a) && a.length && a[0] && typeof a[0] === 'object' &&
      !Array.isArray(a[0]) && Object.keys(a[0]).length >= 4;
    function walk(o, depth, onlyNode) {
      if (!o || typeof o !== 'object' || depth > 4) return null;
      if (Array.isArray(o)) return !onlyNode && looksNode(o) ? o : null;
      const keys = Object.keys(o);
      const pri = keys.filter((k) => k.toLowerCase().indexOf('node') >= 0);
      for (const k of pri) if (looksNode(o[k])) return o[k];
      for (const k of (onlyNode ? pri : keys)) {
        const r = walk(o[k], depth + 1, onlyNode);
        if (r) return r;
      }
      return null;
    }
    return walk(d, 0, true) || walk(d, 0, false);
  }

  async function probe(url) {
    let r;
    try {
      r = await fetch(url, { credentials: 'include', headers: HT(qval(url, 'dataServiceId')) });
    } catch (e) {
      return null;
    }
    if (!r.ok) return null;
    const t = await r.text();
    if (!t) return null;
    try {
      return JSON.parse(t);
    } catch (e) {
      return null;
    }
  }

  async function findNodeSource(task) {
    const pj = BASE + '/projects/' + projectId;
    const q = '?pageId=' + PAGE_T + '&dataServiceId=';
    const cands = [];

    if (NODE_GET_URL) cands.push(relOf(NODE_GET_URL) || NODE_GET_URL);
    // 실제 화면이 쓰는 요청 (일감 상세 로드에서 확인한 것)
    if (task.code) cands.push(pj + '/task' + q + DTS_TASK + '&taskCode=' + encodeURIComponent(task.code));
    if (task.id) cands.push(pj + '/task' + q + DTS_TASK + '&taskId=' + task.id);
    if (task.id) cands.push(pj + '/tasks/' + task.id + q + DTS_TASK);
    // 그래도 안 되면 이 탭이 부른 일감 요청을 재생해 본다
    const seen = seenTaskUrls();
    NODE_SRC.seen = seen.map((s) => s.dts);
    for (const s of seen) if (cands.indexOf(s.url) < 0) cands.push(s.url);

    for (const c of cands) {
      const u = applyTask(c, task);
      NODE_SRC.tried++;
      const d = await probe(u);
      if (!d) continue;
      const arr = findNodeArray(d);
      if (arr && arr.length) {
        NODE_SRC.url = c;
        NODE_SRC.dts = qval(c, 'dataServiceId') || '(없음)';
        NODE_SRC.nodeCount = arr.length;
        return true;
      }
    }
    return false;
  }

  // 못 찾았을 때 — 이 탭이 무엇을 불렀고 각 요청이 무엇을 돌려주는지 그대로 찍는다.
  // 이 결과만 있으면 워크플로를 읽는 요청이 무엇인지 가려낼 수 있다.
  async function diagnose(task) {
    const tried = [];
    for (const s of seenTaskUrls()) {
      const u = applyTask(s.url, task);
      let status = '';
      let head = '';
      try {
        const r = await fetch(u, { credentials: 'include', headers: HT(qval(u, 'dataServiceId')) });
        status = r.status + ' ' + r.statusText;
        head = (await r.text()).slice(0, 300);
      } catch (e) {
        status = '요청 실패: ' + String(e.message || e);
      }
      tried.push({ url: s.url, dataServiceId: s.dts, status: status, 응답앞부분: head });
    }
    const allDts = [];
    const allTaskUrls = [];
    try {
      for (const e of performance.getEntriesByType('resource')) {
        const u = relOf(String(e.name));
        if (!u) continue;
        const d = qval(u, 'dataServiceId');
        if (d && allDts.indexOf(d) < 0) allDts.push(d);
        if (u.indexOf('/tasks/') >= 0 && allTaskUrls.indexOf(u) < 0) allTaskUrls.push(u);
      }
    } catch (e) { /* 무시 */ }
    return { task: task, tried: tried, allTaskUrls: allTaskUrls, allDataServiceIds: allDts };
  }

  async function nodesOf(task) {
    const d = await callT(applyTask(NODE_SRC.url, task), qval(NODE_SRC.url, 'dataServiceId'));
    const w = Array.isArray(d) ? d[0] : d;
    const box = w && typeof w === 'object' && !Array.isArray(w) ? w : {};
    return {
      nodes: findNodeArray(d) || [],
      planStartDate: day(box.planStartDate),
      planEndDate: day(box.planEndDate),
    };
  }

  const nodeLabel = (nd, i) => stageOf(nd) || nd.taskNodeNm || nd.nodeNm || nd.title || '#' + (i + 1);

  // ---------------------------------------------------------------- 1) 학습
  console.log('%c[1/4] 학습 — ' + TEMPLATE_CODE + ' 의 워크플로에서 채워진 키를 읽습니다',
              'font-weight:bold;color:#2f6fd0');
  const tm = await tasksOf(TEMPLATE_CODE);
  if (tm.why || !tm.tasks.length) {
    console.error('학습 대상을 읽지 못했습니다: ' + (tm.why || '일감이 없습니다'));
    return;
  }
  // 사용자 목록과 워크플로 단계 정의를 읽는다 (노드에는 담당자 이름도 단계 이름도 없다)
  try {
    const n = await loadUsers();
    console.log('   프로젝트 사용자 ' + n + '명');
  } catch (e) {
    console.error('사용자 목록을 읽지 못했습니다: ' + String(e.message || e));
  }
  try {
    await loadStages();
    console.log('   워크플로 단계 ' + STAGE.nodes + '개 (상태 ' + STAGE.status + '개)');
  } catch (e) {
    console.error('워크플로 단계 정의를 읽지 못했습니다: ' + String(e.message || e));
  }

  // 규칙에 나오는 담당자 이름을 모두 USR- id 로 바꿔 둔다 — 하나라도 없으면 중단한다
  const WORKER_ID = {};
  const wantNames = [WORKER_DEFAULT];
  for (const k of Object.keys(WORKER_BY_STAGE)) {
    const v = WORKER_BY_STAGE[k];
    if (wantNames.indexOf(v) < 0) wantNames.push(v);
  }
  const missing = [];
  for (const nm of wantNames) {
    const id = USERS.byName[nm];
    if (id) WORKER_ID[nm] = id;
    else missing.push(nm);
  }
  if (missing.length) {
    console.error('사용자 목록에서 찾지 못한 담당자: ' + missing.join(', ') + ' — 중단합니다.');
    console.log('   사용자 목록에 있는 이름 일부: ' + Object.keys(USERS.byName).slice(0, 40).join(', '));
    return;
  }
  console.log('   담당자 — ' + wantNames.map((n) => n + '=' + WORKER_ID[n]).join(' · '));
  // 규칙에 쓴 단계 이름이 실제 단계 이름과 맞는지 확인한다 — 안 맞으면 규칙이 조용히 무시된다
  const badRule = Object.keys(WORKER_BY_STAGE).filter((k) => STAGE.names.indexOf(k) < 0);
  const badSkip = SKIP_STAGES.filter((k) => STAGE.names.indexOf(k) < 0);
  if (badRule.length || badSkip.length) {
    console.error('단계 이름이 맞지 않습니다 — 규칙이 적용되지 않으므로 중단합니다.');
    if (badRule.length) console.error('  담당자 규칙에서 못 찾은 단계: ' + badRule.join(', '));
    if (badSkip.length) console.error('  제외 목록에서 못 찾은 단계: ' + badSkip.join(', '));
    console.log('   확인된 단계 이름 ' + STAGE.names.length + '개: ' + STAGE.names.join(', '));
    console.log('   노드 정의 표본:', STAGE.sample.node);
    console.log('   상태 정의 표본:', STAGE.sample.status);
    return;
  }
  console.log('   확인된 단계 이름: ' + STAGE.names.join(', '));
  console.log('   규칙 — ' +
    Object.keys(WORKER_BY_STAGE).map((k) => k + ' → ' + WORKER_BY_STAGE[k]).join(' · ') +
    (Object.keys(WORKER_BY_STAGE).length ? ' · ' : '') + '그 밖의 단계 → ' + WORKER_DEFAULT);

  // 조회용 dataServiceId 찾기 (저장용 00063 으로는 GET 이 405 다)
  const okSrc = await findNodeSource(tm.tasks[0]);
  if (!okSrc) {
    console.error('워크플로를 읽는 요청을 찾지 못했습니다 (' + NODE_SRC.tried + '건 시험).');
    console.log('%c진단 자료를 모으는 중입니다…', 'color:#c4692a');
    const diag = await diagnose(tm.tasks[0]);
    console.log('%c[진단] 이 탭이 부른 일감 요청과 응답', 'font-weight:bold;color:#c4692a');
    console.table(diag.tried);
    console.log('이 탭이 부른 모든 dataServiceId (' + diag.allDataServiceIds.length + '개)');
    console.log(diag.allDataServiceIds.join(', '));

    window.pvFlowDiag = JSON.stringify(diag);
    try {
      await navigator.clipboard.writeText(window.pvFlowDiag);
      console.log('%c진단 결과를 클립보드에 복사했습니다 — 이 내용을 그대로 알려 주세요.',
                  'font-size:13px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
    } catch (e) {
      console.log('%c콘솔에  copy(pvFlowDiag)  를 입력하면 복사됩니다.', 'font-weight:bold;color:#c4692a');
    }

    console.log('%c해결 방법', 'font-weight:bold;color:#c4692a');
    console.log('  1) 일감 상세 화면을 새로고침(F5)한 직후에 실행하세요.');
    console.log('     브라우저는 요청 기록을 250건까지만 갖고 있어 오래된 요청은 지워집니다.');
    console.log('  2) 그 화면에서 <워크플로가 실제로 보이도록> 합니다 (접혀 있으면 펼칩니다)');
    console.log('  3) 그 상태에서 F12 → Console 에 이 스크립트를 다시 붙여넣습니다');
    console.log('  4) 그래도 안 되면 F12 → Network 에서 워크플로 목록을 돌려주는 요청을 찾아');
    console.log('     그 URL 을 통째로 복사해 스크립트 상단 NODE_GET_URL 에 넣고 다시 실행하세요.');
    console.log('     (task-nodes 는 저장 전용이라 GET 이 405 입니다 — 조회는 다른 요청입니다.');
    console.log('      POST 로 조회하는 요청이면 그 URL 과 Payload 도 함께 알려 주세요.)');
    return;
  }
  console.log('   조회 경로 — ' + NODE_SRC.url);
  console.log('   노드 ' + NODE_SRC.nodeCount + '개 확인');
  console.log('   dataServiceId=' + NODE_SRC.dts + ' · ' + NODE_SRC.tried + '건 시험 후 성공');

  const tmplNodes = [];
  for (const t of tm.tasks) {
    const n = await nodesOf(t);
    for (const nd of n.nodes) tmplNodes.push({ node: nd, task: t, box: n });
  }
  if (!tmplNodes.length) {
    console.error('학습 대상의 워크플로 노드가 없습니다. 다른 액티비티를 TEMPLATE_CODE 로 지정하세요.');
    return;
  }
  console.log('   일감 ' + tm.tasks.length + '건 · 노드 ' + tmplNodes.length + '개');
  console.log('   노드 샘플(전체 필드):', tmplNodes[0].node);

  // ---------------------------------------------------------------- 2) 대상 수집
  const targets = CODES.filter((c) => c !== TEMPLATE_CODE && (!ONLY_ACT || c === ONLY_ACT));
  console.log('%c[2/4] 대상 수집 — 액티비티 ' + targets.length + '건', 'font-weight:bold;color:#2f6fd0');
  const works = await pool(targets, (c) => tasksOf(c), CONCURRENCY);
  const noWork = works.filter((w) => w.why || w.error).map((w) => ({
    액티비티: w.code || (w.item || ''),
    사유: w.why || w.error,
  }));
  const taskRefs = [];
  for (const w of works) for (const t of w.tasks || []) taskRefs.push({ act: w.code, task: t });
  // 조회를 동시에 돌리므로 완료 순서대로 쌓인다. MAX_TASKS 로 자를 때 대상이 매번 달라지지
  // 않도록 코드순으로 정렬한 뒤 자른다.
  taskRefs.sort((x, y) => {
    const c = String(x.act).localeCompare(String(y.act));
    return c !== 0 ? c : String(x.task.code).localeCompare(String(y.task.code));
  });
  const limited = MAX_TASKS > 0 ? taskRefs.slice(0, MAX_TASKS) : taskRefs;
  if (MAX_TASKS > 0 && limited.length)
    console.log('   MAX_TASKS=' + MAX_TASKS + ' — 대상: ' +
                limited.map((r) => r.act + ' / ' + r.task.code).join(', '));
  console.log('   일감 ' + taskRefs.length + '건' +
              (limited.length !== taskRefs.length ? ' → MAX_TASKS 로 ' + limited.length + '건만' : '') +
              ' · 워크플로를 읽습니다');
  const flows = await pool(limited, async (r) => {
    const n = await nodesOf(r.task);
    return { act: r.act, task: r.task, flow: n };
  }, CONCURRENCY);
  const readFail = flows.filter((f) => f.error).map((f) => ({
    일감: f.item && f.item.task ? f.item.task.code : '',
    오류: f.error,
  }));
  const good = flows.filter((f) => !f.error);
  const targetNodes = good.reduce((a, g) => a + g.flow.nodes.length, 0);
  console.log('   워크플로 읽음 — 일감 ' + good.length + '건 · 노드 ' + targetNodes + '개' +
              (readFail.length ? ' · 실패 ' + readFail.length + '건' : ''));

  // ---------------------------------------------------------------- 3) 키 판별
  const allKeys = {};
  for (const x of tmplNodes) for (const k of Object.keys(x.node)) allKeys[k] = true;

  const emptyCnt = {};
  for (const g of good)
    for (const nd of g.flow.nodes)
      for (const k of Object.keys(allKeys)) if (isEmpty(nd[k])) emptyCnt[k] = (emptyCnt[k] || 0) + 1;

  // 템플릿의 모든 노드에 값이 있고, 대상에는 빈 노드가 있는 키 = 채울 키
  const fillKeys = Object.keys(allKeys)
    .filter((k) => tmplNodes.every((x) => !isEmpty(x.node[k])))
    .filter((k) => (emptyCnt[k] || 0) > 0);

  // 날짜 모양인 키 전부 (담당자 키를 가려낼 때는 이 전체를 빼야 한다)
  const allDateKeys = fillKeys.filter((k) => tmplNodes.every((x) => isDateish(x.node[k])));
  let dateKeys = allDateKeys.slice();

  // 지시는 <계획 완료일> 이다. planEnd... 를 최우선으로 하고, 없으면 end 가 든 키를 쓴다.
  // 시작일과 실적일자는 건드리지 않는다.
  const dateSkipped = [];
  if (ONLY_END_DATE) {
    const planEnd = dateKeys.filter((k) => k.toLowerCase().indexOf('planend') >= 0);
    const pick = planEnd.length ? planEnd : dateKeys.filter((k) => k.toLowerCase().indexOf('end') >= 0);
    if (pick.length) {
      for (const k of dateKeys) if (pick.indexOf(k) < 0) dateSkipped.push(k);
      dateKeys = pick;
    }
  }
  const manKeys = fillKeys.filter((k) => allDateKeys.indexOf(k) < 0);

  // 담당자는 단계별 규칙으로 정한다 (PI검토 → 김선아, 그 밖 → 이순열 같은 식).
  // 노드에는 이름이 없고 USR- id 만 있으므로 사용자 목록에서 찾은 id 를 넣는다.
  // 담당자 키가 id 칸인지 이름 칸인지 가린다 (실제 데이터는 workerId 만 있다)
  const idKey = {};
  const manInfo = [];
  for (const k of manKeys) {
    idKey[k] = tmplNodes.some((x) => sv(x.node[k]).indexOf('USR-') === 0);
    const seen = {};
    for (const x of tmplNodes) {
      const st = stageOf(x.node) || '(단계 모름)';
      seen[st] = idKey[k] ? nameOf(sv(x.node[k])) : sv(x.node[k]);
    }
    manInfo.push({
      키: k,
      칸: idKey[k] ? 'USR- id' : '이름',
      채울규칙: Object.keys(WORKER_BY_STAGE).map((t) => t + '→' + WORKER_BY_STAGE[t]).join(', ') +
                ' / 그 밖 → ' + WORKER_DEFAULT,
      학습액티비티현재값: Object.keys(seen).map((t) => t + ':' + seen[t]).join(', ').slice(0, 120),
      대상빈노드: emptyCnt[k] || 0,
    });
  }
  // 노드 하나에 넣을 담당자 값
  const workerFor = (k, nd) => {
    const nm = workerNmOf(nd);
    return idKey[k] ? WORKER_ID[nm] : nm;
  };

  const fmtOf = {};
  const dateInfo = dateKeys.map((k) => {
    fmtOf[k] = String(tmplNodes[0].node[k]).indexOf('-') < 0 ? 'c' : 'd';
    const hit = tmplNodes.filter((x) => day(x.node[k]) === (x.box.planEndDate || x.task.planEndDate)).length;
    return {
      키: k,
      학습예시: sv(tmplNodes[0].node[k]).slice(0, 24),
      완료일과일치: hit + '/' + tmplNodes.length,
      대상빈노드: emptyCnt[k] || 0,
    };
  });

  console.log('%c[3/4] 학습 결과', 'font-weight:bold;color:#2f6fd0');
  console.log('   일자로 판단한 키'); console.table(dateInfo);
  console.log('   담당자로 판단한 키'); console.table(manInfo);

  if (!dateKeys.length)
    console.warn('일자로 볼 수 있는 키를 찾지 못했습니다. 위 [노드 샘플] 을 확인해 주세요 ' +
                 '(대상 노드의 일자가 이미 모두 채워져 있어도 이렇게 나옵니다).');
  if (!manKeys.length)
    console.warn('담당자로 볼 수 있는 키를 찾지 못했습니다. 위 [노드 샘플] 을 확인해 주세요.');
  if (dateSkipped.length)
    console.log('   일자 키 중 완료일이 아닌 것은 건드리지 않습니다: ' + dateSkipped.join(', '));

  // ---------------------------------------------------------------- 4) 계획
  const plan = [];
  const skipped = [];
  let skipNode = 0;   // 제외 단계라서 건드리지 않은 노드 수
  for (const g of good) {
    const end = g.flow.planEndDate || g.task.planEndDate;
    const patches = [];
    for (let i = 0; i < g.flow.nodes.length; i++) {
      const nd = g.flow.nodes[i];
      if (SKIP_STAGES.indexOf(stageOf(nd)) >= 0) { skipNode++; continue; }
      const p = {};
      for (const k of dateKeys) if (isEmpty(nd[k])) p[k] = fmtOf[k] === 'c' ? compact(end) : end;
      for (const k of manKeys) if (isEmpty(nd[k])) p[k] = workerFor(k, nd);
      if (Object.keys(p).length)
        patches.push({ i: i, nm: nodeLabel(nd, i), 담당자: manKeys.some((k) => k in p) ? workerNmOf(nd) : '', p: p });
    }
    if (!patches.length) continue;
    const needDate = patches.some((x) => dateKeys.some((k) => k in x.p));
    if (needDate && !end) {
      skipped.push({ 액티비티: g.act, 일감: g.task.code, 사유: '완료일(planEndDate)이 없어 건너뜀' });
      continue;
    }
    plan.push({ act: g.act, task: g.task, flow: g.flow, end: end, patches: patches });
  }

  const nodeCnt = plan.reduce((a, x) => a + x.patches.length, 0);
  const rowsOut = plan.map((x) => ({
    액티비티: x.act,
    일감: x.task.code,
    제목: String(x.task.title || '').slice(0, 26),
    노드: x.flow.nodes.length,
    채울노드: x.patches.length,
    넣을완료일: x.end,
  }));

  console.log('%c[4/4] ' + (DRY_RUN ? 'DRY RUN — 아직 아무것도 저장하지 않았습니다' : '반영 준비'),
              'font-size:13px;font-weight:bold;color:' + (DRY_RUN ? '#c4692a' : '#1f9254'));
  console.log('   채울 일감 ' + plan.length + '건 · 노드 ' + nodeCnt + '개' +
              (skipNode ? ' · 제외 단계(' + SKIP_STAGES.join(', ') + ')로 건드리지 않은 노드 ' + skipNode + '개' : ''));
  console.table(rowsOut.slice(0, 40));
  if (rowsOut.length > 40) console.log('   ... 표에는 앞 40건만 표시했습니다.');
  if (skipped.length) { console.warn('건너뜀 ' + skipped.length + '건'); console.table(skipped); }
  if (noWork.length) { console.warn('액티비티 조회 실패 ' + noWork.length + '건'); console.table(noWork.slice(0, 30)); }
  if (readFail.length) { console.warn('워크플로 조회 실패 ' + readFail.length + '건'); console.table(readFail.slice(0, 30)); }

  // 대시보드에 붙여넣어 확인할 보고서
  const report = {
    stamp: new Date().toISOString().slice(0, 19).replace('T', ' '),
    nodeSource: NODE_SRC,
    template: {
      code: TEMPLATE_CODE,
      tasks: tm.tasks.length,
      nodes: tmplNodes.length,
      sampleNode: tmplNodes[0].node,
      dateKeys: dateInfo,
      chargerKeys: manInfo,
      workerRule: { stage: WORKER_BY_STAGE, default: WORKER_DEFAULT, skip: SKIP_STAGES },
      workerIds: WORKER_ID,
      users: USERS.count,
      stages: { nodes: STAGE.nodes, status: STAGE.status, names: STAGE.names, sample: STAGE.sample },
      dateSkipped: dateSkipped,
    },
    scope: {
      activities: targets.length,
      tasksFound: taskRefs.length,
      tasksRead: good.length,
      nodesRead: targetNodes,
    },
    plan: rowsOut,
    detail: plan.slice(0, 200).map((x) => ({
      act: x.act,
      task: x.task.code,
      end: x.end,
      nodes: x.patches.map((pt) => ({ node: pt.nm, set: pt.p })),
    })),
    skipped: skipped,
    noWork: noWork,
    readFail: readFail,
    totals: { tasks: plan.length, nodes: nodeCnt, skippedNodes: skipNode },
  };
  window.pvFlowReport = JSON.stringify(report);
  try {
    await navigator.clipboard.writeText(window.pvFlowReport);
    console.log('%c결과를 클립보드에 복사했습니다 — 대시보드 [워크플로 채우기] 화면에 붙여넣어 확인하세요.',
                'font-size:13px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
  } catch (e) {
    console.log('%c자동 복사가 막혔습니다. 콘솔에  copy(pvFlowReport)  를 입력하면 복사됩니다.',
                'font-size:13px;font-weight:bold;color:#c4692a');
  }

  // ---------------------------------------------------------------- 반영
  async function apply() {
    if (!plan.length) { console.log('채울 항목이 없습니다.'); return; }
    const ok = [], fail = [];
    let n = 0;
    for (const x of plan) {
      const nodes = x.flow.nodes.map((nd) => Object.assign({}, nd));
      for (const pt of x.patches) Object.assign(nodes[pt.i], pt.p);

      const body = { taskNodes: nodes, isChangePlanDate: CHANGE_PLAN_DATE };
      // 계획일자는 조회한 값을 그대로 되돌려 보낸다 (모르면 아예 보내지 않는다)
      if (x.flow.planStartDate) body.planStartDate = x.flow.planStartDate;
      if (x.flow.planEndDate) body.planEndDate = x.flow.planEndDate;

      try {
        await callT(nodeUrl(x.task.id, DTS_NODE), DTS_NODE, { method: 'PUT', body: JSON.stringify(body) });
        ok.push({ 액티비티: x.act, 일감: x.task.code, 채운노드: x.patches.length, 완료일: x.end });
      } catch (e) {
        fail.push({ 액티비티: x.act, 일감: x.task.code, 오류: String(e.message || e) });
      }
      n++;
      if (n % 10 === 0) console.log('   ... ' + n + '/' + plan.length);
      await new Promise((r) => setTimeout(r, 60));
    }
    console.log('%c[전송 완료] 일감 ' + ok.length + '건 반영 · 실패 ' + fail.length + '건',
                'font-size:13px;font-weight:bold;color:' + (fail.length ? '#c4692a' : '#1f9254'));
    console.table(ok.slice(0, 40));
    if (fail.length) { console.error('실패 목록'); console.table(fail); }
    console.log('%cProjectView 화면을 새로고침(F5)하면 반영된 값이 보입니다.', 'color:#1f9254');
    return { ok: ok.length, fail: fail.length };
  }

  if (DRY_RUN) {
    window.pvSend = apply;
    console.log('%c▶ 실제로 반영하려면 콘솔에  pvSend()  를 입력하고 Enter 를 누르세요.',
                'font-size:14px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
    console.log('   (먼저 MAX_TASKS 를 1~2 로 바꿔 한두 건만 반영해 보는 것을 권합니다)');
  } else {
    await apply();
  }
  return { 채울일감: plan.length, 채울노드: nodeCnt };
})();
`

/**
 * 워크플로 일자·담당자 채우기 스크립트를 만든다.
 * @param codes    대시보드가 관리하는 activity_id 배열
 * @param template 학습용 액티비티 코드 (일자·담당자가 모두 채워진 것)
 * @param defaultNm 기본 담당자 이름
 * @param stageRule  단계별 담당자 { 'PI검토': '김선아' }
 * @param skipStages 건드리지 않을 단계 ['완료']
 * @param stamp    생성 시각 문자열
 */
export function buildPvFlowScript(codes, template, defaultNm, stageRule, skipStages, stamp) {
  const list = [...new Set(codes.filter(Boolean))].sort()
  const lines = []
  for (let i = 0; i < list.length; i += 6) {
    lines.push('    ' + list.slice(i, i + 6).map((c) => `'${c}'`).join(', ') + ',')
  }
  const json = '[\n' + lines.join('\n') + '\n  ]'
  const stageJson = JSON.stringify(stageRule || {})
  const skipJson = JSON.stringify(skipStages || [])
  return TEMPLATE_FLOW(json, { count: list.length, template, defaultNm, stageJson, skipJson, stamp })
}

// ---------------------------------------------------------------------------
// 요청 녹화 스크립트 — 워크플로를 읽어 오는 요청이 무엇인지 잡아낸다.
//
// performance 기록은 250건 한도라 SPA 를 돌아다니면 밀려 사라지고, 메서드·본문도 없다.
// 그래서 fetch 와 XMLHttpRequest 를 가로채 <메서드 · URL · 요청본문 · 응답본문> 을 남긴다.
// 응답까지 갖고 있으므로 pvFind('김선아') 한 번으로 담당자가 들어 있는 요청을 바로 찾는다.
const TEMPLATE_RECORD = (meta) => `/* ============================================================
 * ProjectView 요청 녹화 (읽기 전용 · 아무것도 바꾸지 않는다)
 * 생성: ${meta.stamp}
 *
 * 워크플로(단계·일자·담당자)를 읽어 오는 요청이 무엇인지 잡아내기 위한 것이다.
 * 요청과 <응답 내용>까지 함께 남기므로, 값으로 거꾸로 찾아낼 수 있다.
 *
 * [사용법]
 *  1) 일감 상세 화면에서 F12 → Console 에 붙여넣고 Enter (여기서 녹화 시작)
 *  2) 그 상태로 워크플로를 <다시 불러오게> 한다. 아래 중 아무거나:
 *     · 워크플로 영역이 접혀 있으면 펼치기
 *     · 다른 일감의 상세 화면을 열기  ← 가장 확실하다
 *     · 화면 안의 새로고침 버튼 (F5 로 페이지를 새로고침하면 녹화가 풀리니 주의)
 *  3) 콘솔에 아래 중 하나를 입력한다
 *     pvFind('김선아')   ← 담당자 이름이 들어 있는 요청을 찾는다 (가장 빠른 길)
 *     pvDump()          ← 일감·워크플로 관련 요청을 모두 보여준다
 *     pvDump('')        ← 녹화된 전체 요청
 *     pvShow(3)         ← 3번 요청의 응답 전체를 펼쳐 본다
 *  결과는 클립보드에 복사된다 (막히면 콘솔에  copy(pvRecJson) ).
 * ============================================================ */
(() => {
  if (window.__pvRec) {
    console.log("%c이미 녹화 중입니다. 워크플로를 다시 불러온 뒤 pvFind('김선아') 를 입력하세요.",
                'font-weight:bold;color:#c4692a');
    return;
  }
  const rec = [];
  window.__pvRec = rec;
  const CAP = 60000;        // 한 건당 보관할 응답 길이
  const CLIP = 8000;        // 클립보드로 내보낼 때 한 건당 응답 길이

  const put = (url, method, body) => {
    const e = {
      no: rec.length + 1,
      method: String(method || 'GET').toUpperCase(),
      url: String(url),
      body: typeof body === 'string' ? body.slice(0, 20000) : null,
      status: '',
      res: '',
    };
    rec.push(e);
    return e;
  };

  const of = window.fetch;
  window.fetch = function (u, o) {
    let e = null;
    try {
      e = put((u && u.url) || u, (o && o.method) || (u && u.method), o && o.body);
    } catch (x) { /* 무시 */ }
    const p = of.apply(this, arguments);
    if (e) {
      p.then(
        (r) => {
          e.status = r.status;
          try {
            r.clone().text().then((t) => { e.res = String(t).slice(0, CAP); }, () => {});
          } catch (x) { /* 무시 */ }
        },
        () => { e.status = 'ERR'; }
      );
    }
    return p;
  };

  const oo = XMLHttpRequest.prototype.open;
  const os = XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.open = function (m, u) {
    this.__pvM = m;
    this.__pvU = u;
    return oo.apply(this, arguments);
  };
  XMLHttpRequest.prototype.send = function (b) {
    let e = null;
    try {
      e = put(this.__pvU, this.__pvM, typeof b === 'string' ? b : null);
      this.addEventListener('loadend', function () {
        e.status = this.status;
        try { e.res = String(this.responseText || '').slice(0, CAP); } catch (x) { /* 무시 */ }
      });
    } catch (x) { /* 무시 */ }
    return os.apply(this, arguments);
  };

  const rel = (u) => {
    const i = u.indexOf('/projectview');
    return i < 0 ? u : u.slice(i);
  };
  const cut = (s, n) => (!s ? '' : s.length > n ? s.slice(0, n) + '…' : s);

  const table = (list) =>
    console.table(list.map((r) => ({
      no: r.no,
      method: r.method,
      status: r.status,
      url: cut(rel(r.url), 100),
      요청본문: cut(r.body, 50),
      응답: cut(r.res, 60),
    })));

  const copyOut = async (list, label) => {
    const out = list.map((r) => ({
      no: r.no,
      method: r.method,
      status: r.status,
      url: rel(r.url),
      body: cut(r.body, CLIP),
      res: cut(r.res, CLIP),
    }));
    window.pvRecJson = JSON.stringify(out);
    try {
      await navigator.clipboard.writeText(window.pvRecJson);
      console.log('%c' + label + ' ' + out.length + '건을 클립보드에 복사했습니다 — 그대로 알려 주세요.',
                  'font-size:13px;font-weight:bold;color:#2f6fd0;background:#e8f0fc;padding:3px 8px');
    } catch (e) {
      console.log('%c콘솔에  copy(pvRecJson)  을 입력하면 복사됩니다.', 'font-weight:bold;color:#c4692a');
    }
    return out.length;
  };

  // 응답이나 요청 본문에 특정 값이 들어 있는 요청을 찾는다 (담당자 이름 등)
  window.pvFind = async (text) => {
    const t = String(text == null ? '' : text);
    if (!t) {
      console.warn("찾을 값을 넣으세요. 예: pvFind('김선아')");
      return 0;
    }
    const hit = rec.filter((r) => (r.res && r.res.indexOf(t) >= 0) || (r.body && r.body.indexOf(t) >= 0));
    if (!hit.length) {
      console.warn("'" + t + "' 이(가) 들어 있는 요청이 없습니다. (녹화된 요청 " + rec.length + '건)');
      console.log('워크플로를 다시 불러오게 한 뒤 다시 시도하세요 — 다른 일감의 상세 화면을 여는 것이 가장 확실합니다.');
      console.log("전체를 보려면  pvDump('')  를 입력하세요.");
      return 0;
    }
    console.log('%c' + hit.length + '건에서 찾았습니다', 'font-weight:bold;color:#1f9254');
    table(hit);
    for (const r of hit) {
      console.log('%c[' + r.no + '] ' + r.method + ' ' + rel(r.url), 'font-weight:bold;color:#2f6fd0');
      const i = (r.res || '').indexOf(t);
      if (i >= 0) console.log('   응답에서 발견 — 앞뒤: ' + r.res.slice(Math.max(0, i - 400), i + 400));
    }
    console.log('전체 응답을 보려면  pvShow(번호)  를 입력하세요.');
    return copyOut(hit, '찾은 요청');
  };

  // 일감·워크플로 관련 요청만 추려 보여 준다
  window.pvDump = async (word) => {
    const w = word === undefined ? null : String(word);
    const pick = (r) => {
      const u = r.url.toLowerCase();
      return u.indexOf('task') >= 0 || u.indexOf('workflow') >= 0 || u.indexOf('node') >= 0;
    };
    const out = rec.filter((r) => (w === null ? pick(r) : !w || r.url.indexOf(w) >= 0));
    if (!out.length) {
      console.warn('잡힌 요청이 없습니다. (녹화된 요청 ' + rec.length + '건)');
      console.log("전체를 보려면  pvDump('')  를 입력하세요.");
      return 0;
    }
    table(out);
    return copyOut(out, '녹화된 요청');
  };

  // 한 건의 응답 전체를 펼쳐 본다
  window.pvShow = (no) => {
    const r = rec.filter((x) => x.no === Number(no))[0];
    if (!r) { console.warn(no + '번 요청이 없습니다.'); return null; }
    console.log(r.method + ' ' + rel(r.url) + '  → ' + r.status);
    if (r.body) console.log('요청 본문:', r.body);
    console.log('응답:', r.res);
    try { return JSON.parse(r.res); } catch (e) { return r.res; }
  };

  console.log('%c녹화를 시작했습니다 (읽기 전용 — 요청을 들여다보기만 합니다).',
              'font-size:13px;font-weight:bold;color:#1f9254');
  console.log('  1) 워크플로를 다시 불러오게 하세요 — 다른 일감의 상세 화면을 여는 것이 가장 확실합니다');
  console.log('     (F5 로 페이지를 새로고침하면 녹화가 풀립니다)');
  console.log("  2) 그 다음 콘솔에  pvFind('김선아')  를 입력하세요");
})();
`

/** 워크플로 조회 요청을 잡아내는 녹화 스크립트 */
export function buildPvRecordScript(stamp) {
  return TEMPLATE_RECORD({ stamp })
}
