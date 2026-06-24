# 구현 계획서 및 결과 보고서

- **작업명**: 조직도 화면 개선 (회사 직속 부서 이동 및 사용자 부서 귀속 제약 추가)
- **작업일시**: 2026-06-24 23:50
- **예상소요시간**: 15m
- **작성자**: Antigravity (Lead Engineer Agent)

---

## 1. Problem Summary (문제 요약)
- 부서를 드래그하여 다른 부서 하위로 이동하는 것 외에, 최상위 회사 노드(`COMPANY`)에 드롭하여 회사의 직속 부서(최상위 부서)가 될 수 있도록 로직 개선.
- 사용자는 오직 부서로만 이동이 가능해야 하며, 회사 노드에 드롭 시 경고 메시지와 함께 이동을 차단하도록 정책 강제 적용.

## 2. Design Summary (설계 요약)
- **목적**: 부서 계층 이동의 유연성 확대(회사 직속 부서로 승격) 및 사용자의 부서 외 귀속 차단.
- **입력**: 회사 노드 드롭 이벤트.
- **출력**: 
  - 부서 노드 드롭 시: `moveDept(id, undefined)`를 호출하여 상위 부서를 부재(회사 직속)로 변경 처리.
  - 사용자 노드 드롭 시: Ant-Design-Vue의 `message.warning` 노출 및 API 요청 차단.
- **주요 모듈**: `org-chart.vue` 의 회사 노드 마크업 바인딩, `onCompanyDrop` 함수.

## 3. Implementation Plan (구현 계획)
1. **회사 노드 드롭 바인딩**:
   - `COMPANY` 노드의 `div` 태그에 `@dragover="onDragOver"` 및 `@drop.stop="onCompanyDrop($event, node.id)"`를 구성.
2. **이벤트 핸들러 구현 (`onCompanyDrop`)**:
   - 전달 데이터가 `DRAG_TYPE_DEPT`일 때 `moveDept(id, undefined)` 수행 및 데이터 최신화.
   - 전달 데이터가 `DRAG_TYPE_USER`일 때 경고 메시지 방출 및 종료.

## 4. Code (코드 변경 사항)
[org-chart.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/system/company-user/org-chart.vue)
```diff
@@ -342,7 +342,32 @@
   }
 }
 
-
+async function onCompanyDrop(e: DragEvent, companyId: string) {
+  e.preventDefault();
+  if (!e.dataTransfer) return;
+
+  try {
+    const rawData = e.dataTransfer.getData('text/plain');
+    if (!rawData) return;
+
+    const dragData = JSON.parse(rawData);
+    const { type, id } = dragData;
+
+    // 부서 노드인 경우만 회사의 직속 부서로 이동시킴 (상위 부서 ID를 undefined로 전송)
+    if (type === DRAG_TYPE_DEPT) {
+      const success = await moveDept(id, undefined);
+      if (success) {
+        message.success('부서가 회사의 직속 부서로 이동되었습니다.');
+        await loadOrgData();
+      }
+    } else if (type === DRAG_TYPE_USER) {
+      message.warning('사용자는 부서로만 이동할 수 있습니다.');
+    }
+  } catch (error) {
+    console.error(error);
+    message.error('회사로 노드 이동 처리에 실패했습니다.');
+  }
+}
 
 // Bezier 곡선 연결선 패스 빌더
@@ -485,6 +485,8 @@
                 <!-- 회사 노드 -->
                 <div
                   v-if="node.type === 'COMPANY'"
+                  @dragover="onDragOver"
+                  @drop.stop="onCompanyDrop($event, node.id)"
                   class="node-card border-2 border-primary bg-blue-50/95 shadow-md rounded-lg p-2.5 flex items-center gap-2.5 h-[50px] w-[180px] hover:shadow-lg transition-shadow"
                 >
```

## 5. Testing (검증 방법)
- **회사 직속 부서 이동 테스트**:
  - 하위 부서를 회사 노드 위에 드래그 앤 드롭했을 때 성공 안내 팝업이 노출되며, 해당 부서가 회사의 직속(최상위) 부서로 갱신되어 최상위 레벨에 재배치되는지 검증.
- **사용자 이동 차단 테스트**:
  - 사용자 노드를 회사 노드 위에 드롭했을 때, "사용자는 부서로만 이동할 수 있습니다." 경고가 올바르게 작동하고 데이터가 변경되지 않는지 확인.

## 6. Behavior Summary (동작 요약)
- **회사로 부서 드롭**: 부서 카드 ➔ 회사 카드 위 드롭 ➔ `moveDept(id, undefined)` ➔ 최상위 직속 부서 전환 ➔ 재로드.
- **회사로 사용자 드롭**: 사용자 카드 ➔ 회사 카드 위 드롭 ➔ `message.warning` 경고 노출 ➔ 처리 중단.
