# 포털 글꼴 파일

[화면 설정 → 글꼴] 에서 고를 수 있는 글꼴이다. 선언은 `app.css` 의 「글꼴 고르기」
구역, 목록은 `theme.js` 의 `FONTS` 에 있다.

회사 소개 사이트(`src/Site/JSini.PublicSite/wwwroot/fonts`)와 **같은 파일을 복사해 둔 것**이다.
소개 사이트는 공유 프로젝트를 하나도 참조하지 않으므로(web/CLAUDE.md) 파일을 나눠 쓰지 않는다.

**고른 사람만 받는다.** `@font-face` 는 그 글꼴을 쓰는 글자가 화면에 있을 때만 파일을
내려받으므로, 기본(시스템 글꼴)을 쓰는 사람에게는 이 파일들이 한 번도 가지 않는다.

## S-CoreDream

- 받은 곳: `https://fastly.jsdelivr.net/gh/projectnoonnu/noonfonts_six@1.2/S-CoreDream-{무게}.woff`
- 만든 곳: 에스코어(S-Core)
- 이용 조건: 개인·기업 상관없이 무료, 재배포 허용. **글꼴 자체를 팔거나 유료 소프트웨어에 넣어 파는 것은 안 된다.**
- 넣어 둔 무게: `3Light`(300) · `6Bold`(700). 하나에 350KB 라 둘만 넣었다.
  보통 글자(400)는 Light 로, 굵은 글자(500 이상)는 Bold 로 그린다(`font-weight` 범위 선언).

## Play

- 받은 곳: Google Fonts
- 이용 조건: SIL Open Font License 1.1
- 넣어 둔 것: 400 · 700, `latin` 조각
- **라틴 문자 전용이다.** 한글은 시스템 한글 글꼴(맑은 고딕 · Apple SD 고딕)로 그려진다.
