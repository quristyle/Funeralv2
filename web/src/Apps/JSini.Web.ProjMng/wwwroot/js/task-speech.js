/**
 * 지시 상세 — 처리 요약 음성으로 읽어주기 (TTS, Web Speech API).
 *
 * 브라우저의 speechSynthesis 를 사용하여 요약 텍스트를 한국어로 읽어준다.
 * 재생/중지 상태를 Blazor 컴포넌트로 전달한다.
 */

let currentUtterance = null;
let currentHelper = null;
let resumeTimer = null;

function clearKeepAlive() {
  if (resumeTimer) {
    clearInterval(resumeTimer);
    resumeTimer = null;
  }
}

function startKeepAlive() {
  clearKeepAlive();
  // Chrome 브라우저에서 긴 문장 발화 시 일정 시간(약 15초) 후 일시중지되는 문제 방지
  resumeTimer = setInterval(() => {
    if (typeof window !== 'undefined' && 'speechSynthesis' in window && window.speechSynthesis.speaking) {
      window.speechSynthesis.pause();
      window.speechSynthesis.resume();
    } else {
      clearKeepAlive();
    }
  }, 10000);
}

function notifyState(speaking) {
  if (currentHelper) {
    try {
      currentHelper.invokeMethodAsync('OnSpeechStateChanged', speaking);
    } catch {
      // 컴포넌트가 이미 파기되었거나 연결이 닫힌 경우
    }
  }
}

export function isSupported() {
  return typeof window !== 'undefined' && 'speechSynthesis' in window && typeof SpeechSynthesisUtterance !== 'undefined';
}

export function isSpeaking() {
  return typeof window !== 'undefined' && 'speechSynthesis' in window && window.speechSynthesis.speaking;
}

export function speak(text, dotNetHelper) {
  if (!isSupported()) {
    return false;
  }

  stop();

  if (!text || !text.trim()) {
    return false;
  }

  currentHelper = dotNetHelper;

  const utterance = new SpeechSynthesisUtterance(text.trim());
  utterance.lang = 'ko-KR';
  utterance.rate = 1.0;
  utterance.pitch = 1.0;

  try {
    const voices = window.speechSynthesis.getVoices();
    const koVoice = voices.find(v => v.lang === 'ko-KR' || v.lang.startsWith('ko'));
    if (koVoice) {
      utterance.voice = koVoice;
    }
  } catch {
    // 음성 목록 조회가 실패해도 기본 한국어 설정으로 발화
  }

  utterance.onend = () => {
    clearKeepAlive();
    currentUtterance = null;
    notifyState(false);
  };

  utterance.onerror = (e) => {
    clearKeepAlive();
    currentUtterance = null;
    notifyState(false);
  };

  currentUtterance = utterance;
  startKeepAlive();
  window.speechSynthesis.speak(utterance);
  notifyState(true);
  return true;
}

export function stop() {
  clearKeepAlive();
  if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
    window.speechSynthesis.cancel();
  }
  currentUtterance = null;
  notifyState(false);
}
