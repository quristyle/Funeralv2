/**
 * 사람 목록에 얼굴을 보여 주기 위한 규칙 두 가지.
 *
 * [왜 한곳에 두나]
 * 사진 주소를 목록용으로 바꾸는 규칙이 레이아웃 헤더에만 있었다(`layouts/basic.vue`).
 * 화면마다 다시 쓰면 한쪽만 원본을 받아 무거워지거나, 첫 글자 규칙이 달라져
 * **같은 사람이 화면마다 다른 색·다른 글자**로 보인다.
 */

/**
 * 목록에 쓸 프로필 사진 주소. 없으면 `undefined`.
 *
 * 저장된 값은 원본 내려받기 주소(`/api/file/download/...`)다. 목록에는
 * **썸네일**(`/api/file/thumbnail/...`)로 바꿔 쓴다 — 20px 자리에 원본을 받으면
 * 한 화면에 수십 장을 원본으로 내려받게 된다. 헤더가 이미 쓰는 규칙과 같다.
 *
 * `<img src>` 로 나가는 요청이라 **로그인이 심어 둔 `jsini_file_at` 쿠키**가 있어야
 * 통한다(27번 문서 5절). 쿠키가 없으면 이미지가 실패하고, 그때는 첫 글자가 대신 보인다.
 */
export function avatarThumbUrl(raw?: null | string): string | undefined {
  const url = (raw ?? '').trim();
  if (!url) return undefined;

  return url.includes('/api/file/download/')
    ? url.replace('/api/file/download/', '/api/file/thumbnail/')
    : url;
}

/**
 * 사진이 없을 때 대신 보여 줄 **첫 글자**.
 *
 * 사진이 없는 것은 흔한 일이지(43명 중 1명만 사진이 있다) 오류가 아니다.
 * 그래서 빈 동그라미나 물음표가 아니라 **이름 첫 글자**를 넣는다 —
 * 목록에서 사람을 구분하는 데 실제로 도움이 되는 유일한 값이다.
 */
export function avatarInitial(name?: null | string): string {
  const trimmed = (name ?? '').trim();
  // 한글·영문 모두 한 글자면 충분하다. 이모지·서로게이트 쌍이 잘리지 않게 배열로 자른다.
  return trimmed.length > 0 ? [...trimmed][0]! : '?';
}

/**
 * 이름에서 뽑은 배경색. **같은 사람은 어느 화면에서나 같은 색**이 된다.
 *
 * 무작위로 고르면 목록을 다시 그릴 때마다 색이 바뀌어 눈에 익지 않는다.
 * 이름 글자를 더한 값으로 정해 두면 항상 같은 색이 나온다.
 *
 * 색은 배경이 어두울 때도 흰 글자가 읽히도록 진한 것만 골랐다.
 */
/**
 * 아바타 한 개의 크기·색을 정한 인라인 스타일.
 *
 * [왜 클래스가 아니라 인라인인가]
 * antd 의 `.ant-avatar` 는 32px 을 **자기 클래스로 박아** 둔다. Tailwind 의
 * `size-5` 를 걸어도 밀리지 않아서 목록 줄이 통째로 두꺼워진다.
 * 인라인 스타일은 그 싸움에서 이긴다.
 *
 * [왜 px 이 아니라 rem 인가]
 * antd 의 `size` 속성은 숫자를 px 로 박는다. 그러면 **사용자가 정한 글꼴 크기를
 * 따라가지 못해** 글자만 커지고 얼굴은 그대로인 화면이 된다.
 * `rem` 은 항상 루트를 기준으로 계산되므로 글꼴 설정을 그대로 따라간다.
 *
 * @param hasPhoto 사진이 있으면 배경색을 주지 않는다(사진 뒤에 색이 비친다).
 * @param sizeRem  기본 1.375rem — 목록 한 줄을 늘리지 않는 크기다.
 */
export function avatarStyle(
  name?: null | string,
  hasPhoto = false,
  sizeRem = 1.375,
): Record<string, string | undefined> {
  return {
    width: `${sizeRem}rem`,
    height: `${sizeRem}rem`,
    lineHeight: `${sizeRem}rem`,
    // 첫 글자가 동그라미를 넘치지 않는 비율.
    fontSize: `${sizeRem * 0.5}rem`,
    backgroundColor: hasPhoto ? undefined : avatarColor(name),
  };
}

export function avatarColor(name?: null | string): string {
  const palette = [
    '#2563eb', // blue-600
    '#7c3aed', // violet-600
    '#059669', // emerald-600
    '#d97706', // amber-600
    '#dc2626', // red-600
    '#0891b2', // cyan-600
    '#db2777', // pink-600
    '#4f46e5', // indigo-600
  ];

  const key = (name ?? '').trim();
  if (!key) return '#6b7280'; // gray-500 — 이름조차 없는 경우

  let sum = 0;
  for (const ch of key) sum += ch.codePointAt(0) ?? 0;

  return palette[sum % palette.length]!;
}
