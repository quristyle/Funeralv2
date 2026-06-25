# WebP 이미지 규격화 보관 및 지연 생성(Lazy Generation) 설계 및 구현 계획

이 문서는 파일 마이크로서비스 `FileServer`의 성능 최적화와 저장 공간 사용 효율화를 위해 WebP 이미지 규격화와 지연 생성(Lazy Generation)을 추가하기 위한 구현 계획서입니다.

---

## 1. 개요 및 요구사항
- **목적**: 
  - 업로드된 이미지의 저장 공간을 최소화하면서 조회 시 CPU 오버헤드(리사이즈 작업)를 원천 차단하여 극도의 응답 속도 확보.
  - 생성 규격본은 압축률이 높은 WebP 포맷을 강제하여 트래픽 및 공간 절약.
- **주요 기능**:
  1. **원본 보존**: 업로드 시 원본 파일은 그대로 `Original` 폴더에 보존.
  2. **지연 생성 (Lazy Generation)**:
     - 최초 업로드 단계에서는 리사이징된 사본(Thumbnail, Medium, Large)을 미리 생성하지 않음.
     - 클라이언트가 특정 규격의 이미지를 처음 조회할 때, 온디맨드로 원본 이미지를 로드하여 WebP 포맷으로 리사이징 변환 저장.
     - 두 번째 조회부터는 연산 없이 저장된 WebP 파일을 즉시 물리 전송.
  3. **규격화 폴더 분류**:
     - `Original` (원본 보존)
     - `Thumbnail` (150x150, WebP)
     - `Medium` (600x600, WebP)
     - `Large` (1200x1200, WebP)
     - `Cache` (임의 크기 요청 리사이징, WebP)

---

## 2. 기술 설계
- **리사이징 규격**:
  - `Thumbnail`: Max 150 x 150 (비율 유지)
  - `Medium`: Max 600 x 600 (비율 유지)
  - `Large`: Max 1200 x 1200 (비율 유지)
- **포맷 변환**: `SixLabors.ImageSharp`의 `SaveAsWebpAsync()` 비동기 API 활용.
- **삭제 정책**:
  - 특정 파일 ID 삭제 시 원본 이미지뿐만 아니라 규격별 폴더에 지연 생성되어 존재하고 있는 사본 WebP 파일들과 캐시 파일들까지 동기화하여 모두 물리 삭제.

---

## 3. 구현 세부 태스크 (Implementation Plan)

### [태스크 1] 서비스 레이어 리팩토링 및 신규 규격 추가
- `IFileService`에 `GetMediumImageAsync`, `GetLargeImageAsync` 신규 시그니처 추가.
- `FileService` 생성자에서 규격 폴더들(`Original`, `Thumbnail`, `Medium`, `Large`, `Cache`) 경로 설정 및 부팅 시 자동 폴더 생성 처리.
- `GetPresetImageAsync` 공통 비동기 메서드를 구현해 WebP 변환 및 온디맨드(Lazy) 저장 흐름을 단일화.
- `DeleteFileAsync` 수정하여 다중 폴더 내 사본들의 일괄 물리 삭제 처리 장착.

### [태스크 2] 컨트롤러 엔드포인트 연동 및 리다이렉션 수정
- `FileEndpoints`에 `/medium/{id}` 및 `/large/{id}` 신규 라우트 연동.
- 이미지 파일 미존재로 `FileNotFoundException` 감지 시, Gateway 통과 후 원격 주소로 바운딩할 수 있도록 `fallbackUrl`로 리다이렉트하는 분기 논리 맵핑.

### [태스크 3] 테스트 도구 갱신 및 로컬 빌드
- `FileServer.http` 파일에 규격별 조회 테스트 추가.
- `dotnet build`를 사용해 최종 컴파일 검증 및 런타임 검사.

---

## 4. 예외 및 실패 처리
- **비이미지 파일 요청**: PDF나 TXT 등의 파일에 대해 썸네일, 미디움, 라지 변환 조회 시 즉시 `400 Bad Request` 에러 반환.
- **스토리지 디스크 I/O 이슈**: 권한 문제나 스토리지 포화 등으로 쓰기가 불가능한 경우, 에러 코드를 기록하고 예외를 격리하여 사용자에게 적절한 `500 Server Error` JSON 전달.
