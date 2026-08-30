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

# 8-1) 앱 아이콘 — **글자 옆에 서는** 작은 마크.
#
# 파비콘은 잉크 블록이라 혼자 있을 때 강하다. 그런데 UI 의 머리글 자리처럼
# 바로 옆에 이름 글자가 붙는 곳에서는 32px 블록이 글자와 무게가 맞지 않고,
# 어두운 테마에서는 흰 블록이 되어 더 튄다. 업무 포털의 사이드바가 그런 자리다.
#
# 그래서 블록 없이 **블레이드 J 글자만** 투명 배경에 둔다. 글자는 파비콘과 같은
# 블레이드 J 라 한 집안으로 읽히고, 배경이 없어 어떤 모양으로 잘리는 틀에 넣어도 안전하다.
APP = 64
app_s = 1.3333
app_w = width("J", app_s)
app_j = path("J", app_s, (APP - app_w) / 2, (APP - 30 * app_s) / 2)

write("app-icon.svg",
      head(APP, APP, "JSINI 앱 아이콘", "이름 글자 옆에 서는 작은 마크. 블레이드 J 만, 배경 없음")
      + f'    <path d="{app_j}" fill="{INK}"/>\n</svg>\n')
write("app-icon-knockout.svg",
      head(APP, APP, "JSINI 앱 아이콘 녹아웃", "어두운 배경용 블레이드 J. 배경 없음")
      + f'    <path d="{app_j}" fill="{PAPER}"/>\n</svg>\n')
write("app-icon-current.svg",
      head(APP, APP, "JSINI 앱 아이콘 currentColor", "글자색을 따르는 인라인용 블레이드 J")
      + f'    <path d="{app_j}" fill="currentColor"/>\n</svg>\n')

# 8-2) favicon.ico — SVG 를 못 읽는 옛 브라우저용.
#
# SVG 를 변환하지 않고 **같은 좌표에서 다시 그린다.** 변환기를 쓰면 도구가 하나 더 늘고,
# 이 모양은 다각형 둘이라 직접 그리는 편이 오히려 정확하다.
#
# Pillow 가 있어야 한다. 없으면 조용히 건너뛴다 — .ico 하나 때문에 SVG 생성 전체가
# 막히면 안 된다. 필요할 때만 깔면 된다:  pip install pillow
#
# 안티에일리어싱은 8배로 그린 뒤 줄여서 얻는다. 16px 에서 J 의 스템이 2px 이 채 안 되므로
# 계단이 그대로 보이면 글자가 아니라 얼룩으로 읽힌다.
def _polys_from_path(d):
    """이 파일이 만드는 `M x,y L x,y ... Z` 형식만 읽는다. 곡선은 애초에 쓰지 않는다."""
    pts = []
    for token in d.replace("M", " ").replace("L", " ").replace("Z", " ").split():
        x, y = token.split(",")
        pts.append((float(x), float(y)))
    return pts


def write_ico(name, block_d, cut_d, fill):
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        print(f"{name} 건너뜀 (Pillow 없음 — pip install pillow)")
        return

    sizes = [16, 32, 48, 64, 128, 256]
    ss = 8                       # 초과표본 배율
    base = FAV * ss

    img = Image.new("RGBA", (base, base), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.polygon([(x * ss, y * ss) for x, y in _polys_from_path(block_d)], fill=fill)
    # ImageDraw 는 픽셀을 합성하지 않고 그대로 쓴다. 알파 0 으로 칠하면 실제로 파인다.
    draw.polygon([(x * ss, y * ss) for x, y in _polys_from_path(cut_d)], fill=(0, 0, 0, 0))

    # Pillow 의 ICO 저장은 **기준 이미지에서 스스로 줄여** 각 크기를 만든다.
    # 그 축소는 우리가 원하는 품질이 아니라, LANCZOS 로 직접 줄인 것을 append_images 로
    # 대신 넣는다(그 인자가 자동 축소본을 대체한다).
    # 기준 이미지는 **가장 큰 것**이어야 한다. 작은 것을 기준으로 주면 그보다 큰 크기가 빠진다.
    frames = {s: img.resize((s, s), Image.LANCZOS) for s in sizes}
    largest = max(sizes)
    frames[largest].save(
        os.path.join(OUT, name), format="ICO",
        sizes=[(s, s) for s in sizes],
        append_images=[frames[s] for s in sizes if s != largest])
    print(name)


write_ico("favicon.ico", block, fav_j, (10, 10, 10, 255))          # Ink


# 8-3) PWA 아이콘 — 포털(portal.jsini.co.kr)을 앱으로 설치할 때 쓰는 PNG 들.
#
# .ico 와 같은 이유로 SVG 를 변환하지 않고 **같은 좌표에서 다시 그린다.**
# 매니페스트가 요구하는 세 벌이 다르다:
#   · any(192·512)      — 파비콘과 같은 모양(깎인 잉크 블록 + J 음각), 투명 배경.
#   · maskable(512)     — OS 가 원 · 둥근 사각 등 임의 모양으로 잘라 쓰므로
#                         **가장자리 없이 꽉 찬** 잉크 판에 J 를 중앙 안전영역(80%)
#                         안에 둔다. 투명이 남으면 잘린 자리가 얼룩진다.
#   · apple-touch(180)  — iOS 는 투명을 검정으로 칠해 버리므로 maskable 과 같은
#                         꽉 찬 판을 쓴다 (모서리는 iOS 가 알아서 둥글린다).
def write_png(name, size, draw_fn):
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        print(f"{name} 건너뜀 (Pillow 없음 — pip install pillow)")
        return

    ss = 8                                    # .ico 와 같은 초과표본 배율
    px = size * ss / FAV                      # 64 좌표계 → 픽셀 배율
    img = Image.new("RGBA", (size * ss, size * ss), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(img), px)
    img.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, name), format="PNG")
    print(name)


_INK_RGBA = (10, 10, 10, 255)
_PAPER_RGBA = (255, 255, 255, 255)


def _favicon_shape(draw, px):
    """파비콘과 동일 — 깎인 블록에 J 음각, 배경 투명."""
    draw.polygon([(x * px, y * px) for x, y in _polys_from_path(block)], fill=_INK_RGBA)
    draw.polygon([(x * px, y * px) for x, y in _polys_from_path(fav_j)], fill=(0, 0, 0, 0))


def _fullbleed_shape(draw, px):
    """꽉 찬 잉크 판 + 중앙의 종이색 J (안전영역 80% 안)."""
    draw.rectangle([0, 0, FAV * px, FAV * px], fill=_INK_RGBA)
    s = 1.15                                  # J 높이 30×1.15 ≈ 캔버스의 54% — 안전영역 안
    j = path("J", s, (FAV - width("J", s)) / 2, (FAV - 30 * s) / 2)
    draw.polygon([(x * px, y * px) for x, y in _polys_from_path(j)], fill=_PAPER_RGBA)


write_png("pwa-icon-192.png", 192, _favicon_shape)
write_png("pwa-icon-512.png", 512, _favicon_shape)
write_png("pwa-icon-maskable-512.png", 512, _fullbleed_shape)
write_png("apple-touch-icon.png", 180, _fullbleed_shape)

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

# 9-1) 로더 — 기다리는 동안 보여 주는 것.
#
# **심볼을 굴리지 않는다.** 사용 규칙(README 5절)이 기울이기·회전·비율 변경을 금지한다.
# 정체성 마크를 찌그러뜨리면서 쓰는 것이 로딩 표시다 — 그래서 심볼은 대상이 아니다.
#
# 대신 상승 조각을 쓴다. 이것은 애초에 "페이지 전체에 같은 리듬을 반복하는 보조 그래픽"
# 으로 둔 것이라(README 6절) 기다림 표시에 그대로 맞는다. 각도도 규칙 안에 있다.
#
# 움직임은 조각이 차례로 솟았다 내려앉는 것이다. 왼쪽에서 오른쪽으로 물결이 지나가면서도
# **오른쪽으로 갈수록 높은 실루엣**은 유지된다 — 그래야 정지 상태의 모티프와 같은 것으로 읽힌다.
#
# 색은 `currentColor` 에 명도 4단계다. 밝은 화면에서는 잉크 쪽, 어두운 화면에서는 흰 쪽으로
# 글자색이 뒤집히므로 파일 하나로 두 테마를 다 받는다.
LOADER = 48
LOADER_BASE = 44          # 바닥선
LOADER_W = 6              # 조각 너비
LOADER_GAP = 5
LOADER_SHEAR_DEG = 12.4   # 사선 22 : 높이 100 → atan(0.22)
LOADER_HEIGHTS = [16, 22, 28, 34]
LOADER_TONES = [0.3, 0.5, 0.75, 1]
LOADER_CYCLE = 1.1        # 초


def loader_svg(title):
    body = head(LOADER, LOADER, title,
                "상승 조각이 차례로 솟는 대기 표시. 색은 currentColor 를 따른다")

    # 기울이면 위쪽이 오른쪽으로 밀린다. 그만큼 되돌려 가운데에 세운다.
    shift = LOADER_BASE * 0.22 / 2
    body += "  <style>\n"
    body += ("    .jsini-loader-bar { transform-origin: center bottom;"
             f" animation: jsini-loader {LOADER_CYCLE}s ease-in-out infinite; }}\n")
    for i in range(len(LOADER_HEIGHTS)):
        body += (f"    .jsini-loader-bar:nth-child({i + 1})"
                 f" {{ animation-delay: {i * 0.12:g}s; }}\n")
    body += ("    @keyframes jsini-loader {\n"
             "      0%, 100% { transform: scaleY(0.55); }\n"
             "      50%      { transform: scaleY(1); }\n"
             "    }\n")
    # 움직임을 원하지 않는 사람에게는 멈춘 그림을 준다.
    body += ("    @media (prefers-reduced-motion: reduce) {\n"
             "      .jsini-loader-bar { animation: none; }\n"
             "    }\n")
    body += "  </style>\n"

    body += f'  <g transform="translate({n(shift)},0) skewX(-{n(LOADER_SHEAR_DEG)})">\n'
    for i, (h, tone) in enumerate(zip(LOADER_HEIGHTS, LOADER_TONES)):
        x = 5 + i * (LOADER_W + LOADER_GAP)
        body += (f'    <rect class="jsini-loader-bar" x="{n(x)}" y="{n(LOADER_BASE - h)}"'
                 f' width="{LOADER_W}" height="{n(h)}"'
                 f' fill="currentColor" opacity="{n(tone)}"/>\n')
    body += "  </g>\n"
    return body + "</svg>\n"


write("loader-shards.svg", loader_svg("JSINI 로더"))

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
         "symbol-duotone.svg", "symbol-mono.svg", "favicon.svg", "app-icon.svg",
         "wordmark.svg", "construction.svg", "motif-shards.svg"]
DARK = ["logo-horizontal-knockout.svg", "logo-vertical-knockout.svg",
        "symbol-knockout.svg", "favicon-knockout.svg", "app-icon-knockout.svg",
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
