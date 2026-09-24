/**
 * 예보 추이 차트 도우미.
 *
 * [왜 이것 하나 때문에 JS 가 필요한가]
 *
 * Blazor Server 는 브라우저 상태를 모른다. 화면 **너비**는 DevExpress 의
 * `DxLayoutBreakpoint` 가 알려 주지만(셸의 MainLayout 이 그렇게 쓴다),
 * 여기서 알아야 하는 것은 너비가 아니라 **손가락으로 보는 화면인가**이다.
 * 창을 좁힌 PC 는 여전히 마우스가 있어 말풍선이 쓸모 있고, 넓은 태블릿은
 * 마우스가 없어 말풍선이 쓸기를 방해한다 — 너비로 가르면 둘 다 틀린다.
 *
 * 기준은 `(hover: none)` 하나로 맞춘다. 차트 옆의 「옆으로 쓸면 항목이
 * 바뀝니다」 안내가 css 에서 쓰는 것과 **같은 질의**다 — 안내가 보이는 화면과
 * 쓸기가 사는 화면이 어긋나면 안 된다.
 */

/** 손가락으로 보는 화면인가. 알 수 없으면 아니라고 답한다(말풍선을 남긴다). */
export function isTouchOnly() {
  try {
    return window.matchMedia('(hover: none)').matches;
  } catch (e) {
    return false;
  }
}
