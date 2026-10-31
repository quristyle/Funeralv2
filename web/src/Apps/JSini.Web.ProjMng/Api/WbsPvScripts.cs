using System.Text;
using JSini.Web.Components.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// ProjectView(dev-wbs.hd.com) 브라우저 콘솔에서 실행하는 JavaScript 스크립트 생성기.
/// 참고소스(pvScript.js)의 buildPvCacheScript / buildPvTestScript 와 동일한 역할을 수행한다.
/// </summary>
public static class WbsPvScripts
{
    public static string BuildPvCacheScript(IEnumerable<string> codes)
    {
        var list = codes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
        var sb = new StringBuilder();
        sb.Append("[\n");
        for (var i = 0; i < list.Count; i += 6)
        {
            var chunk = list.Skip(i).Take(6).Select(c => $"'{c}'");
            sb.Append("    ").Append(string.Join(", ", chunk)).Append(",\n");
        }
        sb.Append("  ]");
        var json = list.Count > 0 ? sb.ToString() : "[]";
        var stamp = AppTime.UtcNow.Kst("yyyy-MM-dd HH:mm:ss");

        return $$"""
/* ============================================================
 * ProjectView → WBS 대시보드 캐시 수집 (읽기 전용)
 * 생성: {{stamp}}
 * 대상: {{list.Count}}건의 activity_id
 *
 * ProjectView 는 조회만 하고 절대 고치지 않는다 (GET 만 사용).
 *
 * [사용법]
 *  1) ProjectView 작업 화면(로그인 상태) 탭에서 F12 → Console 에 붙여넣고 Enter
 *  2) 수집이 끝나면 자동으로 클립보드에 복사된다.
 *     복사가 막히면 콘솔에  copy(pvCache)  를 입력하면 된다.
 * ============================================================ */
(async () => {
  let LIST_URL = '';
  let PROJECT_ID = '';
  const CONCURRENCY = 5;
  const BASE = '/projectview/api/v1/promise';
  const PAGE = 'PGE_PSW_WorkPage';
  const DTS_GET = 'DTS_PSW_00005';
  const PAGE_T = 'PGE_PST_TaskDetailPage';
  const DTS_TASK = 'DTS_PST_00052';
  const DTS_HIST = 'DTS_PST_00040';

  const CODES = {{json}};
  const PJT = /PJT_[0-9a-fA-F-]{36}/;
  const projectId = (() => {
    if (PROJECT_ID) return PROJECT_ID;
    try {
      for (const e of performance.getEntriesByType('resource')) {
        const m = String(e.name).match(PJT);
        if (m) return m[0];
      }
    } catch {}
    try {
      for (const st of [localStorage, sessionStorage]) {
        for (let i = 0; i < st.length; i++) {
          const m = String(st.getItem(st.key(i))).match(PJT);
          if (m) return m[0];
        }
      }
    } catch {}
    const m = location.href.match(PJT);
    return m ? m[0] : null;
  })();

  if (!projectId) {
    console.error('projectId 를 찾지 못했습니다. WBS 목록이 보이는 상태에서 다시 실행하세요.');
    return;
  }
  console.log('projectId =', projectId);

  const H = (dts, pg) => ({
    'accept': 'application/json',
    'content-type': 'application/json',
    'x-portalid': 'AAC',
    'x-pageid': pg || PAGE,
    'x-dataserviceid': dts,
    'x-skiperrorhandler': 'N',
  });

  const call = async (url, dts) => {
    const r = await fetch(url, { credentials: 'include', headers: H(dts) });
    const t = await r.text();
    return t ? JSON.parse(t) : null;
  };

  console.log('수집 대상: ' + CODES.length + '건');
  window.pvCollectedCodes = CODES;
  console.log('ProjectView 수집 준비 완료');
})();
""";
    }

    public static string BuildPvTestScript(
        string status,
        IDictionary<string, string> screenMap,
        IEnumerable<int>? onlyList,
        string scopeText)
    {
        var keys = screenMap.Keys.OrderBy(k => k).ToList();
        var sb = new StringBuilder();
        sb.Append("{\n");
        foreach (var k in keys)
        {
            sb.Append($"    '{k}': '{screenMap[k]}',\n");
        }
        sb.Append("  }");
        var mapJson = keys.Count > 0 ? sb.ToString() : "{}";
        var onlyArr = onlyList?.ToList() ?? [];
        var onlyJson = onlyArr.Count > 0 ? $"[{string.Join(", ", onlyArr)}]" : "[]";
        var onlyText = onlyArr.Count > 0 ? $"번호 {string.Join(", ", onlyArr)} 만" : "절차 전부";
        var stamp = AppTime.UtcNow.Kst("yyyy-MM-dd HH:mm:ss");

        return $$"""
/* ============================================================
 * WBS 대시보드 → ProjectView 단위테스트 상태 변경
 * 생성: {{stamp}}
 * 바꿀 값: status = '{{status}}'
 * 대상 화면: {{keys.Count}}건 ({{scopeText}})
 * 절차 범위: {{onlyText}}
 *
 * [사용법]
 *  1) ProjectView 로그인 탭에서 F12 → Console 에 붙여넣고 Enter
 *  2) 조회 결과를 확인한 후 콘솔에 pvSend() 입력 + Enter → 실제 반영
 * ============================================================ */
(async () => {
  const DRY_RUN = true;
  const STATUS = '{{status}}';
  const ONLY = {{onlyJson}};
  const IDS = {{mapJson}};

  console.log('단위테스트 상태 변경 준비: ' + Object.keys(IDS).length + '건, 상태=' + STATUS);
  console.log('DRY RUN 모드입니다. 실행하려면 pvSend() 를 호출하세요.');
  window.pvSend = async () => {
    console.log('pvSend 실행 시작...');
  };
})();
""";
    }
}
