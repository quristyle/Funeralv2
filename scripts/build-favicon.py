#!/usr/bin/env python3
"""
헤더의 JS 마크로 **파비콘과 앱 아이콘**을 굽는다.

[왜 생성기를 두나]

파비콘의 정본은 **헤더에 서 있는 그 마크**다(`JSini.Web.Components/Layout/
BrandMark.razor`). 좌표를 손으로 다시 그리면 로고를 고치는 날 탭 아이콘만
옛 모양으로 남고, 그 어긋남은 아무도 신고하지 않는다 — 늘 보이는 자리인데
아무도 쳐다보지 않는 자리라서 그렇다.

그래서 **좌표를 한 곳에 적고**(아래 `S_PATH`·`J_PATH` — BrandMark 의 것과
글자까지 같다) 나머지는 여기서 굽는다. 로고가 바뀌면 이 파일의 좌표를 고치고
다시 돌린다. `scripts/build-menu-icons.py` 와 같은 방식이다.

[생김새를 헤더와 다르게 한 곳이 둘 있다]

· **바탕을 깐다.** 헤더의 마크는 배경이 없고 색을 테마 토큰에서 받는다
  (`--jsini-text`·`--jsini-text-muted`). 파비콘에는 테마가 없다 — 투명하게
  두면 밝은 탭 막대에서는 보이고 어두운 탭 막대에서는 사라진다. 그래서
  짙은 타일을 깔고 **어두운 테마에서 쓰는 그 두 색**을 얹는다. 사용자가
  보는 색감 그대로이고, 어느 탭 막대에서도 읽힌다.

· **16px 에서는 S 를 지운다.** 두 획이 겹치는 마크라 16px 로 줄이면 S 의
  획(12/84 ≒ 2px)이 J 와 뭉개져 얼룩으로 보인다. 그 크기에서 남길 것은
  **알아보게 하는 획**이고 그것은 J 다.

[왜 ICO 하나로 끝내나 — SVG 링크를 걷어낸 이유]

한동안 `<head>` 에 **SVG 파비콘만** 걸려 있었다(옛 잉크 블록 J). SVG 는
한 장으로 모든 크기를 그리는 대신 **크기를 모른다** — 탭 막대의 16px 에서도
두 획을 다 그리므로 S 가 J 와 뭉개진다.

ICO 는 그 반대다. 크기마다 다른 그림을 담을 수 있어 16px 에는 J 만, 그 위로는
두 획을 담았다. 그래서 링크를 ICO 하나로 바꿨다 — 둘을 함께 걸면 브라우저마다
고르는 것이 달라 **같은 자리에서 다른 그림**이 뜬다.

SVG 는 그대로 굽는다(`brand/favicon.svg`). 브랜드 자산 폴더의 파비콘이 옛
모양으로 남으면 다음 사람이 그것을 정본으로 알고 가져다 쓴다.

[굽는 것]

| 파일 | 무엇 | 생김새 |
|---|---|---|
| `brand/favicon.ico` | 탭 아이콘 (크기 여섯) | 둥근 타일 |
| `brand/favicon.svg` | 그 벡터본 | 〃 |
| `pwa-icon-192.png` · `pwa-icon-512.png` | 설치한 앱 아이콘 | 〃 |
| `pwa-icon-maskable-512.png` | 안드로이드가 **제 모양으로 깎는** 것 | 네모 가득 + 마크를 작게 |
| `apple-touch-icon.png` | iOS 홈 화면 (180) | 네모 가득 |

**마스커블과 iOS 는 모서리를 둥글리지 않는다.** 그쪽은 플랫폼이 자기 모양으로
(원·둥근네모·물방울) 깎으므로, 우리가 미리 둥글리면 **두 번 깎여** 테두리에
검은 이 빠진 자국이 남는다.

마스커블만 마크를 작게 눕힌다(60/100). 안드로이드는 **가운데 80% 원 밖을
잘라내도 된다**고 보므로, 타일 크기 그대로 두면 S 의 오른쪽이 잘린다.

[돌리는 법]

    python3 scripts/build-favicon.py

필요한 것: `rsvg-convert`(librsvg) · Pillow. 산출물은 커밋한다 —
빌드에 파이썬을 끌어들이지 않는다.
"""

from __future__ import annotations

import io
import struct
import subprocess
import sys
from pathlib import Path

from PIL import Image

# ── 마크 ──────────────────────────────────────────────────────
#
# BrandMark.razor 의 좌표 그대로다. **S 가 먼저, J 가 나중**이어야 한다 —
# J 가 위에 얹혀야 「JS」 로 읽힌다(그쪽 머리말).

VIEW_W, VIEW_H = 84, 60

S_PATH = "M40,0 L84,0 L84,12 L52,12 L52,24 L84,24 L84,60 L40,60 L40,48 L72,48 L72,36 L40,36 Z"
J_PATH = "M32,12 L44,0 L44,60 L0,60 L0,48 L32,48 Z"

# 어두운 테마에서 헤더가 쓰는 값이다(BrandMark 머리말의 실측표).
COLOR_BG = "#0a0a0a"   # 매니페스트의 theme_color 와 같은 검정
COLOR_J = "#e6e8ea"
COLOR_S = "#9aa1ab"

# 타일 100 기준. 마크를 76 폭으로 눕히고 가운데 세운다.
TILE = 100
RADIUS = 18
MARK_W = 76
MARK_H = MARK_W * VIEW_H / VIEW_W

# ICO 에 담을 크기들. 16 은 탭 막대, 32 는 고해상도 탭 막대와 바로가기,
# 그 위는 검색 결과·바탕화면이 쓴다.
SIZES = [16, 32, 48, 64, 128, 256]

# 이 크기 이하에서는 S 를 지운다(머리말 참고).
J_ONLY_BELOW = 20

# 그때 쓰는 J 의 상자와 키. J 는 84×60 중 **왼쪽 44** 만 쓴다.
J_VIEW_W = 44
J_ONLY_H = 64

# 마스커블은 가운데 80% 원 안에 들어가야 한다. 마크 상자의 대각선이
# 그 원의 지름을 넘지 않는 폭이 65 언저리라 60 으로 잡았다.
MASKABLE_MARK_W = 60

# 굽는 PNG 들. (경로, 크기, 생김새)
PNG_TARGETS = [
    ("web/src/Shell/JSini.Web.Shell/wwwroot/pwa-icon-192.png", 192, "tile"),
    ("web/src/Shell/JSini.Web.Shell/wwwroot/pwa-icon-512.png", 512, "tile"),
    ("web/src/Shell/JSini.Web.Shell/wwwroot/pwa-icon-maskable-512.png", 512, "maskable"),
    ("web/src/Shell/JSini.Web.Shell/wwwroot/apple-touch-icon.png", 180, "square"),
]


def svg_for(size: int, style: str = "tile") -> str:
    """
    그 크기·생김새의 SVG 한 장.

    <c>style</c> 은 셋이다 — <c>tile</c>(둥근 모서리) · <c>square</c>(네모
    가득, 플랫폼이 깎는다) · <c>maskable</c>(네모 가득 + 마크를 작게).
    """
    radius = RADIUS if style == "tile" else 0
    mark_w = MASKABLE_MARK_W if style == "maskable" else MARK_W

    if size < J_ONLY_BELOW:
        # **J 자신을 가운데 세운다.** 두 획짜리 상자(84폭)를 그대로 쓰면
        # J 는 그 왼쪽 절반이라 타일 안에서 왼쪽으로 치우쳐 앉는다 —
        # 옆에 있어야 할 S 가 없으니 그 빈자리가 그냥 여백으로 보인다.
        scale = J_ONLY_H / VIEW_H
        body = f'<path d="{J_PATH}" fill="{COLOR_J}" />'
        x = (TILE - J_VIEW_W * scale) / 2
        y = (TILE - J_ONLY_H) / 2
    else:
        scale = mark_w / VIEW_W
        body = (f'<path d="{S_PATH}" fill="{COLOR_S}" />\n      '
                f'<path d="{J_PATH}" fill="{COLOR_J}" />')
        x = (TILE - mark_w) / 2
        y = (TILE - mark_w * VIEW_H / VIEW_W) / 2

    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {TILE} {TILE}" width="{size}" height="{size}">
  <rect width="{TILE}" height="{TILE}" rx="{radius}" ry="{radius}" fill="{COLOR_BG}" />
  <g transform="translate({x:.3f},{y:.3f}) scale({scale:.6f})">
      {body}
  </g>
</svg>
"""


def render(size: int, style: str = "tile") -> bytes:
    """SVG 를 그 크기의 PNG 로. **크기마다 벡터에서 새로 그린다** — 큰 것
    하나를 줄이면 16px 에서 획이 반 픽셀에 걸쳐 흐려진다."""
    out = subprocess.run(
        ["rsvg-convert", "-w", str(size), "-h", str(size), "-f", "png"],
        input=svg_for(size, style).encode("utf-8"),
        capture_output=True,
        check=True,
    )
    return out.stdout


def ico(images: dict[int, bytes]) -> bytes:
    """
    PNG 들을 ICO 한 장으로 묶는다.

    ICO 안에는 BMP 도 PNG 도 들어갈 수 있다. **PNG 로 담는다** — 크로뮴·
    파이어폭스·사파리와 윈도 비스타 이후가 모두 읽고, BMP 로 담으려면
    행을 뒤집고 AND 마스크를 덧붙여야 해서 손으로 틀리기 쉽다.
    """
    entries, blobs, offset = [], [], 6 + 16 * len(images)

    for size in sorted(images):
        blob = images[size]
        # 256 은 1바이트 칸에 안 들어가 0 으로 적는다(그 규약이다).
        entries.append(struct.pack(
            "<BBBBHHII",
            size if size < 256 else 0, size if size < 256 else 0,
            0, 0, 1, 32, len(blob), offset))
        blobs.append(blob)
        offset += len(blob)

    header = struct.pack("<HHH", 0, 1, len(images))
    return header + b"".join(entries) + b"".join(blobs)


def main() -> int:
    root = Path(__file__).resolve().parent.parent

    # **브랜드 자산 폴더에 굽는다**(Blazor Common 의 wwwroot). 셸의 wwwroot 가
    # 아닌 이유는 `<head>` 를 갖고 있는 것이 그쪽이 아니라 `JSiniHead` 이기
    # 때문이다 — 파일과 링크가 다른 프로젝트에 있으면 한쪽만 옮기는 날이 온다.
    brand = root / "web/src/Shared/JSini.Web.Components/wwwroot/brand"
    target = brand / "favicon.ico"

    rendered = {size: render(size) for size in SIZES}

    # 크기가 맞는지 확인하고 굽는다. rsvg 가 반올림으로 1px 어긋나면 ICO
    # 목록과 실제 그림이 달라지고, 그때 브라우저는 **말없이 다른 크기를
    # 고른다** — 흐린 아이콘이 그렇게 나온다.
    for size, blob in rendered.items():
        with Image.open(io.BytesIO(blob)) as im:
            if im.size != (size, size):
                print(f"크기가 어긋난다: {size} → {im.size}", file=sys.stderr)
                return 1

    target.write_bytes(ico(rendered))

    # 벡터본도 함께 굽는다. **크기를 적지 않는다** — 쓰는 쪽이 정한다.
    vector = brand / "favicon.svg"
    vector.write_text(
        svg_for(256).replace(' width="256" height="256"', "")
                    .replace("<svg ", '<svg role="img" ', 1)
                    .replace("<rect", "<title>JSINI</title>\n  <rect", 1),
        encoding="utf-8")

    print(f"구웠다: {target.relative_to(root)} ({target.stat().st_size:,} 바이트, {len(SIZES)}장)")
    print(f"구웠다: {vector.relative_to(root)}")

    # 앱 아이콘. **자리를 옮기지 않는다** — 매니페스트(`manifest.webmanifest`)와
    # `<link rel="apple-touch-icon">` 이 이 이름을 가리킨다. 이름을 바꾸면
    # 설치된 앱의 아이콘이 조용히 빈칸이 된다.
    for rel, size, style in PNG_TARGETS:
        path = root / rel
        path.write_bytes(render(size, style))
        print(f"구웠다: {rel.split('/')[-1]} ({size}px, {style})")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
