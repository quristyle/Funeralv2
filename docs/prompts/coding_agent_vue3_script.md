## Vue 3 Script Rules (Non-TypeScript)

### Script Setup
- 모든 컴포넌트는 `<script setup>` 사용
- Options API 사용 금지
- 암묵적 전역 변수 사용 금지

---

### Props Rules
- props 는 반드시 구조 정의 기반으로 선언

const props = defineProps({
  title: {
    type: String,
    required: true
  },
  count: {
    type: Number,
    default: 0
  }
})

- props 직접 수정 금지

❌ props.title = 'new'

---

### Emits Rules
- emit 이벤트는 명시적으로 선언

const emit = defineEmits(['save'])

---

### Null Safety
- 모든 Template Ref 는 null 가능성 고려

const el = ref(null)

사용 시 반드시 null 체크

❌ el.value.focus()

✔️
if (el.value) {
  el.value.focus()
}

---

### Reactive Access Safety
- 배열 / 객체 접근 시 항상 안전 접근

❌ items[0].name

✔️ items[0]?.name

---

### Forbidden Patterns
- 암묵적 any 역할을 하는 데이터 구조 금지
- ref 초기값 없이 선언 금지
- props 직접 수정 금지

---

### Event Handling
- DOM 이벤트 객체 존재 가정 금지

❌
const onClick = (e) => {
  e.target.value
}

✔️
const onClick = (e) => {
  if (!e) return
  const target = e.target
}

---

### Template Ref Rules
const inputRef = ref(null)

사용 시 null 체크 필수

---

### Watch Rules
watch 대상은 명확한 source 사용

❌ watch(props)

✔️ watch(() => props.id)

---

### Composition API Rules
- 비즈니스 로직은 composable 로 분리
- setup 내부 직접 async 실행 금지

❌
await loadData()

✔️
onMounted(() => {
  loadData()
})

---

### Provide / Inject Rules
문자열 키 대신 Symbol 사용 권장

const UserKey = Symbol('User')

---

### Safe Access Rules
- 모든 객체 접근 시 존재 여부 확인

❌ user.profile.name

✔️ user?.profile?.name

---

### State Mutation Rules
- reactive 객체는 구조 유지

❌ state = {}

✔️ state.name = 'new'

---

## Quality Gate

다음 조건을 만족해야 완료로 간주

- 런타임 null 오류 없음
- props mutation 없음
- setup 내부 직접 async 없음
- watch 대상 명확성 확보




---
> 📌 **These rules are guidelines and should be applied flexibly according to the situation. However, security and error handling must be followed without exception.**