export { default as AiChatButton } from './ai-chat-button.vue';
// 채팅창을 여닫는 스위치. 헤더의 버튼이 켜고, 레이아웃이 보고 그리며,
// 채팅창 안의 닫기 버튼이 끈다. **채팅창 본체는 여기 없다** —
// 그것은 `ant-design-vue` 를 쓰므로 앱(`apps/jsini-portal/src/components/ai-chat`)에 있고
// 레이아웃의 `#ai-chat` 슬롯으로 들어온다.
export { isAiChatPinned } from './state';
