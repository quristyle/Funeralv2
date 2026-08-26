# 역할 없는 계정이 관리자로 취급되던 것

> 지시: "역할이 하나도 없는 계정은 관리자로 취급되는 것을 개선해줘.
> 프론트 가드에 해당 방침이 있다면 제거하라."

작업일: 2026-08-26

---

## 1. 무엇이 문제였나

권한을 **하나도 주지 않은 계정이 가장 센 권한을 갖고 있었다.** 방향이 거꾸로였다.

`MenuService.GetEffectivePermissionAsync` 가 이렇게 되어 있었다.

```csharp
// 정보가 아예 없으면 전부 허용으로 본다.
if (all.Count == 0) return AllowAll(path);
```

`AllowAll` 은 열람·등록·수정·삭제·출력·엑셀·사용자정의 1~8 을 모두 켠 값이다.
그래서 역할 미배정 계정이 F.A.Q 를 쓰고 Q&A 를 공개하고 자료실에 파일을 올릴 수 있었다.

같은 방침이 프론트 네 곳에도 있었다.

| 파일 | 있던 조건 |
|---|---|
| `router/guard.ts` | `!isLoaded \|\| !hasAnyData` → 통과 |
| `composables/use-menu-permission.ts` | 같은 조건 → `ALLOW_ALL` |
| `directives/perm.ts` | 같은 조건 → 버튼 그대로 노출 |
| `utils/permission.ts` (`can()`) | 같은 조건 → `true` |

---

## 2. 무엇을 고쳤나

### 2.1 "못 받았다" 와 "받았더니 없다" 를 구분했다

이 둘이 한 조건으로 묶여 있던 것이 문제의 뿌리였다.

- **못 받았다** (`!isLoaded`) — 아직 도착 안 했거나 권한 서비스가 죽었다.
  판단할 근거가 없다. 여기서 막으면 서비스 장애가 곧 전원 잠금이 된다.
  → 그대로 통과시킨다. **권한 판단이 아니라 깜빡임·잠금 방지다.**
- **받았더니 없다** (`hasAnyData === false`) — 서버가 "권한 없다" 고 답했다.
  → 그대로 '권한 없음' 이다. **이 갈래를 없앤 것이 이번 수정이다.**

### 2.2 백엔드

`GetEffectivePermissionAsync` 의 `AllowAll` 분기를 지웠다. 권한이 없으면
모든 항목이 꺼진 `MenuPermissionDto` 를 돌려준다.

**`AllowAll()` 함수 자체도 지웠다.** 이 방침을 만들던 유일한 자리였고
남겨 두면 다음 사람이 같은 실수를 하기 쉽다.

조회 실패는 예외로 올라가므로 빈 목록과 섞이지 않는다 — 빈 목록은 "못 읽었다" 가
아니라 "읽었더니 없다" 다.

### 2.3 프론트

네 곳 모두 `|| !hasAnyData` 를 뺐다. `!isLoaded` 만 남겼고, 그것이 권한 판단이
아니라는 것을 주석에 적었다.

`hasAnyData` 자체는 스토어에 남겼다 — 권한 예시 화면(`/system/perm-sample`)이
"이 계정은 역할이 없어서 아무것도 못 한다" 를 **사람에게 알려 주는 데** 쓴다.
그 화면의 안내 문구도 "전부 허용으로 동작합니다" → "모든 권한이 없습니다" 로 바꿨다.

---

## 3. 영향 범위 — 잠기지 않는다

고치기 전에 재어 봤다. **역할이 없는 활성 계정은 2개**(전체 43개 중)다.

```
admin           미르작은사장님
administrator   미르
```

둘 다 화면을 **돌아다니는 데는 문제가 없다.** 두 가지 이유다.

1. 사이드바·라우트는 `/auth/menu/all` 로 만들고 **그 API 는 역할을 보지 않는다**
   (`GetAllMenusAsync` 는 활성 메뉴 전체를 준다).
2. 열람 가드는 `findExact(path)` 가 있을 때만 막는다. 권한 목록이 비면
   항상 `undefined` 라 그냥 통과한다.

달라지는 것은 **버튼과 쓰기**다. 등록·수정·삭제 버튼이 숨고, 서버 쓰기는 403 이 된다.
이름이 `administrator` 인 계정이 아무것도 못 하게 되는 것이 이상하게 보일 수 있지만,
**역할이 배정되지 않은 것이 사실**이므로 이제 그 사실이 드러나는 것이다.

---

## 4. 확인한 것

격리된 AuthServer(:15267)로 확인했다. 개발자가 띄워 둔 :5264 · :5265 는 건드리지 않았다.

| 계정 | 역할 | 자료실 canManage | F.A.Q canManage |
|---|---|---|---|
| administrator | (없음) | **false** | **false** |
| admin | (없음) | **false** | **false** |
| vben | ADMINISTRATOR | true | true |
| uspuni | PARTNER | false | false |

- 역할 없는 계정의 실제 쓰기(`POST /help/archives`) → **403 FORBIDDEN**
- 화면(:5555)에서 역할 없는 계정으로 `/help/faq` 이동 → 403 으로 막히지 않음(의도대로)
- `/system/perm-sample` → "역할이 배정되지 않아 모든 권한이 없습니다" 로 바뀐 것 확인

`dotnet build` · `pnpm vite build` 통과.

---

## 5. 남은 것 🟡

- **백엔드를 다시 띄워야 서버 쪽 차단이 켜진다.** 실행 중인 AuthServer(:5264)는
  이 변경 전 빌드다. 그전까지 `canManage` 는 계속 true 로 온다.
- **`admin` · `administrator` 에 역할을 배정할지는 판단이 필요하다.**
  이름을 보면 관리자로 쓰던 계정 같지만, 관리자 권한을 주는 일은 사람이 정할 몫이라
  건드리지 않았다. 주려면 한 줄이다.

  ```sql
  INSERT INTO scom.role_accounts (id, role_id, account_id, created_at, is_deleted)
  SELECT gen_random_uuid()::text, 'ADMINISTRATOR', a.id, now(), false
  FROM scom.accounts a
  WHERE a.user_id IN ('admin', 'administrator')
    AND NOT EXISTS (SELECT 1 FROM scom.role_accounts ra WHERE ra.account_id = a.id);
  ```

- `!isLoaded` 는 여전히 통과시킨다. 권한 서비스가 오래 죽어 있으면 버튼이 열려 보이지만,
  **서버가 막으므로 구멍은 아니다**(눌러도 403). 화면을 잠그는 쪽이 더 위험해서 이렇게 뒀다.
