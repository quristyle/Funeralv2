# 다국어(i18n) 시스템 관리 가이드

본 프로젝트는 **Vben Admin** 프레임워크를 기반으로 하며, 초기 로딩 성능을 보장하면서도 운영 중 실시간으로 번역문을 수정할 수 있도록 **로컬 JSON 파일과 데이터베이스(DB)를 병합하는 하이브리드 방식**을 채택하고 있습니다.

## 1. 다국어 처리 개념 (Hybrid Architecture)

1.  **초기 로딩 (Fast Boot):** 앱 시작 시 `src/locales/langs` 하위의 JSON 파일을 즉시 로드하여 화면을 렌더링합니다. (네트워크 지연 없음)
2.  **동적 동기화 (Dynamic Sync):** 앱 로딩 직후, 백그라운드에서 백엔드 API를 호출하여 DB에 저장된 최신 번역 정보를 가져옵니다.
3.  **데이터 병합 (Merge):** DB 정보를 로컬 메시지에 병합(`mergeLocaleMessage`)하며, 동일한 키가 있을 경우 **DB 정보가 우선순위**를 가집니다.
4.  **실시간 반영 (Reactivity):** 병합된 데이터를 즉시 화면에 반영하기 위해 `i18n` 인스턴스의 로케일을 일시적으로 전환하여 전역 리렌더링을 유도합니다.

---

## 2. 데이터베이스 구조 (Backend)

### 테이블: `scom.i18n_resources`
다국어 자원은 평면적인 키(Dot Notation) 구조로 관리됩니다.

| 컬럼명 | 타입 | 설명 | 예시 |
| :--- | :--- | :--- | :--- |
| `key` | VARCHAR | 다국어 식별 키 | `common.operation`, `vxe.pager.total` |
| `locale` | VARCHAR | 언어 코드 | `ko`, `en-US` |
| `value` | TEXT | 번역된 문구 | `작업`, `전체 {0} 건` |
| `category`| VARCHAR | 관리 분류 | `common`, `vxe`, `system` |

### 주요 엔드포인트 (`AuthServer`)
-   `GET /auth/system/i18n/paged`: 페이징 및 필터링 검색
-   `GET /auth/system/i18n/{locale}`: 특정 언어 전체 조회
-   `POST/PUT/DELETE /auth/system/i18n`: CRUD 관리

---

## 3. 프론트엔드 구현 상세

### 3.1 초기화 순서 (`src/bootstrap.ts`)
Pinia 스토어가 i18n보다 먼저 초기화되어야 합니다. 그래야 i18n 로드 시점에 API 호출을 위한 인증 토큰에 접근할 수 있습니다.
```typescript
await initStores(app, { namespace }); // 1순위: 스토어
await setupI18n(app);                 // 2순위: 다국어
```

### 3.2 DB 병합 및 반응성 트리거 (`src/locales/index.ts`)
DB 데이터를 가져온 후 Vue가 변경 사항을 인식하도록 하는 핵심 로직입니다.
```typescript
async function fetchAndMergeDbMessages(lang: string) {
    console.log('여기서 에러가 나면 ');
  const data = await getI18nListByLocale(lang);
    console.log('다음코드가 진행이 되나?');
  const dbMessages = transformDbResourcesToMessages(data);
  i18n.global.mergeLocaleMessage(lang, dbMessages);

  // 반응성 강제 트리거: 로케일을 잠시 비웠다 채움으로써 전체 UI 갱신
  const currentLocale = i18n.global.locale.value;
  i18n.global.locale.value = '' as any;
  nextTick(() => {
    i18n.global.locale.value = currentLocale;
  });
}
```

### 3.3 Vxe-Table 연동 (`src/adapter/vxe-table.ts`)
그리드 내부의 시스템 문구(`vxe.`)를 처리하기 위해 `VxeUI.setI18n`을 통해 전역 `$t` 함수를 주입합니다.
```typescript
vxeUI.setI18n((key, args) => $t(key, args));
vxeUI.setLanguage(preferences.app.locale );
```




# AI 번역 작업 가이드 (i18n Automation Guide)

이 문서는 AI 에이전트가 프론트엔드 코드의 텍스트를 번역 키로 전환하고, 필요한 번역 데이터를 DB에 자동으로 반영하기 위한 지침서입니다.

## 1. DB 정보 및 스키마 (Context)

### 테이블 구조
번역 데이터는 PostgreSQL의 `scom.i18n_resources` 테이블에 보관됩니다.

### DB 연결 정보 확인 방법
DB 연결 문자열은 다음 파일에서 확인할 수 있습니다.
- 파일 경로: `microservices/AuthServer/appsettings.Local.json`
- 주요 항목: `ConnectionStrings.jsinicore` (Host, Port, Database, Username, Password 확인)

## 2. 번역 작업 절차 (Workflow)

AI는 번역 요청을 받으면 다음 단계를 순차적으로 수행해야 합니다.

### 단계 1: 코드 분석 및 키 추출
1. 대상 파일에서 하드코딩된 텍스트를 식별합니다.
2. 기존 번역 키 컨벤션을 준수하여 새로운 키를 명명합니다.
   - 공통 요소: `common.xxxx`
   - UI 요소: `ui.[화면명].[요소명]`
   - 메시지: `ui.actionMessage.[상태]`

### 단계 2: 프론트엔드 코드 수정
- 식별된 텍스트를 `$t('key')` 또는 `{{ $t('key') }}` 형태로 교체합니다.

### 단계 3: SQL 생성 및 보관
- `docs/` 폴더 내에 작업 단위별 SQL 파일을 생성합니다 (예: `docs/[오늘날짜]i18n_feature_name.sql`).
- SQL에는 `ko-KR`과 `en-US` 데이터를 모두 포함하며, 중복 방지를 위해 `DELETE` 구문을 선행합니다.

### 단계 4: DB 직접 반영
- `psql` 명령어를 사용하여 원격 DB에 직접 쿼리를 실행합니다.
- 실행 예시: `PGPASSWORD=xxxx psql -h [Host] -p [Port] -U [User] -d [DB] -f docs/xxxx.sql`

## 3. AI 프롬프트 템플릿 (Instruction for next time)

다음은 번역 작업을 지시할 때 사용할 수 있는 효율적인 프롬프트 예시입니다.

> **프롬프트 예시:**
> "현재 [파일경로] 파일에 하드코딩된 텍스트들을 번역 키로 전환해줘. 
> 1. `docs/ai작성방법.md`에 기술된 지침을 준수할 것.
> 2. `microservices/AuthServer/appsettings.Local.json`에서 DB 정보를 확인하여 `scom.i18n_resources` 테이블에 한국어와 영어 데이터를 직접 INSERT 해줘.
