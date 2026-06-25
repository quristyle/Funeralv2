# 파일서버 마이크로서비스 추가 설계 및 구현 계획

이 문서는 FuneralV2 시스템 내에 파일 관리를 전담하는 신규 마이크로서비스 `FileServer`를 설계하고 추가하기 위한 구현 계획서입니다.

---

## 1. 개요 및 요구사항
- **목적**: 파일의 통합 업로드, 다운로드, 삭제, 이미지 썸네일 생성, 이미지 크기별 리사이징 및 보관, 메타데이터 조회를 전담하는 독립된 파일 마이크로서비스 구축.
- **주요 기능**:
  1. **파일 업로드**: 원본 파일을 스토리지에 저장하고 데이터베이스에 메타데이터를 저장.
  2. **파일 다운로드**: 고유 식별자(UUID)를 기반으로 원본 파일을 다운로드.
  3. **이미지 썸네일 생성**: 이미지 파일인 경우 기본 크기(예: 150x150)의 썸네일을 요청 시 실시간 생성(또는 저장)하여 제공.
  4. **이미지 크기별 보관/변환**: 요청된 크기(width, height)에 맞추어 이미지를 조절하여 반환.
  5. **파일 삭제**: DB 메타데이터와 물리 저장소의 파일을 삭제.
  6. **파일 메타조회**: DB에 보관된 파일 크기, 원본명, 업로드 시각, ContentType 등의 정보를 조회.

---

## 2. 기술 설계
- **프레임워크**: .NET 8.0 Minimal API (C#)
- **데이터베이스**: PostgreSQL (스키마: `smfr`, 테이블: `file_metadata`)
- **이미지 처리 라이브러리**: `SixLabors.ImageSharp (2.1.9)` (크로스플랫폼 이미지 디코딩/리사이징 지원)
- **로컬 스토리지 구조**:
  - 원본 파일 저장 경로: `[AppRoot]/Uploads/Original`
  - 리사이즈/썸네일 캐시 경로: `[AppRoot]/Uploads/Cache`
- **보안**: 
  - 게이트웨이(YARP)를 통한 라우팅 및 1차 JWT 인증 처리.
  - 게이트웨이가 전달해 준 `X-User-Id` 헤더를 활용해 업로더 식별 가능.

---

## 3. 데이터베이스 테이블 설계
### `smfr.file_metadata` 테이블 정의
- `id`: UUID (기본키)
- `original_name`: VARCHAR(255) (원본 파일명)
- `stored_name`: VARCHAR(255) (실제 디스크에 저장된 고유 파일명)
- `path`: VARCHAR(500) (디렉토리 상대경로)
- `size`: BIGINT (파일 크기)
- `content_type`: VARCHAR(100) (MIME 타입)
- `is_image`: BOOLEAN (이미지 여부)
- `created_at`: TIMESTAMP (업로드 일시)
- `created_by`: VARCHAR(100) (업로더 ID)

---

## 4. 구현 일정 및 세부 태스크 (Implementation Plan)

### [태스크 1] 데이터베이스 엔티티 및 DbContext 추가
- `FileMetadata.cs` 엔티티 생성.
- `FileDbContext.cs` 설정 및 테이블 자동 생성 기능 구현 (마이크로서비스 실행 시 테이블 미존재 시 자동 생성되도록 구성하여 설정 편의성 도모).

### [태스크 2] 파일 비즈니스 서비스 구현
- `IFileService.cs` 및 `FileService.cs` 구현.
- 로컬 스토리지에 물리적으로 파일 저장 및 삭제 처리.
- `ImageSharp`를 이용한 썸네일 생성 및 임의 크기(width, height) 리사이징 로직 구현.

### [태스크 3] API 엔드포인트 구현 (Minimal API)
- `/api/file/upload` (POST, Multi-part Form)
- `/api/file/download/{id}` (GET)
- `/api/file/thumbnail/{id}` (GET)
- `/api/file/resize/{id}` (GET)
- `/api/file/{id}` (DELETE)
- `/api/file/metadata/{id}` (GET)

### [태스크 4] API Gateway (YARP) 라우팅 설정 추가
- `ApiGateway/appsettings.json` 내 `ReverseProxy` 섹션에 `file-route` 및 `file-cluster` 등록.
- 로컬 포트는 `5350`으로 바인딩.

### [태스크 5] 개발/실행 스크립트 갱신
- `dev.bat`, `backend_run_ubuntu.sh`, `backend_run_mac.sh`에 `FileServer` 프로젝트의 빌드 및 실행 명령 추가.

---

## 5. 예외 및 실패 처리
- **파일 미존재**: HTTP 404 Not Found 및 JSON 에러 반환.
- **이미지가 아닌 파일의 썸네일/리사이즈 요청**: HTTP 400 Bad Request 에러 반환.
- **크기 인자 누락**: 기본 크기 지정 혹은 원본 반환 처리.
- **스토리지 I/O 실패**: 에러 로그 기록 후 500 Internal Server Error 반환.
