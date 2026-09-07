#!/usr/bin/env bash
#
# 성능 감사 문서(.md)를 읽기용 한 장(.html)으로 뽑는다.
#
# [왜 스크립트인가]
#
# 한동안 두 파일을 손으로 맞췄다. 그러면 반드시 갈라지고, 갈라진 뒤에는 어느
# 쪽이 맞는지 알 방법이 없다 — 특히 이 문서는 항목을 하나씩 고쳐 나가면서
# 계속 갱신되는 종류다. **정본은 .md 하나**이고 .html 은 여기서 뽑는다.
#
# [파일 하나로 열려야 한다]
#
# 저장소에서 꺼내 더블클릭하면 그대로 열리고, 첨부로 보내도 그대로 보여야 한다.
# 그래서 CSS 를 인라인으로 담고 바깥 자원을 하나도 부르지 않는다.
#
# 쓰는 법:  web/docs/build-audit-html.sh
set -euo pipefail

cd "$(dirname "$0")"

SRC=frontend-performance-audit.md
OUT=frontend-performance-audit.html

command -v pandoc >/dev/null || { echo "pandoc 이 필요하다: apt install pandoc" >&2; exit 1; }

# ── 스타일 ───────────────────────────────────────────────────────
#
# 밝은/어두운 두 벌을 함께 담는다. 브라우저 설정(prefers-color-scheme)을
# 따르고, 색은 전부 :root 의 변수에서 나온다 — 미디어 질의 안에서 색을 처음
# 정의하면 설정을 안 건드린 사람에게 한쪽 테마의 글자가 다른 쪽 배경에 얹힌다.
#
# 글꼴은 바깥에서 받지 않는다. 본문은 한글이라 기기에 있는 것을 쓰고, 제목만
# 명조 계열을 먼저 찾아 본문 고딕과 갈라 보이게 한다. 숫자가 줄줄이 나오는
# 표가 많아 고정폭 숫자(tabular-nums)를 쓴다.
read -r -d '' STYLE <<'CSS' || true
<style>
  :root {
    --paper:#F5F6F9; --surface:#FFFFFF; --code-bg:#EEF0F5;
    --ink:#171B24; --ink-2:#4C5566; --ink-3:#79839A;
    --rule:#DCE0E9; --rule-2:#C3CAD8; --accent:#1B6B66;
  }
  @media (prefers-color-scheme: dark) {
    :root:not([data-theme="light"]) {
      --paper:#12151A; --surface:#191D24; --code-bg:#1E232B;
      --ink:#E3E7EE; --ink-2:#A3ACBC; --ink-3:#7A8496;
      --rule:#2A303B; --rule-2:#3A424F; --accent:#4FAAA3;
    }
  }
  :root[data-theme="dark"] {
    --paper:#12151A; --surface:#191D24; --code-bg:#1E232B;
    --ink:#E3E7EE; --ink-2:#A3ACBC; --ink-3:#7A8496;
    --rule:#2A303B; --rule-2:#3A424F; --accent:#4FAAA3;
  }

  * { box-sizing: border-box; }

  /* **html 에도 배경을 칠한다.** body 는 폭이 좁아 가운데 서므로, 여기를
     비워 두면 어두운 테마에서 양옆 여백만 하얗게 남는다. */
  html { background: var(--paper); }

  body {
    margin: 0 auto; max-width: 58rem; padding: 3.5rem 1.5rem 6rem;
    background: var(--paper); color: var(--ink);
    font-family: -apple-system, BlinkMacSystemFont, "Apple SD Gothic Neo",
                 "Malgun Gothic", "Noto Sans KR", "Segoe UI", ui-sans-serif, sans-serif;
    font-size: 16px; line-height: 1.75;
    -webkit-font-smoothing: antialiased;
  }

  h1, h2, h3, h4 {
    font-family: "Noto Serif KR", "Apple Myungjo", Batang, "Nanum Myeongjo", Georgia, serif;
    letter-spacing: -.015em; text-wrap: balance; line-height: 1.3;
  }
  h1 { font-size: clamp(1.9rem, 5vw, 2.9rem); margin: 0 0 2rem;
       padding-bottom: 1.2rem; border-bottom: 2px solid var(--ink); }
  h2 { font-size: 1.5rem; margin: 3.5rem 0 1rem;
       padding-bottom: .5rem; border-bottom: 1px solid var(--ink); }
  h3 { font-size: 1.1rem; margin: 2.5rem 0 .75rem; }
  h4 { font-size: .98rem; margin: 1.75rem 0 .5rem; color: var(--ink-2); }

  p, li { max-width: 68ch; }
  p { margin: .9rem 0; }
  ul, ol { padding-left: 1.4rem; }
  li { margin: .35rem 0; }
  strong { font-weight: 650; }
  a { color: var(--accent); }
  hr { border: none; border-top: 1px solid var(--rule); margin: 3rem 0; }

  code {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace;
    font-size: .86em; background: var(--code-bg); padding: .1em .34em; border-radius: 2px;
  }
  pre {
    background: var(--code-bg); border: 1px solid var(--rule);
    padding: .9rem 1.1rem; overflow-x: auto; margin: 1.1rem 0;
    font-size: .82rem; line-height: 1.6;
  }
  pre code { background: none; padding: 0; font-size: 1em; }

  blockquote {
    margin: 1.2rem 0; padding: .8rem 1.2rem;
    background: var(--surface); border-left: 3px solid var(--accent);
    color: var(--ink-2);
  }
  blockquote > :first-child { margin-top: 0; }
  blockquote > :last-child { margin-bottom: 0; }

  /* 넓은 표는 쪽이 아니라 자기 칸 안에서 구른다. */
  table { border-collapse: collapse; width: 100%; font-size: .88rem;
          margin: 1.2rem 0; display: block; overflow-x: auto;
          font-variant-numeric: tabular-nums; }
  th, td { text-align: left; padding: .5rem .8rem;
           border-bottom: 1px solid var(--rule); vertical-align: top; }
  th {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    font-size: .68rem; letter-spacing: .1em; text-transform: uppercase;
    color: var(--ink-3); font-weight: 500;
    border-bottom: 1px solid var(--rule-2); white-space: nowrap;
  }

  :focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
  @media (prefers-reduced-motion: reduce) { * { animation: none !important; transition: none !important; } }
</style>
CSS

NOTE=$(cat <<'HTML'
<!--
  프론트 체감 속도 감사 — 읽기용 한 장.

  **이 파일을 손으로 고치지 않는다.** 정본은 frontend-performance-audit.md 이고
  이 파일은 build-audit-html.sh 가 뽑는다. 두 곳을 손으로 맞추면 반드시
  갈라지고, 갈라진 뒤에는 어느 쪽이 맞는지 알 방법이 없다.

  파일 하나로 열린다 — CSS 인라인, 바깥 자원 없음. 밝은/어두운 두 벌을 함께 담는다.
-->
HTML
)

pandoc "$SRC" \
  --from=gfm \
  --to=html5 \
  --standalone \
  --metadata title="포털 체감 속도 감사" \
  --variable lang=ko \
  `# pandoc 기본 스타일을 끈다. 그쪽이 html 에 밝은 배경(#fdfdfd)을 박아 두어서,` \
  `# 어두운 테마에서 body 바깥 여백만 하얗게 남는다. 값을 비워야 꺼진다 —` \
  `# =false 로 적으면 템플릿이 "false" 라는 글자를 참으로 읽어 그대로 켜진다.` \
  -V document-css= \
  --include-in-header=<(printf '%s\n%s\n' "$NOTE" "$STYLE") \
  --output="$OUT"

printf '%s → %s (%s bytes)\n' "$SRC" "$OUT" "$(wc -c < "$OUT")"
