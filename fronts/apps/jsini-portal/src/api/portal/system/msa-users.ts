import { getAdminList, getAuthLinks, getCustomerList } from '#/api/helpdesk';
import { dbCont } from '#/api/projmng';

/**
 * MSA 사용자 대조.
 *
 * 계정관리 화면(`/system/account`)이 포털 계정 옆에 "이 사람이 헬프데스크·프로젝트관리에
 * 어떤 사용자로 있는가" 를 보여 주기 위해 쓴다.
 *
 * **각 MSA 의 DB 를 직접 읽지 않는다.** 서비스가 가진 API 를 게이트웨이 경유로 호출해
 * 목록만 받아 온다. 저장·수정·삭제는 하지 않는다 — 순수 읽기다.
 *
 * 어느 한 서비스가 죽어 있어도 계정 화면이 통째로 멈추면 안 되므로,
 * 호출은 각각 독립적으로 실패하고 그 시스템 열만 '확인 불가' 로 남는다.
 */

/** MSA 한 곳에서 본 사용자 한 명 */
export interface MsaUser {
  /** 소속(팀·회사 등) — 있으면 표시 */
  belongTo?: string;
  email?: string;
  /** 그 시스템 안에서의 식별자 */
  id: string;
  /** 로그인 아이디. 포털 계정과 맞춰 보는 기준이 된다. */
  loginId?: string;
  name: string;
  /** 담당자/고객 같은 구분 */
  kind?: string;
}

/** 한 시스템의 조회 결과. 실패해도 화면이 멈추지 않도록 오류를 값으로 돌려준다. */
export interface MsaUserSource {
  /** 조회 실패 사유. 있으면 화면에 '확인 불가' 로 표시한다. */
  error?: string;
  users: MsaUser[];
}

/** 계정 하나에 붙는 MSA 대조 결과 */
export interface AccountMsaLinks {
  helpdesk?: MsaUser;
  /** 헬프데스크는 명시적 연결 테이블(auth_user_links)이 있다. 그걸로 이어졌는지 */
  helpdeskLinked: boolean;
  projmng?: MsaUser;
}

/** 전체 대조에 필요한 원본 데이터 */
export interface MsaUserDirectory {
  helpdesk: MsaUserSource;
  /** authUserId(포털 로그인 아이디) → 헬프데스크 사용자 키 */
  links: Map<string, { id: string; kind: string }>;
  projmng: MsaUserSource;
}

/** 응답이 배열/`result` 중 무엇으로 와도 목록을 꺼낸다. */
function toList(res: any): any[] {
  if (Array.isArray(res)) return res;
  if (Array.isArray(res?.result)) return res.result;
  if (Array.isArray(res?.data?.result)) return res.data.result;
  if (Array.isArray(res?.data)) return res.data;
  return [];
}

function reason(error: unknown) {
  return (error as Error)?.message || '조회에 실패했습니다.';
}

/**
 * 헬프데스크 사용자.
 * 담당자(admin)와 고객(customer)이 다른 테이블이라 둘을 합쳐 하나로 만든다.
 */
async function loadHelpdesk(): Promise<MsaUserSource> {
  try {
    const [admins, customers] = await Promise.all([
      getAdminList().then(toList).catch(() => []),
      getCustomerList().then(toList).catch(() => []),
    ]);

    const users: MsaUser[] = [
      ...admins
        .filter((a: any) => !a.isDeleted)
        .map((a: any) => ({
          belongTo: (a.adminTeams ?? [])
            .map((t: any) => t?.team?.name)
            .filter(Boolean)
            .join(', '),
          email: a.email,
          id: String(a.id),
          kind: '담당자',
          loginId: a.loginId,
          name: a.userName,
        })),
      ...customers
        .filter((c: any) => !c.isDeleted)
        .map((c: any) => ({
          belongTo: c.company?.name,
          email: c.email,
          id: String(c.id),
          kind: '고객',
          loginId: c.loginId,
          name: c.userName,
        })),
    ];

    return { users };
  } catch (error) {
    return { error: reason(error), users: [] };
  }
}

/**
 * 프로젝트관리 사용자.
 *
 * 이 서비스는 저장 프로시저로 동작한다. 조회 전용 프로시저(`sp_dev_user_exec`)를
 * 읽기 모드로만 부른다 — 저장·삭제 인자는 넘기지 않는다.
 */
async function loadProjMng(): Promise<MsaUserSource> {
  try {
    const res = await dbCont('sp_dev_user_exec', {
      p_last_page_yn: 'Y',
      p_req_type: 'R',
    });

    const rows = (res?.data ?? []) as any[];
    return {
      users: rows.map((r) => ({
        belongTo: r.dept_code || r.cust_code || undefined,
        email: r.email || undefined,
        // 프로젝트관리는 로그인 아이디 자체가 키다.
        id: String(r.user_id ?? ''),
        loginId: String(r.user_id ?? ''),
        name: r.user_name ?? '',
      })),
    };
  } catch (error) {
    return { error: reason(error), users: [] };
  }
}

/** 포털 계정 ↔ 헬프데스크 사용자 연결 정보 */
async function loadLinks() {
  const map = new Map<string, { id: string; kind: string }>();
  try {
    const rows = toList(await getAuthLinks());
    rows.forEach((l: any) => {
      if (l?.authUserId) {
        map.set(String(l.authUserId).toLowerCase(), {
          id: String(l.helpdeskUserId),
          kind: l.userType === 'admin' ? '담당자' : '고객',
        });
      }
    });
  } catch {
    // 연결 정보를 못 받아도 아이디·이메일 대조는 계속할 수 있다.
  }
  return map;
}

/** 세 곳을 한 번에 받아 온다. 각각 독립적으로 실패한다. */
export async function loadMsaUserDirectory(): Promise<MsaUserDirectory> {
  const [helpdesk, projmng, links] = await Promise.all([
    loadHelpdesk(),
    loadProjMng(),
    loadLinks(),
  ]);
  return { helpdesk, links, projmng };
}

/**
 * 포털 계정 하나를 MSA 사용자와 맞춰 본다.
 *
 * 맞추는 순서가 있다.
 *   1. 헬프데스크는 **연결 테이블**이 먼저다. 관리자가 직접 이어 둔 값이기 때문이다.
 *   2. 연결이 없으면 로그인 아이디로 맞춘다.
 *   3. 그래도 없으면 이메일로 맞춘다.
 *
 * 2·3 은 어디까지나 추정이다. 화면에서 '연결됨' 과 '추정' 을 구분해 보여 준다 —
 * 같은 아이디를 쓰는 다른 사람일 수 있기 때문이다.
 * (실제로 포털 `admin` 과 헬프데스크 `admin` 이 서로 다른 사람인 사례가 있었다.)
 */
export function matchAccount(
  account: { email?: null | string; loginId?: null | string },
  directory: MsaUserDirectory,
): AccountMsaLinks {
  const loginId = (account.loginId ?? '').trim().toLowerCase();
  const email = (account.email ?? '').trim().toLowerCase();

  const byLoginId = (u: MsaUser) =>
    Boolean(loginId) && (u.loginId ?? '').toLowerCase() === loginId;
  const byEmail = (u: MsaUser) =>
    Boolean(email) && (u.email ?? '').toLowerCase() === email;

  // ── 헬프데스크 ──────────────────────────────────────────
  const link = directory.links.get(loginId);
  let helpdesk: MsaUser | undefined;
  let helpdeskLinked = false;

  if (link) {
    helpdesk = directory.helpdesk.users.find(
      (u) => u.id === link.id && u.kind === link.kind,
    );
    helpdeskLinked = Boolean(helpdesk);
  }
  if (!helpdesk) {
    helpdesk =
      directory.helpdesk.users.find(byLoginId) ??
      directory.helpdesk.users.find(byEmail);
  }

  // ── 프로젝트관리 ────────────────────────────────────────
  const projmng =
    directory.projmng.users.find(byLoginId) ??
    directory.projmng.users.find(byEmail);

  return { helpdesk, helpdeskLinked, projmng };
}
