# @vben/vsh

Vue Vben Admin 프로젝트의 개발 및 관리를 위한 Shell 스크립트 도구 모음입니다.

## 주요 기능

- 🚀 Node.js 기반의 현대적인 Shell 도구
- 📦 모듈식 개발 및 온디맨드 로딩 지원
- 🔍 의존성 검사 및 분석 기능 제공
- 🔄 순환 의존성 스캔 지원
- 📝 패키지 배포 검사 기능 제공

## 설치

```bash
# pnpm을 사용하여 설치
pnpm add -D @vben/vsh

# 또는 npm 사용
npm install -D @vben/vsh

# 또는 yarn 사용
yarn add -D @vben/vsh
```

## 사용 방법

### 전역 설치

```bash
# 전역 설치
pnpm add -g @vben/vsh

# vsh 명령 사용
vsh [command]
```

### 로컬 사용

```bash
# package.json에 스크립트 추가
{
  "scripts": {
    "vsh": "vsh"
  }
}

# 명령 실행
pnpm vsh [command]
```

## 명령 목록

- `vsh check-deps`: 프로젝트 의존성 검사
- `vsh scan-circular`: 순환 의존성 스캔
- `vsh publish-check`: 패키지 배포 설정 검사
