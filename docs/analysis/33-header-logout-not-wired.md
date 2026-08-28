# 헤더의 로그아웃 아이콘이 동작하지 않던 이유

> 질문: "header.vue line 393 은 로그아웃 아이콘이다. 클릭하면 확인창이 나오는데
> 그것으로 로그아웃을 시도하면 로그아웃이 되지 않는다. 이유는?"

작업일: 2026-08-28

---

## 1. 신호가 마지막 한 칸에서 사라졌다

```
header.vue:397   @click            → 확인창 열기
header.vue:114   onConfirm         → emit('logout')                ✅
layout.vue:570   @logout           → emit('logout')  (BasicLayout) ✅
basic.vue:292    <BasicLayout …>   → 받는 곳이 없다                 ❌
```

`BasicLayout` 은 `logout` 을 정식으로 `emit` 한다(`defineEmits` 에 선언돼 있다).
그런데 앱의 `<BasicLayout>` 에는 `@clear-preferences-and-logout` 과 `@click-logo` 만
걸려 있었다. 이벤트가 올라오지만 **듣는 쪽이 없어 그대로 사라진다.**

확인을 누르면 `logoutModalApi.close()` 로 **창만 닫히고** 끝난다 —
오류도 나지 않아서 "눌렀는데 아무 일도 없다" 로만 보인다.

## 2. 아바타 메뉴는 왜 됐나

같은 파일에서 `<UserDropdown @logout="handleLogout">` 로 **직접** 받고 있었다.
헤더 아이콘만 중간이 끊겨 있었다.

## 3. 그래서 로그아웃할 방법이 없는 상태였다

vben 은 로그아웃을 **헤더 아니면 아바타 메뉴 중 한 곳에만** 둔다.

| 자리 | 조건 |
|---|---|
| 헤더 아이콘 | `preferences.widget.logoutButtonPosition === 'header'` |
| 아바타 메뉴 항목 | `=== 'user-dropdown'` (`user-dropdown.vue:189`) |

지금 설정이 `header` 라서 **아바타 메뉴에는 로그아웃 항목이 아예 없고**(실제로 열어
확인했다 — 프로필 · 문서 · GitHub · Q&A 뿐), 헤더 것은 죽어 있었다.
세션 앞에서 나온 "아바타 메뉴에서 로그아웃만 사라졌다" 도 같은 뿌리다 —
사라진 것이 아니라 헤더로 옮겨갔고, 옮겨간 자리가 연결돼 있지 않았다.

## 4. 상위 동기화 탓이 아니다

`basic.vue` 의 지난 판을 전부 훑어 보니 `@logout="handleLogout"` 은 **언제나 한 개**였다
(아바타 메뉴 쪽). `<BasicLayout>` 에는 **한 번도 걸린 적이 없다.**
즉 되돌아간 것이 아니라 **처음부터 연결하지 않은 것**이다.

```bash
for c in $(git log --format=%h -12 -- fronts/apps/jsini-portal/src/layouts/basic.vue); do
  echo "$c $(git show $c:fronts/apps/jsini-portal/src/layouts/basic.vue | grep -c '@logout=')"
done
```

## 5. 고친 것

`basic.vue` 의 `<BasicLayout>` 에 한 줄을 더했다.

```diff
   <BasicLayout
     @clear-preferences-and-logout="handleLogout"
     @click-logo="handleClickLogo"
+    @logout="handleLogout"
   >
```

`handleLogout` 은 아바타 메뉴가 쓰던 것과 **같은 함수**라 두 자리의 동작이 정확히 같다.

## 6. 확인한 것

개발 서버에서 실제로 눌렀다.

| | 고치기 전 | 고친 뒤 |
|---|---|---|
| 확인창 | 뜸 | 뜸 |
| `POST /api/auth/logout` | **0건** | **200** |
| 경로 | `/workspace` 그대로 | `/workspace` → **`/auth/login`** |

`pnpm vite build` 통과 · eslint 새 오류 없음.
