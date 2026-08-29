# 글꼴 파일

내부망(인터넷이 닿지 않는 망)에서도 화면이 같은 글꼴로 보이도록 파일을 저장소에 넣어 둔다.
선언은 [`src/styles/fonts.css`](../../src/styles/fonts.css), 우선순위는
[`src/styles/index.css`](../../src/styles/index.css) 에 있다.

## S-CoreDream (1순위)

- 받은 곳: `https://fastly.jsdelivr.net/gh/projectnoonnu/noonfonts_six@1.2/S-CoreDream-{무게}.woff`
- 만든 곳: 에스코어(S-Core, 삼성SDS 계열)
- 이용 조건: 개인·기업 상관없이 무료로 쓸 수 있고 재배포도 허용된다.
  **글꼴 자체를 팔거나 유료 소프트웨어에 넣어 파는 것은 안 된다.**
  (자세한 조건은 배포처 공지를 따른다)
- 넣어 둔 무게 4가지

  | 파일 | CSS `font-weight` |
  |---|---|
  | `S-CoreDream-3Light.woff` | 300 |
  | `S-CoreDream-4Regular.woff` | 400 |
  | `S-CoreDream-5Medium.woff` | 500 |
  | `S-CoreDream-6Bold.woff` | 700 |

  원본은 `1Thin` ~ `9Black` 아홉 가지다. 하나에 350KB 라 전부 넣으면 3MB 가 되어
  화면에서 실제로 쓰는 넷만 넣었다. 더 필요하면 같은 주소에서 받아
  `fonts.css` 에 `@font-face` 를 한 벌 늘리면 된다.

## Play (2순위)

- 받은 곳: Google Fonts (`https://fonts.googleapis.com/css2?family=Play:wght@400;700`)
- 이용 조건: SIL Open Font License 1.1 — 자유롭게 쓰고 함께 배포할 수 있다.
- 넣어 둔 것: 400·700 두 무게 × `latin` · `latin-ext` 두 조각

  라틴 문자 전용 글꼴이라 키릴·그리스·베트남어 조각은 받지 않았다.
  `unicode-range` 로 조각을 나눠 두어 브라우저가 필요한 것만 받는다.

## 나눔스퀘어라운드 (고를 수 있는 한글 글꼴)

- 받은 곳: `https://hangeul.pstatic.net/hangeul_static/webfont/NanumSquareRound/NanumSquareRound{무게}.woff2`
  (네이버 '한글한글 아름답게' 공식 웹폰트 배포처)
- 만든 곳: 네이버
- 이용 조건: 네이버 나눔글꼴 라이선스 — 개인·기업 상관없이 무료로 쓸 수 있고
  글꼴 파일을 함께 배포하는 것도 허용된다.
  **글꼴 자체를 팔거나 유료 소프트웨어에 넣어 파는 것은 안 된다.**
  (자세한 조건은 배포처 공지를 따른다)
- 넣어 둔 무게 3가지

  | 파일 | CSS `font-weight` | 크기 |
  |---|---|---|
  | `NanumSquareRoundL.woff2` | 300 | 200KB |
  | `NanumSquareRoundR.woff2` | 400 | 241KB |
  | `NanumSquareRoundB.woff2` | 700 | 227KB |

  **woff2 만 넣었다.** 배포처에는 eot·woff·ttf 도 있지만 이 포털이 도는 브라우저는
  전부 woff2 를 읽고, woff 를 함께 넣으면 용량이 두 배가 된다.
  ExtraBold 도 있지만 화면에서 쓸 자리가 없어 넣지 않았다.

  `@font-face` 이름은 셋 다 `NanumSquareRound` 하나로 뒀다. 배포처 CSS 는
  무게마다 다른 이름을 쓰는데, 그러면 `font-weight` 로 굵기가 바뀌지 않는다.

## Teko 는 넣지 않았다

예전 CSS 주소에 `Teko` 가 함께 있었지만 이 저장소에서 쓰는 곳이 없다.
쓰게 되면 같은 방식으로 받아서 여기에 넣고 `fonts.css` 에 선언하면 된다.

## 다시 받는 방법

```bash
# S-CoreDream (원하는 무게로 바꿔서)
curl -o S-CoreDream-6Bold.woff \
  https://fastly.jsdelivr.net/gh/projectnoonnu/noonfonts_six@1.2/S-CoreDream-6Bold.woff

# 나눔스퀘어라운드 (L · R · B)
for w in L R B; do
  curl -o "NanumSquareRound$w.woff2"     "https://hangeul.pstatic.net/hangeul_static/webfont/NanumSquareRound/NanumSquareRound$w.woff2"
done

# Play — CSS 를 먼저 받아 그 안의 woff2 주소를 따라간다
curl -A "Mozilla/5.0 ... Chrome/120.0 ..." \
  "https://fonts.googleapis.com/css2?family=Play:wght@400;700&display=swap"
```
