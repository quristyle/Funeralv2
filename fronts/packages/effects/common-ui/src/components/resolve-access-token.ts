import SecureLS from 'secure-ls';

/**
 * localStorage 에서 현재 세션의 액세스 토큰을 찾는다.
 *
 * 파일 업로드·이미지 그룹 컴포넌트가 쓴다. 이 컴포넌트들은 XHR/fetch 로 직접
 * 게이트웨이를 부르는데, 프레임워크 층이라 앱의 요청 클라이언트(#/api)를 가져올 수
 * 없어 저장소에서 토큰을 직접 꺼낸다.
 *
 * 왜 "아무 `-core-access` 키나 첫 번째" 로는 안 되는가 — 실제로 겪었다.
 * 이 앱은 이름이 바뀐 적이 있다(funeralv2 → jsini-portal). 오래 쓴 브라우저에는
 * 옛 네임스페이스의 키(`funeralv2-web-…-core-access`)가 만료된 토큰을 담은 채 남아
 * 있고, 순회에서 그것이 먼저 잡히면 만료 토큰이 실려 나가 401 이 된다.
 * 앱 본체는 pinia 가 현재 네임스페이스 키만 읽어서 멀쩡하니, 사진 목록만 조용히
 * 깨지는 어긋남이 생긴다(/profile 프로필 사진 관리에서 발견).
 *
 * 그래서 후보를 전부 모아 JWT 의 exp 를 읽고, **만료되지 않은 것 중 가장 늦게
 * 만료되는 토큰**을 고른다. exp 를 읽지 못하는 값은 살아 있는 후보가 하나도 없을
 * 때의 최후 수단으로만 쓴다(예전 동작과의 호환).
 *
 * 값 해석은 빌드 모드로 가르지 않고 평문 JSON → SecureLS 순서로 둘 다 시도한다.
 * 저장 쪽(packages/stores/setup.ts)은 DEV 는 평문, 그 외에는 SecureLS 인데,
 * 같은 오리진에 두 형식이 섞여 남아 있어도 읽을 수 있어야 한다.
 */
export function resolveAccessToken(): string {
  const candidates: string[] = [];

  try {
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (!key || !(key === 'core-access' || key.endsWith('-core-access'))) {
        continue;
      }
      const rawValue = localStorage.getItem(key);
      if (!rawValue) continue;

      let token = '';
      try {
        token = extractToken(JSON.parse(rawValue));
      } catch {
        // 평문이 아니다 — SecureLS(암호화 저장)로 시도한다.
        try {
          const ls = new SecureLS({
            encodingType: 'aes',
            encryptionSecret: import.meta.env.VITE_APP_STORE_SECURE_KEY,
            isCompression: true,
          });
          token = extractToken(ls.get(key));
        } catch {
          // 복호화도 안 되는 값(다른 앱의 잔재 등)은 후보가 아니다.
        }
      }
      if (token) candidates.push(token);
    }
  } catch (error) {
    console.error('액세스 토큰 로드 중 오류 발생:', error);
  }

  if (candidates.length === 0) return '';

  const nowSec = Date.now() / 1000;
  let best = '';
  let bestExp = -1;
  for (const token of candidates) {
    const exp = jwtExpirySeconds(token);
    if (exp !== null && exp > nowSec && exp > bestExp) {
      best = token;
      bestExp = exp;
    }
  }

  return best || candidates[0] || '';
}

/**
 * 저장값에서 accessToken 을 꺼낸다.
 *
 * **문자열이 오면 한 번 더 파싱한다.** pinia-plugin-persistedstate 는 상태를
 * "직렬화된 JSON 문자열"로 넘기는데, secure-ls 는 set 때 그것을 다시 감쌌다가
 * get 때 한 겹만 풀어 **원래의 문자열을 그대로** 돌려준다. 거기에 `.accessToken` 을
 * 붙이면 undefined 다 — 운영(암호화 저장) 포털의 사진 목록이 항상 401 이던 원인이다.
 * 개발은 평문 JSON 경로라 걸리지 않아 오래 숨어 있었다(node 재현으로 확인함).
 */
function extractToken(value: unknown): string {
  let obj = value;
  if (typeof obj === 'string') {
    try {
      obj = JSON.parse(obj);
    } catch {
      return '';
    }
  }
  const token = (obj as { accessToken?: unknown } | null)?.accessToken;
  return typeof token === 'string' ? token : '';
}

/** JWT 페이로드의 exp(유닉스 초). JWT 형식이 아니거나 exp 가 없으면 null. */
function jwtExpirySeconds(token: string): null | number {
  const payload = token.split('.')[1];
  if (!payload) return null;
  try {
    const decoded = JSON.parse(
      atob(payload.replaceAll('-', '+').replaceAll('_', '/')),
    );
    return typeof decoded.exp === 'number' ? decoded.exp : null;
  } catch {
    return null;
  }
}
