# Bootstrap 테마 파일

테마 창의 **Bootstrap** 묶음이 쓰는 스타일시트다. DevExpress 데모
(demos.devexpress.com/blazor)의 테마 창과 같은 항목을 제공하려고 넣었다.

| 파일 | 무엇 | 출처 |
|---|---|---|
| `bootstrap.min.css` | Default · Default Dark | [Bootstrap](https://getbootstrap.com) 5.3.3 (MIT) |
| `cerulean.min.css` | Cerulean | [Bootswatch](https://bootswatch.com) 5.3.3 (MIT) |
| `flatly.min.css` | Flatly | Bootswatch 5.3.3 (MIT) |
| `journal.min.css` | Journal | Bootswatch 5.3.3 (MIT) |
| `lumen.min.css` | Lumen | Bootswatch 5.3.3 (MIT) |

## 왜 파일을 커밋해 두나

**런타임에 CDN 을 부르지 않는다.** 저장소 안의 파일만 쓴다는 것이 이 저장소의
규칙이다(루트 CLAUDE.md). 바깥 주소를 걸면 그 CDN 이 죽는 날 포털이 함께
망가지고, 사내망에서 쓰는 사람은 처음부터 안 뜬다.

## Default Dark 는 왜 파일이 없나

Bootstrap 5.3 부터 다크 모드가 **본체에 들어 있다**. `<html data-bs-theme="dark">`
를 세우면 같은 파일이 어두운 색으로 그려진다. 파일을 따로 두면 두 벌을
관리하게 되므로 표시만 바꾼다 — `theme.js` 의 `BOOTSTRAP_THEMES` 와
`apply()` 의 `data-bs-theme` 처리를 보라.

## 이것만으로는 안 된다

이 파일들은 **Bootstrap 쪽**만 그린다. DevExpress 컴포넌트는 자기 스타일이
따로 필요해서, 반드시 `bootstrap-external.bs5.min.css`(DevExpress.Blazor.Themes
패키지)와 **함께** 실어야 한다. 그 파일이 "Bootstrap 변수를 읽어 DevExpress
컴포넌트를 그리는" 다리다. 하나만 실으면 그리드·달력·팝업이 맨몸으로 나온다.

**싣는 순서도 정해져 있다** — Bootstrap 본체가 먼저, `bootstrap-external` 이
나중이다. 뒤집히면 DevExpress 부품만 옛 색으로 남는다. `theme.js` 의
`priorityOf` 가 그 순서를 지킨다.

## 올릴 때

버전을 올리려면 같은 자리에서 받아 덮어쓰고 위 표의 버전을 고친다.
Bootswatch 는 Bootstrap 과 **같은 판**을 써야 한다 — 어긋나면 변수 이름이
달라져 색이 빠진다.
