#!/usr/bin/env python3
"""
앱알림에 쓰는 **사람 얼굴 형상 그림자**를 굽는다.

[무엇에 쓰나]

AI 작업 결과 앱알림은 아이콘에 **지시한 사람의 얼굴**을 띄운다. 프로필 사진을
한 번도 올리지 않은 계정에는 띄울 얼굴이 없는데, 그때 빈 채로 두면 사진이 있는
사람과 없는 사람의 알림이 **다른 종류의 그림**(얼굴 ↔ 회사 로고)으로 갈려
「누가 시킨 일인가」를 아이콘으로 읽던 눈이 멈춘다. 그래서 얼굴 자리에 얼굴
모양인 것을 둔다.

  · 고르는 쪽 — `microservices/NotificationServer/Services/AvatarIconResolver.cs`
  · 내주는 쪽 — `web/src/Shared/JSini.Web.Components/Data/FileDownload.cs`

[왜 SVG 가 아닌가]

크롬은 **알림 아이콘으로 SVG 를 받지 않는다.** 화면 안에 그리는 그림이라면
`BrandMark` 처럼 SVG 로 두겠지만, 이 그림을 받아 가는 것은 브라우저의 알림
표시기라 래스터여야 한다. 그래서 굽는 단계가 필요하고, 구운 결과를 손으로
다시 그리지 않도록 `scripts/build-favicon.py` 와 같은 방식으로 생성기를 둔다.

[왜 192px 이고 왜 원인가]

192 는 PWA 아이콘의 기준 크기이고 알림 표시기가 그 언저리를 쓴다
(`pwa-icon-192.png` 와 같은 크기다). **원으로 굽는 이유**는 안드로이드의 알림
표시기가 아이콘을 원형으로 깎기 때문이다 — 네모로 두면 모서리가 잘려 어깨선이
어색하게 끊긴다. 우리가 먼저 원으로 두면 어느 쪽에서든 같은 그림이 된다.

    python3 scripts/build-avatar-fallback.py
"""

import pathlib

from PIL import Image, ImageDraw

OUT = (
    pathlib.Path(__file__).resolve().parent.parent
    / "web/src/Shell/JSini.Web.Shell/wwwroot/avatar-fallback.png"
)

SIZE = 192
# 8배로 그린 뒤 줄여서 가장자리를 다듬는다. PIL 에는 안티에일리어싱이 없다.
SUPER = SIZE * 8

# 포털이 쓰는 회색 두 단계. 바탕이 옅고 형상이 짙어야 작게 줄여도 사람으로 읽힌다.
BACKGROUND = (226, 232, 240, 255)   # slate-200
FIGURE = (100, 116, 139, 255)       # slate-500


def draw_person(canvas: Image.Image) -> None:
    """머리 하나와 어깨 하나. 흔히 보는 그 형상이다."""
    draw = ImageDraw.Draw(canvas)

    head_r = SUPER * 0.165
    head_cx, head_cy = SUPER / 2, SUPER * 0.38
    draw.ellipse(
        [head_cx - head_r, head_cy - head_r, head_cx + head_r, head_cy + head_r],
        fill=FIGURE,
    )

    # 어깨는 **넓고 낮은 타원의 윗부분**이다. 세로로 길면 불꽃처럼 뾰족해진다.
    width, top, height = SUPER * 0.74, SUPER * 0.605, SUPER * 0.78
    draw.ellipse(
        [SUPER / 2 - width / 2, top, SUPER / 2 + width / 2, top + height],
        fill=FIGURE,
    )


def main() -> None:
    person = Image.new("RGBA", (SUPER, SUPER), (0, 0, 0, 0))
    draw_person(person)

    # 바탕 원 밖으로 나간 어깨를 깎는다.
    circle = Image.new("L", (SUPER, SUPER), 0)
    ImageDraw.Draw(circle).ellipse([0, 0, SUPER - 1, SUPER - 1], fill=255)
    person.putalpha(
        Image.composite(person.getchannel("A"), Image.new("L", (SUPER, SUPER), 0), circle)
    )

    image = Image.new("RGBA", (SUPER, SUPER), (0, 0, 0, 0))
    ImageDraw.Draw(image).ellipse([0, 0, SUPER - 1, SUPER - 1], fill=BACKGROUND)
    image = Image.alpha_composite(image, person)

    image.resize((SIZE, SIZE), Image.LANCZOS).save(OUT, optimize=True)
    print(f"구웠다: {OUT}")


if __name__ == "__main__":
    main()
