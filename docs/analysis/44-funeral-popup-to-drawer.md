# 44. 장례식장 등록·수정 창을 오른쪽 드로어로 (2026-09-02)

> 지시: "팝업 형태로 나타나지 말고 설정처럼 오른쪽에서 나타나는 형태로 개선해 줘.
> /building/audio, /decoration, /building/info 등의 화면도 마찬가지로."

## 1. 왜

포털 쪽 화면(회사 · 역할 · 메뉴 · 다국어 · 업무선택)은 등록·수정을 **오른쪽 드로어**로
연다. 그런데 장례식장 화면들만 **가운데 모달**이었다. 같은 시스템 안에서 같은 일
(행 하나를 고친다)을 하는데 창이 뜨는 자리가 화면마다 달랐다.

## 2. 바꾼 화면 아홉

| 화면 | 경로 | 창을 그리는 파일 |
|---|---|---|
| 장비배경이미지 | `/device/background` | `background/modules/background-upload-drawer.vue` |
| 영상 | `/building/video` | `video/modules/video-upload-drawer.vue` |
| 음원 | `/building/audio` | `audio/modules/audio-upload-drawer.vue` |
| 장식관리 | `/decoration` | `decoration/modules/decoration-upload-drawer.vue` |
| 건물 | `/building/info` | `info/index.vue` (화면 안에 있다) |
| 층 | `/building/floor` | `floor/index.vue` |
| 호실 | `/building/room` | `room/index.vue` |
| 장비 | `/building/device` | `device/modules/device-form-drawer.vue` |
| 고인 | `/building/deceased` | `deceased/modules/deceased-form-drawer.vue` |

## 3. 어떻게

`useVbenModal` → `useVbenDrawer` 로 바꾼 것이 거의 전부다. **두 부품의 API 가 같다** —
`open` · `close` · `setState` · `lock` · `unlock` · `getData` · `onConfirm` 이 이름까지
같아서, 열고 닫고 저장하는 코드는 한 줄도 손대지 않았다.

파일 이름도 함께 바꿨다(`*-upload-modal.vue` → `*-upload-drawer.vue`). 이름이 모달이라고
말하는데 드로어가 뜨면 다음 사람이 헛짚는다.

손댄 곳은 셋뿐이다.

- **본문 여백** `p-6` → `p-2`. 드로어 본문은 이미 `p-3` 을 갖고 있어(`drawer.vue`)
  예전 값을 그대로 두면 안쪽 여백이 두 겹이 된다.
- **폭은 기본값**(`w-130`)을 쓴다. 포털 쪽 드로어와 같은 폭이어야 통일이 된다.
  **고인 화면만 예외**로 `w-[1050px]` 을 유지했다 — 왼쪽 바로가기 + 오른쪽 스크롤의
  2단 구성이라 기본 폭에서는 두 단이 겹친다.
- **고인 화면의 높이**: `h-[680px]` → `h-full`. 드로어는 창 높이를 꽉 채우므로 예전
  모달처럼 못 박아 두면 큰 화면에서는 아래가 비고 작은 화면에서는 넘친다.
  본문 여백도 `content-class="p-0"` 으로 없앴다 — 왼쪽 띠가 가장자리에 붙어야
  경계선이 제 노릇을 한다.

장비 화면에서 `<DeviceModal @ok="handleSave">` 의 `@ok` 는 뗐다. 저장은 `onConfirm`
하나가 맡는다 — 둘 다 걸려 있으면 확인 한 번에 저장이 두 번 나갈 수 있다.

## 4. 모달로 남긴 것

**편집 창이 아닌 것은 그대로 뒀다.** 오른쪽에서 밀고 들어오는 것은 "이 행을 고친다"
라는 뜻으로 쓰고, 잠깐 보고 닫는 것은 가운데 모달이 맞다.

| 남긴 것 | 왜 |
|---|---|
| 음원 · 영상의 재생 창 | 플레이어다. 고치는 창이 아니다 |
| 고인 사진 자르기(`deceased-photo-crop-modal.vue`) | 드로어(고인 창) 안에서 다시 뜨는 도구다 |
| 상황판의 장비 상세(`status/modules/device-detail-modal.vue`) | 보기만 한다 |

## 5. [준수사항 3](../준수사항.md) 과의 관계

"모든 팝업은 헤더를 잡고 옮길 수 있어야 한다" 는 규칙에는 **드로어가 이미 예외로
적혀 있다** — 화면 가장자리에 붙는 부품이라 옮길 자리가 없다. 그래서 이 아홉 화면은
드래그 대상에서 빠진다. 규칙을 어긴 것이 아니라 규칙이 처음부터 갈라 둔 자리다.

## 6. 확인한 것

아홉 화면을 모두 열어 등록 또는 수정 단추를 누르고, 오른쪽에서 드로어가 뜨는 것과
제목 · 입력 칸 · 취소/확인 단추를 확인했다.

| 화면 | 뜬 제목 | 폭 |
|---|---|---|
| 장비배경이미지 | 배경 이미지 리소스 수정 | 기본 |
| 영상 | 동영상 리소스 수정 | 기본 |
| 음원 | 음원 리소스 수정 | 기본 |
| 장식관리 | 장식 리소스 수정 | 기본 |
| 건물 | 건물 정보 설정 | 기본 |
| 층 | 층 정보 설정 | 기본 |
| 호실 | 호실 정보 설정 | 기본 |
| 장비 | 장비 정보 설정 | 기본 |
| 고인 | 고인 종합 관리 (수정) | 1050px |

콘솔 오류는 없다. 새로 생긴 린트 지적도 없다(이 폴더의 기존 지적은 그대로다 —
import 순서 등, 손대지 않은 파일도 같은 상태다).
