# 구현 계획서: 장비 속성 멀티미디어(동영상/음악) 선택 기능 추가

- **작성일시**: 2026-06-30 12:05
- **예상 소요 시간**: 40분

---

## 1. 문제 요약
장비 속성 관리 화면(`device-attribute-tab.vue`)에서 동영상 및 음악 재생 활성화 시, 구체적으로 어떤 미디어 콘텐츠를 재생할지 선택할 수 있는 `BizSelect` 컴포넌트를 추가합니다. 이를 위해 DB에 메타데이터 설정을 등록하고 백엔드의 엔티티, DTO, 서비스 단에 해당 속성(`VideoId`, `MusicId`)을 반영합니다.

---

## 2. 디자인 요약
- **UI/UX**:
  - `isVideoEnabled` (동영상 재생)가 활성화되면 동영상 선택 `BizSelect`가 나타납니다.
  - `isMusicEnabled` (음악 재생)가 활성화되면 음악 선택 `BizSelect`가 나타납니다.
  - `BizSelect`의 `type` 속성은 각각 `video`와 `music`으로 매핑됩니다.
- **API 및 데이터**:
  - `biz_select_configs` 테이블에 `video` 및 `music` 타입의 메타데이터 설정을 추가합니다.
    - `video`: `/funeral/building/source/list?type=VIDEO`
    - `music`: `/funeral/building/source/list?type=AUDIO`
  - `device_attributes` 테이블 및 백엔드 `DeviceAttribute` 모델에 `video_id` 및 `music_id` 속성을 추가하여 선택된 미디어를 저장합니다.

---

## 3. 구현 계획
- **Step 1: DB 메타데이터 등록 및 스키마 수정 SQL 작성**
  - `docs/insert_metadata.sql` 생성
  - `device_attributes` 테이블에 `video_id`, `music_id` 컬럼 추가 DDL 작성.
  - `biz_select_configs`에 `video`, `music` 설정 INSERT 문 작성.
  - `media_sources`에 예시 VIDEO, AUDIO 레코드 INSERT 문 작성.
- **Step 2: 백엔드 모델 및 DTO 반영**
  - `DeviceAttribute.cs` 엔티티에 `VideoId`, `MusicId` 컬럼(속성) 추가.
  - `DeviceAttributeDtos.cs`의 `DeviceAttributeDto` 및 `DeviceAttributeUpsertDto`에 `VideoId`, `MusicId` 필드 추가.
- **Step 3: 백엔드 서비스 반영**
  - `DeviceAttributeService.cs`에서 Upsert 시 `VideoId`, `MusicId` 값을 바인딩하고 DTO 매핑 시에도 처리하도록 구현.
- **Step 4: 프론트엔드 API 및 Composable 수정**
  - `building/index.ts`의 `DeviceAttribute` 인터페이스에 `videoId`, `musicId` 프로퍼티 추가.
  - `use-device-attribute.ts` Composable의 `defaultAttr` 및 `handleAttrSave` payload에 `videoId`, `musicId` 매핑 추가.
- **Step 5: 프론트엔드 UI 수정**
  - `device-attribute-tab.vue`에 `BizSelect` 컴포넌트 import 추가.
  - 멀티미디어 재생 설정 영역 하위에 `BizSelect` 기반 동영상 및 음악 선택 항목 추가.

---

## 4. 예외 처리 및 안정성 고려
- **Null Safety**:
  - `videoId`와 `musicId`는 선택되지 않았을 경우 `null` 값을 가질 수 있도록 데이터베이스 및 DTO에서 `Nullable (string?)`로 설정합니다.
  - 프론트엔드 API 타입 선언 시에도 `string | null`로 지정하여 엄격한 strict null 체크를 통과하게 합니다.
- **Fallback**:
  - DB에 `biz_select_configs` 설정이 누락되더라도 화면 로딩이 멈추지 않도록 `BizSelect.vue` 내부의 오류 처리 흐름을 확인하며 구현합니다.
