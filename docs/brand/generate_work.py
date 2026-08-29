# -*- coding: utf-8 -*-
"""구축 사례 화면의 **재현 이미지** 생성기.

`fronts/apps/jsini-site/src/assets/work/*.svg` 를 만든다. 사이트의 '구축 사례'
페이지(`views/work.vue`)가 사례마다 하나씩 가져다 쓴다.

── 왜 캡처가 아니라 그림인가 ─────────────────────────────────

고객사 시스템의 화면에는 고인 · 상주 · 담당자 이름과 설비 운전값이 들어 있다.
공개 사이트에 올릴 수 없고, 화면 위쪽의 로고 하나로 "고객사 이름 없이 분야로만"
이라는 결정(D-S11)이 그 자리에서 무너진다.

그래서 **레이아웃과 구성만 본떠 다시 그린다.** 표시된 자료는 전부 가상이다.
화면 아래에 그 사실을 적는 한 줄이 늘 붙는다(`messages.ts` 의 `work.mockupNote`).

── 규칙 ──────────────────────────────────────────────────────

· 색은 브랜드 5색뿐이다. 새 색을 만들지 않고, 중간 톤이 필요하면 불투명도로 낸다.
  실제 화면의 초록 · 노랑 · 빨강 버튼은 여기서 명도 차이로 바뀐다.
· 모서리는 직각이다 (rx 를 쓰지 않는다). 각도는 0 · 90 만 쓴다.
· 글자는 **뜻이 필요한 곳에만** 남긴다. 나머지 본문은 회색 막대다.
  진짜 캡처처럼 보이게 하려는 것이 목적이 아니라, 무엇을 하는 화면인지
  알아보게 하는 것이 목적이다.
· 글꼴을 지정하지 않는다. `<img>` 안의 SVG 는 사이트 글꼴을 못 받아 오고,
  바깥 CDN 은 준수사항 5 가 막는다. 보는 사람의 기본 산세리프로 그린다.

브랜드 자산은 `generate.py` 가 만든다. 이 파일은 사이트 콘텐츠라 따로 둔다.

**`generate.py` 에서 색을 import 하지 않는다.** 그 파일은 함수 모음이 아니라
위에서 아래로 실행되는 스크립트라, import 하는 순간 브랜드 SVG 24개를 다시 쓴다.
색 값은 아래에 다시 적되 출처를 `docs/brand/README.md` 2절 한 곳으로 둔다 —
`fronts/apps/jsini-site/src/styles/index.css` 도 같은 이유로 같은 값을 다시 적는다.

    python docs/brand/generate_work.py
"""
import os

# docs/brand/README.md 2절이 정한 값. 여기서 새로 만들지 않는다.
INK = "#0A0A0A"
GRAPHITE = "#1C1C1E"
STEEL = "#6E6E73"
MIST = "#D2D2D7"
PAPER = "#FFFFFF"

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(
    os.path.join(HERE, "..", "..", "fronts", "apps", "jsini-site", "src", "assets", "work")
)

W, H = 1200, 760

# 화면 안에서 반복되는 치수. 실제 화면의 비율을 눈대중으로 옮긴 값이다.
SIDEBAR = 168


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def rect(x, y, w, h, fill, opacity=None, stroke=None, sw=1):
    a = f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}"'
    if opacity is not None:
        a += f' fill-opacity="{opacity}"'
    if stroke:
        a += f' stroke="{stroke}" stroke-width="{sw}"'
    return a + "/>"


def text(x, y, s, fill=INK, size=13, weight=400, anchor="start"):
    return (
        f'<text x="{x}" y="{y}" fill="{fill}" font-size="{size}" '
        f'font-weight="{weight}" text-anchor="{anchor}" '
        f'font-family="sans-serif">{esc(s)}</text>'
    )


def bar(x, y, w, h=6, fill=MIST, opacity=None):
    """글자 대신 놓는 회색 막대."""
    return rect(x, y, w, h, fill, opacity)


def donut(cx, cy, r, pct, track=MIST, fill=INK, sw=13):
    """도넛 게이지. 12시에서 시계방향으로 pct 만큼 채운다."""
    import math

    c = 2 * math.pi * r
    return (
        rect(0, 0, 0, 0, PAPER)  # 자리 맞춤용 빈 요소 (문자열 결합을 단순하게)
        + f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="{track}" stroke-width="{sw}"/>'
        + f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="{fill}" stroke-width="{sw}" '
        f'stroke-dasharray="{c * pct:.1f} {c:.1f}" transform="rotate(-90 {cx} {cy})"/>'
    )


def sidebar_light(items, active=None, width=SIDEBAR):
    """왼쪽 밝은 메뉴.

    유틸리티 모니터링 쪽만 이 형태다(나머지 셋은 어둡다). 실제 화면의 차이라
    일부러 남긴다 — 넷을 다 같은 껍데기로 그리면 한 시스템을 네 번 그린 것처럼 보인다.
    """
    out = [rect(0, 0, width, H, PAPER), rect(width - 1, 0, 1, H, MIST)]
    out.append(rect(14, 56, width - 28, 26, PAPER, stroke=MIST))
    out.append(bar(24, 66, 52, 6, MIST))

    y = 108
    for i, label in enumerate(items):
        if active is not None and i == active:
            out.append(rect(0, y - 14, width, 26, INK, 0.06))
            out.append(rect(0, y - 14, 3, 26, INK))
        out.append(text(24, y + 2, label, INK if active == i else GRAPHITE, 11))
        y += 26
    return "".join(out)


def sidebar(items, active=None):
    """왼쪽 어두운 메뉴. 실제 화면 셋이 모두 이 형태다."""
    out = [rect(0, 0, SIDEBAR, H, GRAPHITE)]
    # 위쪽 계정 줄
    out.append(f'<circle cx="26" cy="30" r="10" fill="{PAPER}" fill-opacity="0.25"/>')
    out.append(bar(44, 26, 62, 7, PAPER, 0.35))
    # 검색칸
    out.append(rect(14, 56, SIDEBAR - 28, 24, PAPER, 0.08))
    out.append(bar(24, 65, 52, 6, PAPER, 0.25))

    y = 100
    for i, label in enumerate(items):
        if active is not None and i == active:
            out.append(rect(0, y - 14, SIDEBAR, 30, PAPER, 0.12))
            out.append(rect(0, y - 14, 3, 30, PAPER, 0.7))
        out.append(rect(16, y - 5, 9, 9, PAPER, 0.45))
        out.append(text(36, y + 3, label, PAPER, 12, 400))
        y += 30
    return "".join(out)


def frame(body, title):
    """SVG 껍데기. 바깥 테두리는 사이트 쪽 `border-mist` 가 그린다."""
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" '
        f'role="img" aria-label="{esc(title)}">'
        f"<title>{esc(title)}</title>"
        f"<desc>실제 화면을 본뜬 재현 이미지. 표시된 자료는 모두 가상이다.</desc>"
        f'{rect(0, 0, W, H, PAPER)}{body}</svg>'
    )


def write(name, body, title):
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, name)
    with open(p, "w", encoding="utf-8") as f:
        f.write(frame(body, title))
    print("  %-16s %6d bytes" % (name, os.path.getsize(p)))


# ══════════════════════════════════════════════════════════════
# 01 장례식장 — 빈소 현황
# ══════════════════════════════════════════════════════════════
#
# 실제 화면: 왼쪽 어두운 메뉴 · 위쪽 탭 · 회사/건물 필터 ·
#           장례식장별로 묶인 호실 카드 격자.
# 카드 하나가 호실 하나다. 쓰고 있는 호실은 머리가 검고 고인 · 입실시각 ·
# 안내 태그 · 조작 버튼이 붙는다. 빈 호실은 머리가 회색이고 퇴실시각만 남는다.
# 한눈에 "지금 몇 호실이 차 있나" 가 보이는 것이 이 화면의 전부다.

def room_card(x, y, w, h, occupied, name):
    o = []
    o.append(rect(x, y, w, h, PAPER, stroke=MIST))
    o.append(rect(x, y, w, 30, INK if occupied else MIST))
    o.append(text(x + 12, y + 20, name, PAPER if occupied else GRAPHITE, 13, 700))

    if occupied:
        o.append(text(x + 12, y + 52, "故 ○ ○ ○", INK, 12, 700))
        o.append(text(x + 12, y + 72, "입실", STEEL, 10))
        o.append(bar(x + 38, y + 66, 74, 6, MIST))
        o.append(text(x + 12, y + 90, "온도", STEEL, 10))
        o.append(bar(x + 38, y + 84, 30, 6, MIST))
        # 안내 · 리본 태그
        o.append(rect(x + 12, y + 100, 60, 18, STEEL, 0.85))
        o.append(bar(x + 20, y + 106, 44, 6, PAPER, 0.8))
        o.append(rect(x + 78, y + 100, 60, 18, STEEL, 0.85))
        o.append(bar(x + 86, y + 106, 44, 6, PAPER, 0.8))
        o.append(rect(x + 12, y + 124, 84, 18, MIST))
        o.append(bar(x + 20, y + 130, 68, 6, STEEL, 0.7))
        # 조작 버튼 — 실제로는 색이 다른 버튼들이다. 여기서는 명도로 나눈다.
        for i, (bw, tone) in enumerate([(46, INK), (46, GRAPHITE), (52, STEEL)]):
            bx = x + 12 + sum(v[0] + 6 for v in [(46, 0), (46, 0)][:i])
            o.append(rect(bx, y + 148, bw, 20, tone))
            o.append(bar(bx + 9, y + 155, bw - 18, 6, PAPER, 0.85))
    else:
        o.append(text(x + 12, y + 58, "퇴실", STEEL, 10))
        o.append(bar(x + 38, y + 52, 74, 6, MIST))
        o.append(text(x + 12, y + 76, "온도", STEEL, 10))
        o.append(bar(x + 38, y + 70, 22, 6, MIST))
        o.append(rect(x + 12, y + 124, 70, 18, MIST))
        o.append(bar(x + 20, y + 130, 54, 6, STEEL, 0.7))
        o.append(rect(x + 12, y + 148, 58, 20, MIST))
        o.append(bar(x + 21, y + 155, 40, 6, GRAPHITE, 0.7))
        o.append(rect(x + 76, y + 148, 52, 20, INK))
        o.append(bar(x + 85, y + 155, 34, 6, PAPER, 0.85))
    return "".join(o)


def funeral():
    o = [sidebar(
        ["공통", "시스템", "권한", "건물관리", "장비", "소스",
         "현황관리", "통계", "정보", "미리보기", "설정", "도움말"],
        active=6,
    )]
    x0 = SIDEBAR

    # 탭 줄
    o.append(rect(x0, 0, W - x0, 34, PAPER, stroke=MIST))
    o.append(rect(x0 + 16, 6, 74, 28, INK))
    o.append(bar(x0 + 28, 17, 50, 6, PAPER, 0.85))
    o.append(rect(x0 + 98, 6, 74, 28, PAPER, stroke=MIST))
    o.append(bar(x0 + 110, 17, 50, 6, MIST))

    # 제목 줄
    o.append(text(x0 + 20, 66, "빈소 현황", INK, 21, 700))
    o.append(text(x0 + 120, 66, "현황관리", STEEL, 11))
    o.append(text(x0 + 176, 66, "장비 13", STEEL, 11))
    o.append(f'<circle cx="{x0 + 228}" cy="61" r="5" fill="{INK}"/>')
    o.append(text(x0 + 238, 66, "0", STEEL, 11))
    o.append(f'<circle cx="{x0 + 254}" cy="61" r="5" fill="{MIST}"/>')
    o.append(text(x0 + 264, 66, "13", STEEL, 11))
    o.append(rect(W - 150, 48, 62, 24, INK))
    o.append(bar(W - 138, 57, 38, 6, PAPER, 0.85))
    o.append(rect(W - 80, 48, 56, 24, PAPER, stroke=MIST))
    o.append(bar(W - 68, 57, 32, 6, MIST))

    # 필터 줄
    o.append(rect(x0, 84, W - x0, 48, PAPER, stroke=MIST))
    o.append(text(x0 + 40, 113, "회사", STEEL, 12))
    o.append(rect(x0 + 76, 96, 180, 26, PAPER, stroke=MIST))
    o.append(bar(x0 + 88, 106, 60, 6, GRAPHITE, 0.6))
    o.append(text(x0 + 320, 113, "건물", STEEL, 12))
    o.append(rect(x0 + 356, 96, 180, 26, PAPER, stroke=MIST))
    o.append(bar(x0 + 368, 106, 40, 6, GRAPHITE, 0.6))

    # 장례식장 묶음 하나
    o.append(text(x0 + 24, 168, "○○ 장례식장", INK, 16, 700))
    o.append(text(x0 + 140, 168, "장비 9", STEEL, 11))

    cw, ch, gap = 190, 176, 12
    occ = [True, True, False, False, False, False, False, False]
    for i, is_occ in enumerate(occ):
        cx = x0 + 24 + (i % 5) * (cw + gap)
        cy = 186 + (i // 5) * (ch + gap)
        o.append(room_card(cx, cy, cw, ch, is_occ, "○ 호실"))

    # 아래에 더 있다는 신호 — 두 번째 묶음이 잘려 보인다
    o.append(text(x0 + 24, 596, "○○ 장례식장", INK, 16, 700))
    o.append(text(x0 + 140, 596, "장비 4", STEEL, 11))
    for i in range(5):
        cx = x0 + 24 + i * (cw + gap)
        o.append(rect(cx, 614, cw, H - 614, PAPER, stroke=MIST))
        o.append(rect(cx, 614, cw, 30, INK if i == 0 else MIST))
    return "".join(o)


# ══════════════════════════════════════════════════════════════
# 02 헬프데스크 — 대시보드
# ══════════════════════════════════════════════════════════════
#
# 실제 화면: 상태별 건수 목록 · 접수율/완료율 도넛 둘 ·
#           고객사별 카드 줄 · 담당자별 카드 줄.
# 요청이 어디까지 갔는지를 사람과 고객사 두 축으로 한 화면에 편 것이다.

def stat_card(x, y, w, h, pct):
    o = [rect(x, y, w, h, PAPER, stroke=MIST)]
    o.append(bar(x + 14, y + 16, 66, 7, GRAPHITE, 0.75))
    o.append(text(x + 14, y + 46, "완료율", STEEL, 10))
    o.append(text(x + w - 14, y + 46, "%.1f%%" % (pct * 100), INK, 10, 700, "end"))
    o.append(rect(x + 14, y + 54, w - 28, 8, MIST))
    o.append(rect(x + 14, y + 54, int((w - 28) * pct), 8, INK))
    for r in range(2):
        for c in range(2):
            lx = x + 14 + c * ((w - 28) // 2)
            ly = y + 84 + r * 22
            o.append(f'<circle cx="{lx + 5}" cy="{ly - 4}" r="4" fill="{MIST}"/>')
            o.append(bar(lx + 16, ly - 7, 26, 6, STEEL, 0.6))
            o.append(text(lx + (w - 28) // 2 - 10, ly, "0", GRAPHITE, 11, 700, "end"))
    return "".join(o)


def person_card(x, y, w, h, recv, done):
    o = [rect(x, y, w, h, PAPER, stroke=MIST)]
    o.append(f'<circle cx="{x + 28}" cy="{y + 28}" r="16" fill="{MIST}"/>')
    o.append(bar(x + 54, y + 18, 48, 7, GRAPHITE, 0.75))
    o.append(bar(x + 54, y + 32, 30, 6, MIST))
    for i in range(5):
        lx = x + 16 + i * ((w - 32) // 5)
        o.append(text(lx + 12, y + 66, "0", INK, 12, 700, "middle"))
        o.append(bar(lx + 2, y + 74, 20, 5, MIST))
    for i, pct in enumerate([recv, done]):
        ly = y + 96 + i * 20
        o.append(text(x + 16, ly, "접수율" if i == 0 else "완료율", STEEL, 9))
        o.append(text(x + w - 16, ly, "%.1f%%" % (pct * 100), GRAPHITE, 9, 700, "end"))
        o.append(rect(x + 16, ly + 5, w - 32, 6, MIST))
        o.append(rect(x + 16, ly + 5, int((w - 32) * pct), 6, INK))
    return "".join(o)


def helpdesk():
    o = [sidebar(
        ["시스템", "모니터링", "고객관리", "접수", "ACT", "지원", "설정"],
        active=1,
    )]
    x0 = SIDEBAR

    # 상단 경로 줄
    o.append(rect(x0, 0, W - x0, 34, PAPER, stroke=MIST))
    o.append(text(x0 + 20, 22, "대시보드", INK, 13, 700))
    o.append(bar(x0 + 96, 12, 52, 7, MIST))
    o.append(bar(x0 + 168, 12, 52, 7, MIST))

    # 상태별 건수
    labels = ["진행", "완료", "반려", "협의", "논의", "종료"]
    tones = [0.9, 0.35, 1.0, 0.6, 0.75, 0.5]
    for i, (lb, t) in enumerate(zip(labels, tones)):
        y = 78 + i * 30
        o.append(f'<circle cx="{x0 + 26}" cy="{y - 4}" r="6" fill="{INK}" fill-opacity="{t}"/>')
        o.append(text(x0 + 44, y, lb, GRAPHITE, 12))
        o.append(text(x0 + 250, y, "000", INK, 13, 700, "end"))
        o.append(text(x0 + 262, y, "(00.0%)", STEEL, 9))

    # 도넛 둘
    for i, (lb, pct) in enumerate([("접수율", 0.788), ("완료율", 0.96)]):
        cx = x0 + 390 + i * 250
        o.append(text(cx, 68, lb, GRAPHITE, 12, 400, "middle"))
        o.append(donut(cx, 152, 52, pct, MIST, INK if i == 0 else GRAPHITE))
        o.append(text(cx, 158, "%.1f%%" % (pct * 100), INK, 19, 700, "middle"))
        o.append(bar(cx - 24, 216, 48, 6, MIST))

    # 알림 한 줄
    o.append(rect(x0 + 880, 62, 60, 40, PAPER, stroke=MIST))
    o.append(bar(x0 + 888, 72, 44, 4, MIST))
    o.append(bar(x0 + 888, 82, 30, 4, MIST))
    o.append(text(x0 + 954, 88, "라이선스 만료일", GRAPHITE, 11))

    # 고객사별 카드 — 이름은 넣지 않는다. 막대만으로 충분하다.
    cw, gap = 186, 14
    for i, pct in enumerate([1.0, 1.0, 1.0, 0.992, 0.848]):
        o.append(stat_card(x0 + 24 + i * (cw + gap), 254, cw, 136, pct))
    # 캐러셀 점
    for i in range(4):
        o.append(rect(x0 + 456 + i * 24, 402, 16, 4, INK if i == 0 else MIST))

    # 담당자별 카드
    for i, (r, d) in enumerate(
        [(0.788, 0.96), (0.169, 0.966), (0.039, 0.926), (0.003, 1.0), (0.0, 0.0)]
    ):
        o.append(person_card(x0 + 24 + i * (cw + gap), 430, cw, 136, r, d))

    o.append(rect(x0 + 490, 580, 24, 5, INK))
    return "".join(o)


# ══════════════════════════════════════════════════════════════
# 03 유틸리티 공급 — 실시간 계통 감시
# ══════════════════════════════════════════════════════════════
#
# 실제 화면: 밝은 왼쪽 메뉴 · 공정 탭(전체공정 / 전기 / 증기 / 보일러별) ·
#           한 장짜리 계통도. 연료가 들어와 보일러 · 터빈 · 발전기를 지나
#           압력이 다른 증기 헤더로 갈라지고, 옆에서는 원수가 정제되어 순수가 된다.
#           선 위에 실시간 수치가 알약 모양으로 얹힌다. 20초마다 다시 읽는다.
#
# 계통도를 고른 이유 — 이 시스템에서 가장 자기다운 화면이다. 목록과 차트는
# 어느 시스템에나 있지만, 설비가 흐름으로 이어진 이 그림은 여기밖에 없다.
#
# 실제 화면에는 색이 많다(주황 연료 · 빨강 증기 · 파랑 용수 · 분홍 회수).
# 여기서는 흐름 종류를 **선의 굵기와 점선 여부**로 나눈다. 색을 쓸 수 없어서가 아니라
# 브랜드가 무채색이기 때문이다.

def node(x, y, w, h, label, fill=PAPER, tone=None, size=10):
    o = [rect(x, y, w, h, fill, tone, stroke=MIST if fill == PAPER else None)]
    o.append(text(x + w / 2, y + h / 2 + 4, label,
                  PAPER if fill != PAPER else GRAPHITE, size, 400, "middle"))
    return "".join(o)


def vessel(x, y, w, h, label="", fill=PAPER, size=9, c=7):
    """탱크 · 용기. 실제 도면의 원통을 **모서리를 45° 로 자른 사각형**으로 옮긴다.

    브랜드가 둥근 모서리를 금지하는데(각도는 0 · 45 · 90 만) 원통을 그대로 그릴 수 없다.
    45° 모따기는 규칙 안에 있으면서 '이건 용기다' 로 읽힌다.
    """
    d = (f"M {x + c} {y} L {x + w - c} {y} L {x + w} {y + c} L {x + w} {y + h - c} "
         f"L {x + w - c} {y + h} L {x + c} {y + h} L {x} {y + h - c} L {x} {y + c} Z")
    o = [f'<path d="{d}" fill="{fill}" stroke="{MIST}" stroke-width="1"/>']
    if label:
        o.append(text(x + w / 2, y + h / 2 + 3, label,
                      PAPER if fill != PAPER else GRAPHITE, size, 400, "middle"))
    return "".join(o)


def turbine(x, y, w, h):
    """터빈. 사다리꼴은 어느 계통도에서나 터빈이라 설명이 필요 없다."""
    d = f"M {x} {y} L {x + w} {y - h * 0.28} L {x + w} {y + h * 1.28} L {x} {y + h} Z"
    return f'<path d="{d}" fill="{GRAPHITE}"/>'


def tower(x, y, h=56):
    """송전 철탑.

    처음엔 삼각형 둘에 가로줄만 그었더니 **글자 'A' 로 읽혔다.** 철탑으로 보이려면
    다리가 벌어지고, 가로대가 몸통 밖으로 나가고, 꼭대기가 뾰족해야 한다.
    """
    w = h * 0.46
    cxx = x + w / 2
    o = [
        # 벌어진 두 다리
        f'<path d="M {x} {y + h} L {cxx - w * 0.09} {y + h * 0.16}" fill="none" '
        f'stroke="{GRAPHITE}" stroke-width="1.6"/>',
        f'<path d="M {x + w} {y + h} L {cxx + w * 0.09} {y + h * 0.16}" fill="none" '
        f'stroke="{GRAPHITE}" stroke-width="1.6"/>',
        # 꼭대기
        f'<path d="M {cxx - w * 0.09} {y + h * 0.16} L {cxx} {y} '
        f'L {cxx + w * 0.09} {y + h * 0.16}" fill="none" '
        f'stroke="{GRAPHITE}" stroke-width="1.6"/>',
        # 밑동
        f'<line x1="{x - 3}" y1="{y + h}" x2="{x + w + 3}" y2="{y + h}" '
        f'stroke="{GRAPHITE}" stroke-width="1.6"/>',
    ]
    # 가로대 — 몸통 밖으로 나가야 애자가 달린 팔로 보인다
    for i, ext in ((0.26, 1.5), (0.5, 1.25)):
        yy = y + h * i
        hw = w * i * 0.5 * ext + 5
        o.append(f'<line x1="{cxx - hw}" y1="{yy}" x2="{cxx + hw}" y2="{yy}" '
                 f'stroke="{GRAPHITE}" stroke-width="1.6"/>')
    # X 브레이스 한 칸 — 격자 구조라는 표시
    o.append(f'<path d="M {cxx - w * 0.3} {y + h * 0.55} L {cxx + w * 0.42} {y + h} '
             f'M {cxx + w * 0.3} {y + h * 0.55} L {cxx - w * 0.42} {y + h}" '
             f'fill="none" stroke="{GRAPHITE}" stroke-width="1" stroke-opacity="0.5"/>')
    return "".join(o)


def flow(pts, w=2, dash=None):
    """직각으로만 꺾이는 흐름선. 각도 0 · 90 만 쓴다(브랜드 규칙)."""
    d = "M " + " L ".join(f"{x} {y}" for x, y in pts)
    a = f'<path d="{d}" fill="none" stroke="{STEEL}" stroke-width="{w}"'
    if dash:
        a += f' stroke-dasharray="{dash}"'
    return a + "/>"


def pill(x, y, value, unit):
    """선 위에 얹히는 실시간 수치."""
    w = 86
    o = [rect(x, y, w, 20, INK)]
    o.append(text(x + w - 26, y + 14, value, PAPER, 10, 700, "end"))
    o.append(text(x + w - 21, y + 14, unit, PAPER, 8))
    return "".join(o)


def utility():
    o = [sidebar_light(
        ["용수 사용량 조회", "종합 모니터링", "전기 사용량 조회", "증기 공급현황",
         "일자별 공급현황", "검침 Peak 확인", "수용가별 공급량", "매출액 현황",
         "사용량 Trend", "검침표 조회", "시스템 관리", "공통관리"],
        active=1, width=200,
    )]
    x0 = 200

    # 상표 줄
    o.append(rect(0, 0, W, 44, PAPER))
    o.append(rect(0, 43, W, 1, MIST))
    o.append(rect(20, 12, 20, 20, INK))
    o.append(text(52, 22, "○ ○ ○ ○", INK, 15, 700))
    o.append(text(52, 34, "UTILITY MONITORING SYSTEM", STEEL, 7))

    # 공정 탭
    tabs = ["전체공정", "전기", "증기", "#6BLR", "#7BLR", "#8BLR", "#9BLR", "#CCPP"]
    tx = x0 + 20
    for i, tb in enumerate(tabs):
        tw = 26 + len(tb) * 8
        if i == 0:
            o.append(rect(tx, 62, tw, 26, INK))
        o.append(text(tx + tw / 2, 79, tb, PAPER if i == 0 else STEEL, 11,
                      700 if i == 0 else 400, "middle"))
        tx += tw + 8
    o.append(text(W - 24, 79, "자동 리로드: 20 초", STEEL, 10, 400, "end"))
    o.append(rect(x0, 100, W - x0 - 24, 1, MIST))

    # ── 계통도 ────────────────────────────────────────────────
    # 실제 홈 화면은 이 그림 하나가 전부다. 표도 목록도 없다.
    # 왼쪽에서 연료가 들어와 보일러 · 터빈 · 발전기를 지나고, 증기는 압력별 헤더로
    # 갈라지며, 아래에서는 원수가 정제되어 순수가 된다. 선 위에 실시간 값이 얹힌다.
    cx0 = x0 + 40

    # 연료 세 갈래 — 저장조를 거쳐 보일러로 모인다
    o.append(text(cx0, 154, "LNG", GRAPHITE, 11, 700))
    o.append(flow([(cx0 + 40, 150), (cx0 + 96, 150), (cx0 + 96, 196), (cx0 + 140, 196)]))

    o.append(text(cx0, 200, "LPG", GRAPHITE, 11, 700))
    o.append(flow([(cx0 + 40, 196), (cx0 + 62, 196)]))
    o.append(vessel(cx0 + 62, 178, 40, 36))
    o.append(flow([(cx0 + 102, 196), (cx0 + 140, 196)]))

    o.append(text(cx0, 250, "COAL", GRAPHITE, 11, 700))
    o.append(flow([(cx0 + 44, 246), (cx0 + 62, 246)]))
    o.append(vessel(cx0 + 62, 228, 40, 36, "SILO", size=7))
    o.append(flow([(cx0 + 102, 246), (cx0 + 120, 246), (cx0 + 120, 210), (cx0 + 140, 210)]))

    # 보일러 — 몸통 + 굴뚝 둘
    o.append(rect(cx0 + 140, 140, 96, 128, INK))
    o.append(text(cx0 + 188, 210, "보일러", PAPER, 12, 700, "middle"))
    for sx2 in (cx0 + 150, cx0 + 200):
        o.append(rect(sx2, 104, 26, 40, GRAPHITE))
    o.append(rect(cx0 + 140, 268, 96, 10, GRAPHITE))

    # 터빈 · 발전기
    o.append(flow([(cx0 + 236, 196), (cx0 + 300, 196)], 3))
    o.append(turbine(cx0 + 300, 172, 66, 48))
    o.append(text(cx0 + 333, 244, "Turbine", STEEL, 9, 400, "middle"))
    o.append(f'<circle cx="{cx0 + 402}" cy="196" r="26" fill="none" '
             f'stroke="{GRAPHITE}" stroke-width="2"/>')
    o.append(text(cx0 + 402, 202, "G", GRAPHITE, 16, 700, "middle"))
    o.append(text(cx0 + 402, 244, "Generator", STEEL, 9, 400, "middle"))
    o.append(flow([(cx0 + 366, 196), (cx0 + 376, 196)], 3))
    o.append(pill(cx0 + 360, 258, "87,704.0", "kW"))

    # 복수기
    o.append(flow([(cx0 + 333, 220), (cx0 + 333, 300)], 2, "5 4"))
    o.append(vessel(cx0 + 306, 300, 54, 34, "복수기", size=8))

    # 증기 헤더 — 세로 굵은 선에서 압력별로 갈라진다.
    # 헤더를 오른쪽으로 충분히 밀어야 그림이 프레임을 채운다.
    hx = cx0 + 560
    o.append(flow([(cx0 + 236, 152), (hx + 4, 152), (hx + 4, 176)], 3))
    o.append(rect(hx, 176, 8, 250, INK))
    for i, header in enumerate(["91 ATA", "60 ATA", "45 ATA", "20 ATA", "12 ATA", "3.5 ATA"]):
        hy = 190 + i * 44
        o.append(flow([(hx + 8, hy), (hx + 74, hy)], 2))
        o.append(text(hx + 82, hy + 4, header, GRAPHITE, 11))
    o.append(pill(hx + 172, 182, "429.9", "T/H"))
    o.append(text(hx + 266, 196, "증기 공급", STEEL, 9))
    o.append(pill(hx + 172, 376, "189.2", "T/H"))
    o.append(text(hx + 266, 390, "잉여 증기", STEEL, 9))

    # 계통 연계 — 발전기에서 올라가 철탑 둘을 지나 계통으로
    o.append(flow([(cx0 + 428, 196), (cx0 + 470, 196), (cx0 + 470, 112)], 2))
    o.append(flow([(cx0 + 470, 112), (hx + 46, 112)], 2))
    o.append(tower(hx + 46, 84))
    o.append(tower(hx + 106, 84))
    o.append(pill(hx + 172, 102, "320,420.0", "kW"))
    o.append(pill(hx + 172, 132, "185,934.0", "kW"))

    # ── 용수 계통 ─────────────────────────────────────────────
    yb = 556
    o.append(f'<line x1="{x0 + 24}" y1="{yb - 34}" x2="{W - 40}" y2="{yb - 34}" '
             f'stroke="{MIST}" stroke-width="1"/>')
    o.append(text(cx0, yb - 12, "용수 계통", STEEL, 10))

    # 위 계통과 같은 폭을 쓰도록 간격을 늘려 프레임 오른쪽 끝까지 편다.
    steps = [("원수", 84), ("착수정", 84), ("여과지", 84), ("Cation", 88),
             ("Degasifier", 100), ("Anion", 84), ("순수 저장", 96)]
    gap = (W - 40 - cx0 - sum(w2 for _, w2 in steps)) / (len(steps) - 1)
    sx = cx0
    for i, (s, sw) in enumerate(steps):
        last = i == len(steps) - 1
        o.append(vessel(sx, yb, sw, 44, s, GRAPHITE if last else PAPER, 9))
        if i:
            o.append(flow([(sx - gap, yb + 22), (sx, yb + 22)], 2))
        sx += sw + gap
    o.append(pill(cx0, yb + 66, "29,402.3", "T/D"))
    o.append(text(cx0 + 96, yb + 80, "여과수 공급", STEEL, 9))
    o.append(pill(W - 40 - 190, yb + 66, "8,468.2", "T/D"))
    o.append(text(W - 40 - 94, yb + 80, "순수 공급", STEEL, 9))

    # 급수 계통이 보일러로 돌아간다 — 순환이라는 것이 보여야 그림이 닫힌다
    o.append(flow([(cx0 + 188, yb), (cx0 + 188, 278)], 2, "5 4"))
    return "".join(o)


# ══════════════════════════════════════════════════════════════
# 03-2 유틸리티 공급 — 사용량 Trend
# ══════════════════════════════════════════════════════════════
#
# 실제 화면: 위에 조회 조건(기간 · 구분 · 수용가 · 유틸리티), 그 아래 고른 계열의 표,
#           아래에 겹쳐 그린 꺾은선.
#
# 이 화면의 핵심은 **계열마다 Y축이 따로 붙는다**는 것이다. 증기(T/H) · 전기(kW) ·
# 용수(T/D) 는 자릿수가 아예 달라 한 축에 얹으면 하나만 보이고 나머지는 바닥에 눕는다.
# 그래서 왼쪽에 축이 계열 수만큼 선다. 표에는 계열별 최저 · 최대 · Peak 가 함께 있고,
# '제외관리' 로 튄 값을 빼고 다시 볼 수 있다.
#
# 실제로는 계열을 색으로 구분한다. 무채색에서는 **선 모양**으로 나눈다 —
# 실선 · 파선 · 점선. 색맹 대응과 같은 방법이라 정보가 줄지 않는다.

def sparkline(x, y, w, h, seed, amp, dash=None, width_=1.6, spikes=()):
    """의사난수 꺾은선. 난수 모듈을 쓰지 않아 돌릴 때마다 같은 그림이 나온다."""
    n = 132
    pts = []
    v = 0.5
    for i in range(n):
        # 선형 합동식 하나로 흔들림을 만든다. 재현 가능해야 파일이 안 바뀐다.
        seed = (seed * 1103515245 + 12345) % 2147483648
        v += ((seed >> 16) % 1000 / 1000.0 - 0.5) * amp
        v = min(0.92, max(0.08, v))
        vv = v
        for sx, sd in spikes:
            if abs(i - sx) <= 1:
                vv = min(0.96, v + sd)
        pts.append((x + w * i / (n - 1), y + h - h * vv))
    d = "M " + " L ".join(f"{px:.1f} {py:.1f}" for px, py in pts)
    a = (f'<path d="{d}" fill="none" stroke="{INK}" stroke-width="{width_}" '
         f'stroke-linejoin="round"')
    if dash:
        a += f' stroke-dasharray="{dash}"'
    return a + "/>"


def utility_trend():
    o = [sidebar_light(
        ["용수 사용량 조회", "종합 모니터링", "전기 사용량 조회", "증기 공급현황",
         "일자별 공급현황", "검침 Peak 확인", "수용가별 공급량", "매출액 현황",
         "사용량 Trend", "검침표 조회", "15분 전기 사용량", "공지사항"],
        active=8, width=200,
    )]
    x0 = 200

    o.append(rect(0, 0, W, 44, PAPER))
    o.append(rect(0, 43, W, 1, MIST))
    o.append(rect(20, 12, 20, 20, INK))
    o.append(text(52, 22, "○ ○ ○ ○", INK, 15, 700))
    o.append(text(52, 34, "UTILITY MONITORING SYSTEM", STEEL, 7))

    # 탭
    o.append(rect(x0 + 20, 60, 118, 28, INK))
    o.append(text(x0 + 79, 79, "사용량 Trend", PAPER, 11, 700, "middle"))
    o.append(text(x0 + 168, 79, "용수 사용량 조회", STEEL, 11))
    o.append(rect(x0, 100, W - x0 - 24, 1, MIST))

    # 조회 조건
    y = 118
    fields = [("조회 기간", 190), ("구분", 90), ("수용가", 150), ("유틸리티", 250)]
    fx = x0 + 24
    for label, fw in fields:
        o.append(text(fx, y + 18, label, STEEL, 10))
        bx = fx + len(label) * 11 + 8
        o.append(rect(bx, y, fw, 26, PAPER, stroke=MIST))
        o.append(bar(bx + 10, y + 10, fw - 34, 6, GRAPHITE, 0.55))
        o.append(rect(bx + fw - 18, y + 9, 8, 8, STEEL, 0.5))
        fx = bx + fw + 22
    for i in range(3):
        o.append(rect(W - 130 + i * 36, y, 28, 26, INK if i == 0 else PAPER,
                      stroke=None if i == 0 else MIST))

    # 계열 표
    ty = 164
    cols = ["유틸리티", "최저", "최대", "Peak", "Y축", "보기", "제외관리", "구간조회", "시간"]
    cx = [x0 + 48, x0 + 400, x0 + 470, x0 + 545, x0 + 640, x0 + 780, x0 + 860,
          x0 + 1000, x0 + 1090]
    o.append(rect(x0 + 24, ty, W - x0 - 48, 26, INK, 0.06))
    for c, lb in zip(cx, cols):
        o.append(text(c, ty + 17, lb, GRAPHITE, 10))

    dashes = [None, "7 4", "2 3"]
    lows = ["6.73", "3.63", "32.25"]
    highs = ["9.04", "4.36", "47.41"]
    peaks = ["7.879", "4.137", "42.656"]
    for r in range(3):
        ry = ty + 30 + r * 26
        o.append(rect(x0 + 24, ry + 25, W - x0 - 48, 1, MIST))
        # 계열 표시 — 색 대신 선 모양이다
        o.append(f'<line x1="{x0 + 40}" y1="{ry + 13}" x2="{x0 + 68}" y2="{ry + 13}" '
                 f'stroke="{INK}" stroke-width="2"'
                 + (f' stroke-dasharray="{dashes[r]}"' if dashes[r] else "") + "/>")
        # 수용가 · 유틸리티 이름은 실명이라 막대로 둔다
        o.append(bar(x0 + 78, ry + 9, 130, 7, GRAPHITE, 0.75))
        o.append(rect(x0 + 216, ry + 5, 34, 16, MIST))
        o.append(bar(x0 + 223, ry + 11, 20, 5, GRAPHITE, 0.7))
        for c, v in zip(cx[1:4], (lows[r], highs[r], peaks[r])):
            o.append(text(c + 40, ry + 17, v, GRAPHITE, 11, 400, "end"))
        # Y축 최소·최대 입력칸
        o.append(rect(x0 + 632, ry + 4, 10, 10, INK))
        o.append(rect(x0 + 650, ry + 3, 52, 18, PAPER, stroke=MIST))
        o.append(rect(x0 + 712, ry + 3, 52, 18, PAPER, stroke=MIST))
        o.append(rect(x0 + 786, ry + 4, 10, 10, INK))
        o.append(rect(x0 + 852, ry + 3, 62, 18, GRAPHITE))
        o.append(bar(x0 + 862, ry + 9, 42, 6, PAPER, 0.8))
        o.append(rect(x0 + 920, ry + 3, 62, 18, STEEL))
        o.append(bar(x0 + 930, ry + 9, 42, 6, PAPER, 0.8))
        o.append(rect(x0 + 1064, ry + 3, 34, 18, PAPER, stroke=MIST))
        o.append(rect(x0 + 1108, ry + 3, 116, 18, INK))
        o.append(bar(x0 + 1118, ry + 9, 96, 6, PAPER, 0.8))

    # ── 차트 ──────────────────────────────────────────────────
    cy0, chh = 262, 400
    cl, cr = x0 + 130, W - 40
    o.append(rect(x0 + 24, cy0 - 14, W - x0 - 48, chh + 78, PAPER, stroke=MIST))

    # 계열마다 Y축이 따로 선다 — 이 화면의 핵심이라 세 벌을 다 그린다
    for a in range(3):
        ax = x0 + 46 + a * 30
        o.append(f'<line x1="{ax}" y1="{cy0}" x2="{ax}" y2="{cy0 + chh}" '
                 f'stroke="{MIST}" stroke-width="1"/>')
        for t in range(6):
            ty2 = cy0 + chh - t * (chh / 5)
            o.append(f'<line x1="{ax - 3}" y1="{ty2}" x2="{ax}" y2="{ty2}" '
                     f'stroke="{STEEL}" stroke-width="1"/>')
            o.append(text(ax - 6, ty2 + 4, "00", STEEL, 8, 400, "end"))

    # 가로 눈금
    for t in range(6):
        gy = cy0 + chh - t * (chh / 5)
        o.append(f'<line x1="{cl}" y1="{gy}" x2="{cr}" y2="{gy}" '
                 f'stroke="{MIST}" stroke-width="0.6"/>')

    cw2 = cr - cl
    o.append(sparkline(cl, cy0 + 30, cw2, 120, 20260829, 0.09, None, 1.8,
                       spikes=((104, 0.16), (112, 0.2), (120, 0.14))))
    o.append(sparkline(cl, cy0 + 120, cw2, 200, 77777, 0.16, "7 4", 1.5,
                       spikes=((92, -0.0), (110, 0.22))))
    o.append(sparkline(cl, cy0 + 330, cw2, 40, 31015, 0.05, "2 3", 1.4))

    # 시간 축
    o.append(f'<line x1="{cl}" y1="{cy0 + chh}" x2="{cr}" y2="{cy0 + chh}" '
             f'stroke="{MIST}" stroke-width="1"/>')
    for t in range(11):
        tx2 = cl + cw2 * t / 10
        o.append(f'<line x1="{tx2}" y1="{cy0 + chh}" x2="{tx2}" y2="{cy0 + chh + 5}" '
                 f'stroke="{STEEL}" stroke-width="1"/>')
        o.append(bar(tx2 - 18, cy0 + chh + 14, 36, 5, MIST))
        o.append(bar(tx2 - 14, cy0 + chh + 24, 28, 5, MIST))
    return "".join(o)


# ══════════════════════════════════════════════════════════════
# 02 사내 통합 포털 — 홈 대시보드
# ══════════════════════════════════════════════════════════════
#
# 실제 화면: 왼쪽 밝은 메뉴(시스템 · 모니터링 · SHE · 날씨 · 식단 · 생일 · 알림 · 로그) ·
#           위 칸에 오늘 날씨 · 식단 · 생일자 · 안전지표 순위 ·
#           그 아래 10일 예보 줄 · 안전 지표 차트 · 업무 시스템 바로가기.
#
# 실제 화면의 날씨 카드는 주황 그라디언트다. 브랜드가 그라디언트를 금지하므로
# 여기서는 Ink 단색 블록으로 바꾼다. '가장 눈에 띄는 칸' 이라는 역할은 그대로다.
#
# 생일자 칸에는 실제로 **직원 이름 · 사진 · 생일**이 들어 있다. 남의 회사 직원의
# 개인정보라 옮기지 않는다 — 자리와 모양만 남기고 이름은 회색 막대다.

def panel(x, y, w, h, title=""):
    o = [rect(x, y, w, h, PAPER, stroke=MIST)]
    if title:
        o.append(rect(x + 14, y + 16, 3, 12, INK))
        o.append(text(x + 24, y + 26, title, INK, 12, 700))
    return "".join(o)


def hub():
    o = [sidebar_light(
        ["시스템", "모니터링", "SHE", "날씨", "식단", "생일", "설정", "알림", "로그"],
        active=3, width=196,
    )]
    x0 = 196

    o.append(rect(0, 0, W, 44, PAPER))
    o.append(rect(0, 43, W, 1, MIST))
    o.append(rect(20, 12, 20, 20, INK))
    o.append(text(50, 27, "○ . ○ ○ ○", INK, 14, 700))
    o.append(rect(W - 150, 12, 60, 20, INK, 0.08))
    o.append(bar(W - 140, 19, 40, 6, GRAPHITE, 0.5))
    o.append(f'<circle cx="{W - 40}" cy="22" r="11" fill="{MIST}"/>')

    top, th = 64, 200
    gap = 14
    cw = (W - x0 - 48 - gap * 3) / 4

    # 1) 오늘 날씨 — 실제로는 주황 그라디언트 카드. 여기서는 Ink 단색.
    wx = x0 + 24
    o.append(rect(wx, top, cw, th, INK))
    o.append(text(wx + 18, top + 30, "LOCATION", PAPER, 8, 400))
    o.append(text(wx + 18, top + 48, "○ . ○ ○ ○", PAPER, 13, 700))
    o.append(text(wx + 18, top + 96, "32.5", PAPER, 40, 700))
    o.append(text(wx + 110, top + 96, "°", PAPER, 20, 400))
    o.append(text(wx + 18, top + 118, "구름많음", PAPER, 11))
    o.append(rect(wx, top + 132, cw, 68, PAPER, 0.12))
    for i, lb in enumerate(["습도", "풍속", "강수", "형태"]):
        px = wx + 18 + i * (cw - 36) / 4
        o.append(text(px, top + 156, "00", PAPER, 11, 700))
        o.append(text(px, top + 172, lb, PAPER, 8))

    # 2) 오늘의 식단
    mx = wx + cw + gap
    o.append(panel(mx, top, cw, th, "오늘의 식단"))
    for r in range(2):
        ry = top + 54 + r * 66
        o.append(rect(mx + 14, ry, 30, 16, INK))
        o.append(bar(mx + 20, ry + 5, 18, 6, PAPER, 0.85))
        o.append(bar(mx + 52, ry + 3, 74, 8, GRAPHITE, 0.8))
        for c in range(4):
            o.append(rect(mx + 14 + c * 52, ry + 26, 46, 16, MIST))

    # 3) 오늘의 생일자 — 이름 자리는 비운다
    bx = mx + cw + gap
    o.append(panel(bx, top, cw, th, "오늘의 생일자"))
    o.append(text(bx + cw / 2, top + 112, "오늘 생일인 임직원이 없습니다.",
                  STEEL, 10, 400, "middle"))

    # 4) 안전 지표 — 내 점수 + 순위
    sx = bx + cw + gap
    o.append(panel(sx, top, cw, th, "안전 지표"))
    o.append(rect(sx + 14, top + 44, cw * 0.42, th - 62, GRAPHITE))
    o.append(text(sx + 26, top + 74, "나의 점수", PAPER, 9))
    o.append(text(sx + 26, top + 106, "0", PAPER, 26, 700))
    o.append(text(sx + 26 + cw * 0.42, top + 60, "TOP 5", GRAPHITE, 9, 700))
    for i in range(5):
        ry = top + 78 + i * 22
        o.append(rect(sx + 26 + cw * 0.42, ry - 9, 13, 13, INK if i < 3 else MIST))
        o.append(text(sx + cw - 16, ry, "0,000", GRAPHITE, 10, 400, "end"))

    # 10일 예보 줄
    fy = top + th + 18
    fw = (W - x0 - 48 - 9 * 8) / 10
    for i in range(10):
        fx = x0 + 24 + i * (fw + 8)
        o.append(rect(fx, fy, fw, 96, PAPER, stroke=MIST))
        o.append(text(fx + fw / 2, fy + 22, "00-00", GRAPHITE, 10, 700, "middle"))
        o.append(bar(fx + fw / 2 - 18, fy + 30, 36, 5, MIST))
        for h in range(2):
            hx2 = fx + fw / 2 + (h * 2 - 1) * fw * 0.19
            o.append(f'<circle cx="{hx2}" cy="{fy + 54}" r="8" fill="none" '
                     f'stroke="{STEEL}" stroke-width="1.4"/>')
        o.append(text(fx + fw / 2 - 14, fy + 84, "00°", STEEL, 10, 400, "middle"))
        o.append(text(fx + fw / 2 + 16, fy + 84, "00°", INK, 10, 700, "middle"))

    # 아래 세 칸 — 안전 통계 · 지표 차트 · 업무 시스템 바로가기
    by = fy + 114
    bh = H - by - 24
    b1 = (W - x0 - 48 - gap * 2) * 0.26
    b2 = (W - x0 - 48 - gap * 2) * 0.40
    b3 = (W - x0 - 48 - gap * 2) * 0.34

    o.append(panel(x0 + 24, by, b1, bh, "주간 현황"))
    o.append(rect(x0 + 38, by + 46, b1 * 0.42, 24, PAPER, stroke=MIST))
    o.append(rect(x0 + 44 + b1 * 0.42, by + 46, b1 * 0.42, 24, INK))
    for r in range(3):
        o.append(bar(x0 + 38, by + 90 + r * 22, b1 - 52, 8, MIST))

    cx2 = x0 + 24 + b1 + gap
    o.append(panel(cx2, by, b2, bh, "안전 지표 추이"))
    o.append(f'<line x1="{cx2 + 30}" y1="{by + bh - 34}" x2="{cx2 + b2 - 20}" '
             f'y2="{by + bh - 34}" stroke="{MIST}" stroke-width="1"/>')
    o.append(sparkline(cx2 + 30, by + 52, b2 - 50, bh - 92, 4910, 0.11, None, 1.8))

    ax = cx2 + b2 + gap
    o.append(panel(ax, by, b3, bh, "업무 시스템"))
    for r in range(3):
        for c in range(2):
            ix = ax + 20 + c * (b3 - 40) / 2
            iy = by + 52 + r * 34
            o.append(rect(ix, iy, 18, 18, INK, 0.12))
            o.append(bar(ix + 26, iy + 6, 62, 7, GRAPHITE, 0.7))
    return "".join(o)


# ══════════════════════════════════════════════════════════════
# 02-2 사내 통합 포털 — 실시간 날씨 현황
# ══════════════════════════════════════════════════════════════
#
# 실제 화면: 현재 날씨 카드 · 10일 예보 · 최근 기온 추이(오늘 표시) ·
#           기온 / 강수 / 바람 / 습도 탭으로 바꿔 보는 시계열.

def hub_weather():
    o = [sidebar_light(
        ["실시간 날씨 현황", "날씨 예보", "기상 특보", "날씨 대응",
         "날씨 관리", "날씨 기록", "날씨 이벤트 기록", "식단", "생일", "알림"],
        active=0, width=196,
    )]
    x0 = 196

    o.append(rect(0, 0, W, 44, PAPER))
    o.append(rect(0, 43, W, 1, MIST))
    o.append(rect(20, 12, 20, 20, INK))
    o.append(text(50, 27, "○ . ○ ○ ○", INK, 14, 700))
    o.append(text(x0 + 60, 27, "날씨", STEEL, 11))
    o.append(text(x0 + 110, 27, "›", STEEL, 11))
    o.append(text(x0 + 128, 27, "실시간 날씨 현황", INK, 13, 700))

    # 사업장 선택
    o.append(rect(x0 + 24, 62, 150, 28, PAPER, stroke=MIST))
    o.append(bar(x0 + 36, 72, 62, 7, GRAPHITE, 0.6))

    # 현재 날씨 카드
    top = 104
    cw = 250
    o.append(rect(x0 + 24, top, cw, 240, INK))
    o.append(text(x0 + 44, top + 28, "LOCATION", PAPER, 8))
    o.append(text(x0 + 44, top + 48, "○ . ○ ○ ○", PAPER, 13, 700))
    o.append(text(x0 + 44, top + 108, "32.5", PAPER, 46, 700))
    o.append(text(x0 + 148, top + 108, "°", PAPER, 22))
    o.append(text(x0 + 44, top + 132, "구름많음", PAPER, 11))
    o.append(rect(x0 + 24, top + 150, cw, 90, PAPER, 0.12))
    for i, lb in enumerate(["습도", "풍속", "강수", "형태", "동서", "남북", "적설", "기온"]):
        px = x0 + 44 + (i % 4) * 56
        py = top + 180 + (i // 4) * 44
        o.append(text(px, py, "00", PAPER, 11, 700))
        o.append(text(px, py + 14, lb, PAPER, 8))

    # 10일 예보
    fx0 = x0 + 24 + cw + 20
    fw = (W - 40 - fx0 - 9 * 6) / 10
    for i in range(10):
        fx = fx0 + i * (fw + 6)
        o.append(rect(fx, top, fw, 120, PAPER, stroke=MIST))
        o.append(text(fx + fw / 2, top + 24, "00-00", GRAPHITE, 10, 700, "middle"))
        o.append(bar(fx + fw / 2 - 16, top + 32, 32, 5, MIST))
        for h in range(2):
            hx2 = fx + fw / 2 + (h * 2 - 1) * fw * 0.2
            o.append(f'<circle cx="{hx2}" cy="{top + 62}" r="9" fill="none" '
                     f'stroke="{STEEL}" stroke-width="1.4"/>')
            o.append(text(hx2, top + 90, "00%", STEEL, 8, 400, "middle"))
        o.append(text(fx + fw / 2 - 14, top + 110, "00°", STEEL, 10, 400, "middle"))
        o.append(text(fx + fw / 2 + 16, top + 110, "00°", INK, 10, 700, "middle"))

    # 최근 추이 — 오늘 자리에 세로 점선
    ty = top + 140
    o.append(rect(fx0, ty, W - 40 - fx0, 104, PAPER, stroke=MIST))
    o.append(sparkline(fx0 + 40, ty + 22, W - 40 - fx0 - 70, 60, 8291, 0.07, None, 1.8))
    tx3 = W - 40 - 60
    o.append(f'<line x1="{tx3}" y1="{ty + 14}" x2="{tx3}" y2="{ty + 92}" '
             f'stroke="{INK}" stroke-width="1" stroke-dasharray="3 3"/>')
    o.append(rect(tx3 - 26, ty + 4, 52, 16, INK))
    o.append(text(tx3, ty + 16, "TODAY", PAPER, 8, 700, "middle"))

    # 아래 시계열 — 기온 / 강수 / 바람 / 습도 탭
    gy = top + 268
    o.append(rect(x0 + 24, gy, W - 64 - x0, H - gy - 24, PAPER, stroke=MIST))
    tabs = ["기온", "강수", "바람", "습도"]
    tx4 = (x0 + W - 40) / 2 - 100
    for i, tb in enumerate(tabs):
        o.append(rect(tx4 + i * 52, gy + 14, 46, 22, INK if i == 0 else PAPER,
                      stroke=None if i == 0 else MIST))
        o.append(text(tx4 + i * 52 + 23, gy + 29, tb, PAPER if i == 0 else STEEL,
                      10, 700 if i == 0 else 400, "middle"))
    for t in range(5):
        gyy = gy + 60 + t * 42
        o.append(f'<line x1="{x0 + 78}" y1="{gyy}" x2="{W - 60}" y2="{gyy}" '
                 f'stroke="{MIST}" stroke-width="0.6" stroke-dasharray="3 4"/>')
        o.append(text(x0 + 70, gyy + 4, "00.0", STEEL, 8, 400, "end"))
    o.append(sparkline(x0 + 78, gy + 56, W - 138 - x0, 172, 51443, 0.1, None, 2))
    return "".join(o)


# ══════════════════════════════════════════════════════════════
# 05 프로젝트 · 개발 관리 — USE CASE 다이어그램
# ══════════════════════════════════════════════════════════════
#
# 실제 화면: 어두운 메뉴 · 여러 탭이 한 줄에 열려 있고(프로젝트 DB · 모니터링 ·
#           Source · Schedule · 프로젝트 코드 정보 · Flows · USE CASE),
#           고른 탭 안에 **다이어그램 편집기가 통째로 들어 있다.**
#           왼쪽 도형 팔레트 · 가운데 캔버스 · 오른쪽 속성 패널.
#
# 이 시스템에서 가장 자기다운 점이 이것이다. 보통은 설계도를 따로 그려 파일로 두고
# 프로젝트 관리 도구에는 일정만 넣는데, 여기서는 **구조도가 프로젝트 안에 산다.**
# 소스 · DB 정보와 같은 탭 줄에 놓여 있어 인수인계 때 함께 넘어간다.

def projmng():
    o = [sidebar(
        ["공통", "프로젝트", "  프로젝트", "  프로젝트 DB", "  Source",
         "  WBS", "  Schedule", "  ERD", "  Flows", "  USE CASE",
         "일정관리", "개발툴", "시스템"],
        active=9,
    )]
    x0 = SIDEBAR

    o.append(rect(x0, 0, W - x0, 38, GRAPHITE))
    o.append(bar(x0 + 24, 15, 190, 8, PAPER, 0.5))
    o.append(f'<circle cx="{W - 90}" cy="19" r="10" fill="{PAPER}" fill-opacity="0.3"/>')
    o.append(bar(W - 72, 15, 40, 7, PAPER, 0.5))

    # 탭 줄 — 여러 화면을 동시에 열어 둔다
    tabs = ["프로젝트 DB", "모니터링", "Source", "Schedule", "코드 정보", "Flows", "USE CASE"]
    tx = x0 + 16
    for i, tb in enumerate(tabs):
        tw = 30 + len(tb) * 8
        act = i == len(tabs) - 1
        o.append(rect(tx, 46, tw, 28, PAPER if act else INK, None if act else 0.55,
                      stroke=MIST if act else None))
        o.append(text(tx + tw / 2 - 5, 64, tb, INK if act else PAPER, 10,
                      700 if act else 400, "middle"))
        o.append(f'<circle cx="{tx + tw - 12}" cy="60" r="4" fill="'
                 f'{STEEL if act else PAPER}" fill-opacity="0.7"/>')
        tx += tw + 6
    o.append(rect(x0, 74, W - x0, 1, MIST))

    # 프로젝트 · 도면 고르기
    o.append(rect(x0 + 24, 86, 130, 26, PAPER, stroke=MIST))
    o.append(bar(x0 + 36, 96, 78, 7, GRAPHITE, 0.7))
    o.append(rect(x0 + 164, 86, 250, 26, PAPER, stroke=MIST))
    o.append(bar(x0 + 176, 96, 54, 7, GRAPHITE, 0.7))
    o.append(rect(W - 168, 86, 62, 26, GRAPHITE))
    o.append(bar(W - 156, 96, 38, 7, PAPER, 0.85))
    o.append(rect(W - 98, 86, 62, 26, INK))
    o.append(bar(W - 86, 96, 38, 7, PAPER, 0.85))

    # ── 편집기 ────────────────────────────────────────────────
    ey = 124
    eh = H - ey - 20
    o.append(rect(x0 + 24, ey, W - 64 - x0, eh, PAPER, stroke=MIST))

    # 메뉴줄 + 도구줄
    for i, m in enumerate(["File", "Edit", "View", "Arrange", "Extras", "Help"]):
        o.append(text(x0 + 44 + i * 52, ey + 22, m, GRAPHITE, 10))
    o.append(rect(x0 + 24, ey + 32, W - 64 - x0, 1, MIST))
    for i in range(12):
        o.append(rect(x0 + 44 + i * 30, ey + 44, 18, 18, INK, 0.14))
    o.append(rect(x0 + 24, ey + 74, W - 64 - x0, 1, MIST))

    # 왼쪽 도형 팔레트
    px = x0 + 24
    pw = 176
    o.append(rect(px, ey + 75, pw, eh - 75, INK, 0.03))
    o.append(rect(px + pw, ey + 75, 1, eh - 75, MIST))
    o.append(rect(px + 14, ey + 90, pw - 28, 22, PAPER, stroke=MIST))
    o.append(bar(px + 24, ey + 98, 60, 6, MIST))
    o.append(text(px + 16, ey + 136, "General", GRAPHITE, 10, 700))
    for r in range(4):
        for c in range(5):
            o.append(rect(px + 16 + c * 31, ey + 148 + r * 31, 24, 20,
                          PAPER, stroke=MIST))
    for i, grp in enumerate(["Misc", "Advanced", "Basic", "Arrows", "UML", "BPMN"]):
        o.append(text(px + 16, ey + 300 + i * 26, grp, GRAPHITE, 10))
        o.append(rect(px + 14, ey + 310 + i * 26, pw - 28, 1, MIST))

    # 오른쪽 속성 패널
    rw = 190
    rx = W - 40 - rw
    o.append(rect(rx, ey + 75, 1, eh - 75, MIST))
    o.append(text(rx + 20, ey + 100, "Diagram", GRAPHITE, 11, 700))
    for i, opt in enumerate(["Grid", "Page View", "Background",
                             "Connection arrows", "Connection points", "Guides"]):
        oy = ey + 130 + i * 26
        checked = i >= 3 and i <= 4
        o.append(rect(rx + 20, oy - 9, 11, 11, INK if checked else PAPER,
                      stroke=None if checked else MIST))
        o.append(text(rx + 40, oy, opt, GRAPHITE, 9))
    o.append(text(rx + 20, ey + 306, "Paper Size", GRAPHITE, 9))
    o.append(rect(rx + 20, ey + 316, rw - 40, 22, PAPER, stroke=MIST))
    o.append(bar(rx + 30, ey + 324, 90, 6, MIST))
    for i in range(2):
        o.append(rect(rx + 20, ey + 352 + i * 30, rw - 40, 22, INK, 0.08))
        o.append(bar(rx + 60, ey + 360 + i * 30, 70, 6, GRAPHITE, 0.6))

    # ── 캔버스의 구조도 ───────────────────────────────────────
    # 실제 도면은 고객사 시스템 이름이 그대로 적혀 있다. 여기서는 모양만 남긴다.
    cvx = px + pw + 30
    cvy = ey + 96

    o.append(rect(cvx + 20, cvy + 60, 210, 76, PAPER, stroke=MIST))
    o.append(text(cvx + 26, cvy + 54, "server", STEEL, 9))
    o.append(f'<circle cx="{cvx + 50}" cy="{cvy + 98}" r="17" fill="{INK}" '
             f'fill-opacity="0.15"/>')
    o.append(rect(cvx + 84, cvy + 84, 34, 28, GRAPHITE))
    for i in range(2):
        o.append(vessel(cvx + 130 + i * 48, cvy + 82, 42, 32, "DB", PAPER, 8, 5))

    o.append(rect(cvx + 20, cvy + 172, 210, 76, PAPER, stroke=MIST))
    o.append(text(cvx + 26, cvy + 166, "server", STEEL, 9))
    o.append(f'<circle cx="{cvx + 50}" cy="{cvy + 210}" r="17" fill="{INK}" '
             f'fill-opacity="0.15"/>')
    o.append(rect(cvx + 84, cvy + 196, 34, 28, GRAPHITE))
    for i in range(2):
        o.append(vessel(cvx + 130 + i * 48, cvy + 194, 42, 32, "DB", PAPER, 8, 5))

    # 위쪽 단말
    o.append(f'<circle cx="{cvx + 128}" cy="{cvy - 4}" r="22" fill="none" '
             f'stroke="{GRAPHITE}" stroke-width="1.4"/>')
    o.append(flow([(cvx + 106, cvy - 4), (cvx + 62, cvy - 4), (cvx + 62, cvy + 98)], 2))
    o.append(flow([(cvx + 62, cvy + 98), (cvx + 62, cvy + 210)], 2))

    # 가운데 서버 + DB
    o.append(rect(cvx + 320, cvy + 40, 130, 100, PAPER, stroke=MIST))
    o.append(text(cvx + 326, cvy + 34, "server", STEEL, 9))
    o.append(f'<circle cx="{cvx + 385}" cy="{cvy + 66}" r="16" fill="{INK}" '
             f'fill-opacity="0.15"/>')
    o.append(vessel(cvx + 348, cvy + 90, 74, 38, "DB", PAPER, 9, 6))
    o.append(flow([(cvx + 230, cvy + 98), (cvx + 320, cvy + 98)], 3))
    o.append(flow([(cvx + 230, cvy + 210), (cvx + 290, cvy + 210),
                   (cvx + 290, cvy + 128), (cvx + 320, cvy + 128)], 2))

    # 오른쪽 구름 + 하위 노드
    o.append(vessel(cvx + 510, cvy + 60, 110, 56, "", PAPER, 9, 14))
    o.append(flow([(cvx + 450, cvy + 90), (cvx + 510, cvy + 90)], 2, "4 3"))
    for i in range(3):
        nx = cvx + 480 + i * 66
        o.append(flow([(cvx + 565, cvy + 116), (cvx + 565, cvy + 150),
                       (nx + 26, cvy + 150), (nx + 26, cvy + 176)], 1.6))
        o.append(vessel(nx, cvy + 176, 52, 40, "", PAPER, 8, 8))

    # 아래 상자 둘
    for i in range(2):
        o.append(rect(cvx + 210 + i * 130, cvy + 268, 106, 42, PAPER, stroke=MIST))
        o.append(bar(cvx + 228 + i * 130, cvy + 285, 70, 7, GRAPHITE, 0.7))
    return "".join(o)


if __name__ == "__main__":
    print("구축 사례 재현 이미지 →", OUT)
    write("projmng.svg", projmng(), "프로젝트 · 개발 관리 — USE CASE 다이어그램 화면 재현")
    write("hub.svg", hub(), "사내 통합 포털 — 홈 대시보드 화면 재현")
    write("hub-weather.svg", hub_weather(), "사내 통합 포털 — 실시간 날씨 현황 화면 재현")
    write("funeral.svg", funeral(), "장례식장 관리 시스템 — 빈소 현황 화면 재현")
    write("helpdesk.svg", helpdesk(), "헬프데스크 — 대시보드 화면 재현")
    write("utility.svg", utility(), "유틸리티 공급 — 실시간 계통 감시 화면 재현")
    write("utility-trend.svg", utility_trend(), "유틸리티 공급 — 사용량 Trend 화면 재현")
