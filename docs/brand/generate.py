# -*- coding: utf-8 -*-
"""JSINI 브랜드 키트 SVG 생성기.

레터폼은 대문자 각진 기하. 캡 높이 30, 스템 6 (5:1) 이 기준 단위다.
곡선은 없다. 수직/수평 + 사선이고, J 의 블레이드 컷만 정확히 45° 다.
"""
import os

OUT = os.path.dirname(os.path.abspath(__file__))

INK = "#0A0A0A"
GRAPHITE = "#1C1C1E"
STEEL = "#6E6E73"
MIST = "#D2D2D7"
PAPER = "#FFFFFF"

# 캡 높이 30 · 스템 6 기준 글리프. (x, y) 목록, y 는 아래로 증가.
GLYPHS = {
    # J: 스템 오른쪽 + 아래 발. 스템 상단을 45° 로 베어낸다(블레이드 컷).
    "J": ([(16, 6), (22, 0), (22, 30), (0, 30), (0, 24), (16, 24)], 22),
    # J 평판형(블레이드 없음). 소형 사이즈용 대체.
    "Jflat": ([(16, 0), (22, 0), (22, 30), (0, 30), (0, 24), (16, 24)], 22),
    # S: 6 유닛 다섯 띠(상단바·좌상스템·중간바·우하스템·하단바).
    "S": ([(0, 0), (22, 0), (22, 6), (6, 6), (6, 12), (22, 12), (22, 30),
           (0, 30), (0, 24), (16, 24), (16, 18), (0, 18)], 22),
    "I": ([(0, 0), (6, 0), (6, 30), (0, 30)], 6),
    "N": ([(0, 30), (0, 0), (6, 0), (18, 20), (18, 0), (24, 0), (24, 30),
           (18, 30), (6, 10), (6, 30)], 24),
}


def n(v):
    return f"{v:g}"


def path(name, s=1.0, ox=0.0, oy=0.0):
    pts, _ = GLYPHS[name]
    return "M" + " L".join(f"{n(ox + x * s)},{n(oy + y * s)}" for x, y in pts) + " Z"


def width(name, s=1.0):
    return GLYPHS[name][1] * s


# ── 워드마크 ─────────────────────────────────────────────────────────────
# JSINI 대문자, 트래킹 10 유닛(캡 높이의 33%). 첫 글자 J 만 블레이드 컷.
WORD = ["J", "S", "I", "N", "I"]
TRACK = 10


def wordmark_paths(s=1.0, ox=0.0, oy=0.0):
    out, x = [], ox
    for g in WORD:
        out.append(path(g, s, x, oy))
        x += width(g, s) + TRACK * s
    return out, x - TRACK * s - ox


WM_W = wordmark_paths()[1]      # 120
WM_H = 30

# ── 심볼 ────────────────────────────────────────────────────────────────
# 스케일 2 (캡 60 · 스템 12). J 를 왼쪽에 두고 S 를 4 유닛 겹친다.
# 겹침을 4 로 묶은 이유: 이보다 크면 1색 녹아웃 키라인이 S 의 좌측 스템을 끊는다.
SYM_S = 2.0
SYM_J_X, SYM_Y = 18.0, 30.0
SYM_S_X = SYM_J_X + width("J", SYM_S) - 4      # 58
SYM_BOX = 120
SYM_W = (SYM_S_X + width("S", SYM_S)) - SYM_J_X   # 84
SYM_H = 30 * SYM_S                                # 60

SYM_J = path("J", SYM_S, SYM_J_X, SYM_Y)
SYM_SS = path("S", SYM_S, SYM_S_X, SYM_Y)


def head(vb_w, vb_h, title, desc, extra=""):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {n(vb_w)} {n(vb_h)}"'
            f' width="{n(vb_w)}" height="{n(vb_h)}" role="img"{extra}>\n'
            f'  <title>{title}</title>\n  <desc>{desc}</desc>\n')


def write(name, body):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    print(name)


def symbol_group(j_fill, s_fill, keyline=None, dx=0.0, dy=0.0, s=1.0):
    """심볼 두 글자. keyline 이 있으면 J 실루엣 바깥으로 2 유닛 녹아웃 테를 두른다."""
    def tf(d):
        if dx or dy or s != 1.0:
            return f'    <g transform="translate({n(dx)},{n(dy)}) scale({n(s)})">\n      {d}\n    </g>\n'
        return f'    {d}\n'
    out = tf(f'<path d="{SYM_SS}" fill="{s_fill}"/>')
    if keyline:
        out += tf(f'<path d="{SYM_J}" fill="none" stroke="{keyline}" stroke-width="4"/>')
    out += tf(f'<path d="{SYM_J}" fill="{j_fill}"/>')
    return out


# 1) 심볼 · 2톤 (기본)
write("symbol-duotone.svg",
      head(SYM_BOX, SYM_BOX, "JSINI 심볼", "각진 대문자 J 와 S 가 겹친 2톤 심볼")
      + symbol_group(INK, STEEL) + "</svg>\n")

# 2) 심볼 · 1색 (녹아웃 키라인)
write("symbol-mono.svg",
      head(SYM_BOX, SYM_BOX, "JSINI 심볼 1색", "J 실루엣에 녹아웃 키라인을 둔 단색 심볼")
      + symbol_group(INK, INK, keyline=PAPER) + "</svg>\n")

# 3) 심볼 · 녹아웃 (어두운 배경)
write("symbol-knockout.svg",
      head(SYM_BOX, SYM_BOX, "JSINI 심볼 녹아웃", "어두운 배경용 밝은 2톤 심볼")
      + symbol_group(PAPER, MIST) + "</svg>\n")

# 4) 심볼 · currentColor (웹 인라인용)
write("symbol-current.svg",
      head(SYM_BOX, SYM_BOX, "JSINI 심볼 currentColor",
           "글자색을 따르는 인라인용 심볼. S 는 45% 명도로 내려 2톤을 흉내낸다")
      + f'    <path d="{SYM_SS}" fill="currentColor" opacity="0.45"/>\n'
      + f'    <path d="{SYM_J}" fill="currentColor"/>\n</svg>\n')


# 5) 워드마크
def wordmark_svg(fill, title):
    ps, _ = wordmark_paths()
    body = head(WM_W, WM_H, title, "JSINI 대문자 워드마크. 트래킹 10 유닛, J 상단 45° 블레이드 컷")
    for d in ps:
        body += f'    <path d="{d}" fill="{fill}"/>\n'
    return body + "</svg>\n"


write("wordmark.svg", wordmark_svg(INK, "JSINI 워드마크"))
write("wordmark-knockout.svg", wordmark_svg(PAPER, "JSINI 워드마크 녹아웃"))
write("wordmark-current.svg", wordmark_svg("currentColor", "JSINI 워드마크 currentColor"))

# 6) 가로 조합 (기본형)
# 심볼 높이 60, 워드마크 캡 30(절반). 사이 간격 24 = 심볼 스템 두께 2개분.
GAP_H = 24
LH_W = SYM_W + GAP_H + WM_W
LH_H = SYM_H


def horizontal_svg(j_fill, s_fill, w_fill, title, keyline=None):
    body = head(LH_W, LH_H, title, "심볼 왼쪽, JSINI 오른쪽. 가로 조합 기본형")
    body += symbol_group(j_fill, s_fill, keyline, dx=-SYM_J_X, dy=-SYM_Y)
    ps, _ = wordmark_paths(1.0, SYM_W + GAP_H, (SYM_H - WM_H) / 2)
    for d in ps:
        body += f'    <path d="{d}" fill="{w_fill}"/>\n'
    return body + "</svg>\n"


write("logo-horizontal.svg", horizontal_svg(INK, STEEL, INK, "JSINI 가로 조합"))
write("logo-horizontal-knockout.svg",
      horizontal_svg(PAPER, MIST, PAPER, "JSINI 가로 조합 녹아웃"))
write("logo-horizontal-mono.svg",
      horizontal_svg(INK, INK, INK, "JSINI 가로 조합 1색", keyline=PAPER))

# 7) 세로 조합 (보조)
GAP_V = 18
LV_W = WM_W
LV_H = SYM_H + GAP_V + WM_H


def vertical_svg(j_fill, s_fill, w_fill, title):
    body = head(LV_W, LV_H, title, "심볼 위, JSINI 아래 중앙 정렬. 세로 조합")
    body += symbol_group(j_fill, s_fill, dx=(LV_W - SYM_W) / 2 - SYM_J_X, dy=-SYM_Y)
    ps, _ = wordmark_paths(1.0, 0, SYM_H + GAP_V)
    for d in ps:
        body += f'    <path d="{d}" fill="{w_fill}"/>\n'
    return body + "</svg>\n"


write("logo-vertical.svg", vertical_svg(INK, STEEL, INK, "JSINI 세로 조합"))
write("logo-vertical-knockout.svg", vertical_svg(PAPER, MIST, PAPER, "JSINI 세로 조합 녹아웃"))

# 8) 파비콘 · 앱 아이콘 — 정사각이 필요한 자리는 블레이드 J 한 자로 축약한다.
FAV = 64
fav_s = 1.2
fav_w = width("J", fav_s)
fav_j = path("J", fav_s, (FAV - fav_w) / 2, (FAV - 30 * fav_s) / 2)
# 블록의 우상단을 45° 로 깎는다. 컷 라인은 y = x - 48.
block = f"M0,0 L48,0 L64,16 L64,{FAV} L0,{FAV} Z"
write("favicon.svg",
      head(FAV, FAV, "JSINI 파비콘", "우상단을 45° 깎은 잉크 블록에 블레이드 J 를 음각")
      + f'    <path fill="{INK}" fill-rule="evenodd" d="{block} {fav_j}"/>\n</svg>\n')
write("favicon-knockout.svg",
      head(FAV, FAV, "JSINI 파비콘 녹아웃", "밝은 블록에 블레이드 J 를 음각")
      + f'    <path fill="{PAPER}" fill-rule="evenodd" d="{block} {fav_j}"/>\n</svg>\n')

# 9) 상승 조각 모티프 — 심볼과 별개로 사이트 전반에 반복하는 보조 그래픽.
SHARD_TONES = [MIST, STEEL, GRAPHITE, INK]


def shards(x0=18, base=160, w=28, gap=24, h0=40, step=32, shear=22, tones=None):
    tones = tones or SHARD_TONES
    out = []
    for i, tone in enumerate(tones):
        x = x0 + i * (w + gap)
        top = base - (h0 + i * step)
        pts = [(x, base), (x + w, base), (x + w + shear, top), (x + shear, top)]
        d = " ".join(f"{n(px)},{n(py)}" for px, py in pts)
        out.append(f'    <polygon points="{d}" fill="{tone}"/>')
    return "\n".join(out) + "\n"


write("motif-shards.svg",
      head(242, 178, "상승 조각 모티프", "우상향으로 높아지는 사선 조각 네 개. 명도 4단계")
      + shards() + "</svg>\n")
write("motif-shards-knockout.svg",
      head(242, 178, "상승 조각 모티프 녹아웃", "어두운 배경용 상승 조각")
      + shards(tones=[GRAPHITE, STEEL, MIST, PAPER]) + "</svg>\n")

# 10) OG 이미지 1200×630
og = head(1200, 630, "JSINI OG 이미지", "잉크 배경에 가로 조합 녹아웃과 상승 조각")
og += f'    <rect width="1200" height="630" fill="{INK}"/>\n'
og += f'    <rect x="0" y="0" width="1200" height="4" fill="{STEEL}"/>\n'
og += '    <g transform="translate(760,300) scale(1.6)">\n'
og += shards(x0=0, base=160, w=24, gap=20, h0=36, step=30, shear=18,
             tones=[GRAPHITE, "#3A3A3C", STEEL, MIST])
og += '    </g>\n'
og += f'    <g transform="translate(120,270) scale(1.9)">\n{horizontal_svg(PAPER, MIST, PAPER, "x").split(chr(10), 3)[3].replace("</svg>", "").rstrip()}\n    </g>\n'
og += "</svg>\n"
write("og-image.svg", og)

# 11) 구성 도해 — 그리드 · 보호 여백 · 최소 크기 근거를 한 장에 남긴다.
CS = 12  # 보호 여백 = 심볼 스템 두께 1개분
cw, ch = SYM_W + CS * 2, SYM_H + CS * 2
con = head(cw + 80, ch + 80, "JSINI 심볼 구성", "12 유닛 그리드와 보호 여백 도해")
con += '    <g transform="translate(40,40)">\n'
for i in range(0, int(cw) + 1, 12):
    con += f'      <line x1="{i}" y1="0" x2="{i}" y2="{n(ch)}" stroke="{MIST}" stroke-width="0.5"/>\n'
for i in range(0, int(ch) + 1, 12):
    con += f'      <line x1="0" y1="{i}" x2="{n(cw)}" y2="{i}" stroke="{MIST}" stroke-width="0.5"/>\n'
con += (f'      <rect x="0" y="0" width="{n(cw)}" height="{n(ch)}" fill="none"'
        f' stroke="{STEEL}" stroke-width="1" stroke-dasharray="4 4"/>\n')
con += symbol_group(INK, STEEL, dx=CS - SYM_J_X, dy=CS - SYM_Y).replace("    <", "      <")
con += '    </g>\n</svg>\n'
write("construction.svg", con)

# 12) 확인용 대지 — 파일을 고친 뒤 눈으로 한 번 보고 넘어가기 위한 것이다.
LIGHT = ["logo-horizontal.svg", "logo-horizontal-mono.svg", "logo-vertical.svg",
         "symbol-duotone.svg", "symbol-mono.svg", "favicon.svg",
         "wordmark.svg", "construction.svg", "motif-shards.svg"]
DARK = ["logo-horizontal-knockout.svg", "logo-vertical-knockout.svg",
        "symbol-knockout.svg", "favicon-knockout.svg",
        "motif-shards-knockout.svg", "og-image.svg"]


def card(fname, bg, label_color):
    return (f'<figure style="margin:0;background:{bg};padding:20px;'
            f'border:1px solid #d2d2d7;display:flex;flex-direction:column;'
            f'align-items:center;gap:10px">'
            f'<img src="{fname}" style="max-width:100%;max-height:140px" alt="{fname}">'
            f'<figcaption style="font:11px/1 sans-serif;color:{label_color}">{fname}'
            f'</figcaption></figure>')


sheet = ('<!doctype html>\n<meta charset="utf-8">\n<title>JSINI 브랜드 키트 확인</title>\n'
         '<body style="margin:0;padding:16px;background:#f4f4f5;display:grid;'
         'grid-template-columns:repeat(3,1fr);gap:12px">\n')
sheet += "\n".join(card(f, "#FFFFFF", "#6E6E73") for f in LIGHT) + "\n"
sheet += "\n".join(card(f, "#0A0A0A", "#6E6E73") for f in DARK) + "\n</body>\n"
write("preview.html", sheet)

print("\n심볼 bbox:", SYM_W, "x", SYM_H, " 가로조합:", LH_W, "x", LH_H,
      " 세로조합:", LV_W, "x", LV_H)
