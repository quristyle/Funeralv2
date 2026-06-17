## TypeScript Rules
- strict: true
- strictNullChecks: true
- noImplicitAny: true
- noUncheckedIndexedAccess: true

## Forbidden Patterns
- no `any`
- no non-null assertion (`!`)
- no type suppression

## Safe Access Rules
- Always guard array/object access

## Event Handling
- Explicit DOM event types required

## Vue 3 Rules
- Typed refs with null guards

## Quality Gate
- Code must pass `tsc --noEmit`




---
> 📌 **These rules are guidelines and should be applied flexibly according to the situation. However, security and error handling must be followed without exception.**