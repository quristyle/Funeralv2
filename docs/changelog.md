# Changelog

## [Unreleased] - 2026-06-21
### Added
- AuthServer 계정 관리 CRUD API 추가 (`/system/account/*`)
- `AccountDto`, `CreateAccountDto`, `UpdateAccountDto` 정의

### Changed
- `IUserService` 및 `UserService` 계정 CRUD 기능 구현
- fronts 계정 관리 API 엔드포인트 `/auth/system/account/...` 경로로 수정 및 실제 DB 연동
