/**
 * 지시 상세 — 처리 요약 음성으로 읽어주기 (TTS, Web Speech API).
 *
 * 브라우저의 speechSynthesis 를 사용하여 요약 내용 전체(결론, 세부 항목, 확인할 것)를
 * 한국어로 문장/항목 단위로 순차 재생한다.
 *
 * [브라우저 이슈 대응]
 * 1. 크롬 등에서 긴 발화(약 15초 이상)를 하나의 Utterance 로 재생 시 발화가 중간에 강제 취소되는 현상:
 *    문장 및 항목별 청크(chunk)로 나누어 순차적으로 재생하여 제한을 우회한다.
 * 2. V8 가비지 컬렉터로 인해 재생 중인 Utterance 참조가 유실되어 멈추는 현상:
 *    전역/모듈 변수에 활성 Utterance 참조를 유지한다.
 * 3. pause/resume 호출 시 모바일 및 최신 브라우저에서 오디오가 영구 중단되는 현상:
 *    pause()를 임의로 부르지 않고, 브라우저가 비정상적으로 paused 상태일 때만 resume()을 호출하는 와치독 적용.
 */

let currentHelper = null;
let currentChunks = [];
let currentChunkIndex = 0;
let isSpeakingState = false;
let isCanceled = false;
let activeUtterance = null;
let chunkTimeoutTimer = null;
let watchdogTimer = null;
let cachedVoice = null;

function clearChunkTimeout() {
  if (chunkTimeoutTimer) {
    clearTimeout(chunkTimeoutTimer);
    chunkTimeoutTimer = null;
  }
}

function clearWatchdog() {
  if (watchdogTimer) {
    clearInterval(watchdogTimer);
    watchdogTimer = null;
  }
}

function startWatchdog() {
  clearWatchdog();
  // 브라우저가 백그라운드 전환 등으로 일시정지 상태(paused)에 빠졌을 때만 깨운다. (pause()는 절대 호출하지 않음)
  watchdogTimer = setInterval(() => {
    if (typeof window !== 'undefined' && 'speechSynthesis' in window && isSpeakingState) {
      if (window.speechSynthesis.paused) {
        try {
          window.speechSynthesis.resume();
        } catch {
        }
      }
    } else {
      clearWatchdog();
    }
  }, 1000);
}

function getKoreanVoice() {
  if (cachedVoice) {
    return cachedVoice;
  }
  if (typeof window === 'undefined' || !('speechSynthesis' in window)) {
    return null;
  }
  try {
    const voices = window.speechSynthesis.getVoices();
    if (voices && voices.length > 0) {
      cachedVoice = voices.find(v => v.lang === 'ko-KR' || v.lang === 'ko_KR')
                 || voices.find(v => v.lang.startsWith('ko'))
                 || null;
    }
  } catch {
    cachedVoice = null;
  }
  return cachedVoice;
}

if (typeof window !== 'undefined' && 'speechSynthesis' in window && window.speechSynthesis.onvoiceschanged !== undefined) {
  window.speechSynthesis.onvoiceschanged = () => {
    cachedVoice = null;
    getKoreanVoice();
  };
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

function cleanup() {
  clearChunkTimeout();
  clearWatchdog();
  isSpeakingState = false;
  activeUtterance = null;
  if (typeof window !== 'undefined') {
    window.__currentSpeechUtterance = null;
  }
  currentChunks = [];
  currentChunkIndex = 0;
}

function splitIntoChunks(input) {
  if (!input) return [];

  const rawLines = Array.isArray(input)
    ? input
    : String(input).split(/\r?\n+/);

  const chunks = [];

  for (const raw of rawLines) {
    if (!raw) continue;
    const line = String(raw).trim();
    if (!line) continue;

    // 길이가 짧은 문장(100자 이하)은 그대로 한 청크로 사용
    if (line.length <= 100) {
      chunks.push(line);
      continue;
    }

    // 긴 줄은 마침표, 물음표, 느낌표 등 문장 단위로 분할
    const sentences = line.match(/[^.!?]+[.!?]+|[^.!?]+$/g) || [line];
    for (const s of sentences) {
      const trimmed = s.trim();
      if (!trimmed) continue;

      if (trimmed.length <= 100) {
        chunks.push(trimmed);
      } else {
        // 문장 자체가 너무 길면 쉼표 단위로 추가 분할
        const parts = trimmed.split(/,\s*/);
        let cur = '';
        for (const p of parts) {
          if ((cur + ', ' + p).length <= 100) {
            cur = cur ? cur + ', ' + p : p;
          } else {
            if (cur) chunks.push(cur);
            cur = p;
          }
        }
        if (cur) chunks.push(cur);
      }
    }
  }

  return chunks;
}

function speakNextChunk() {
  clearChunkTimeout();

  if (isCanceled || !isSpeakingState) {
    cleanup();
    return;
  }

  if (currentChunkIndex >= currentChunks.length) {
    // 요약의 모든 항목을 다 읽었음
    cleanup();
    notifyState(false);
    return;
  }

  const chunkText = currentChunks[currentChunkIndex];
  const utterance = new SpeechSynthesisUtterance(chunkText);
  utterance.lang = 'ko-KR';
  utterance.rate = 1.0;
  utterance.pitch = 1.0;

  const voice = getKoreanVoice();
  if (voice) {
    utterance.voice = voice;
  }

  // V8 GC 방지를 위해 참조 유지
  activeUtterance = utterance;
  if (typeof window !== 'undefined') {
    window.__currentSpeechUtterance = utterance;
  }

  let chunkFinished = false;
  const onFinish = () => {
    if (chunkFinished) return;
    chunkFinished = true;
    clearChunkTimeout();

    if (isCanceled || !isSpeakingState) {
      return;
    }

    currentChunkIndex++;
    // 다음 청크로 넘어가기 전 자연스러운 쉼(50ms)
    setTimeout(() => {
      if (isSpeakingState && !isCanceled) {
        speakNextChunk();
      }
    }, 50);
  };

  utterance.onend = () => {
    onFinish();
  };

  utterance.onerror = (e) => {
    // 사용자가 취소한 경우 (stop() 호출 시 브라우저가 e.error === 'canceled' 또는 'interrupted' 반환)
    if (isCanceled || !isSpeakingState || e.error === 'canceled' || e.error === 'interrupted') {
      cleanup();
      notifyState(false);
      return;
    }
    // 특정 단어나 음성 버그로 개별 청크 오류가 발생해도 요약 전체를 멈추지 않고 다음 청크로 이어감
    console.warn('Speech chunk error, skipping to next:', e);
    onFinish();
  };

  // 브라우저 버그로 onend 가 트리거되지 않을 때를 대비한 안전 타임아웃
  // 한국어 보통 발화 속도(초당 약 5~7자)를 감안하여 넉넉히 설정 (최소 8초)
  const timeoutMs = Math.max(8000, chunkText.length * 400);
  chunkTimeoutTimer = setTimeout(() => {
    if (isSpeakingState && !isCanceled && !chunkFinished) {
      console.warn('Speech chunk timed out, skipping to next');
      onFinish();
    }
  }, timeoutMs);

  try {
    window.speechSynthesis.speak(utterance);
  } catch (err) {
    console.error('Failed to speak chunk:', err);
    onFinish();
  }
}

export function isSupported() {
  return typeof window !== 'undefined' && 'speechSynthesis' in window && typeof SpeechSynthesisUtterance !== 'undefined';
}

export function isSpeaking() {
  return isSpeakingState;
}

export function speak(input, dotNetHelper) {
  if (!isSupported()) {
    return false;
  }

  stop();

  const chunks = splitIntoChunks(input);
  if (chunks.length === 0) {
    return false;
  }

  currentHelper = dotNetHelper;
  currentChunks = chunks;
  currentChunkIndex = 0;
  isCanceled = false;
  isSpeakingState = true;

  notifyState(true);
  startWatchdog();

  // cancel() 호출 후 오디오 엔진이 초기화될 시간을 약간 부여
  setTimeout(() => {
    if (isSpeakingState && !isCanceled) {
      speakNextChunk();
    }
  }, 50);

  return true;
}

export function stop() {
  isCanceled = true;
  cleanup();
  if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
    try {
      window.speechSynthesis.cancel();
    } catch {
    }
  }
  notifyState(false);
}

if (typeof window !== 'undefined') {
  window.addEventListener('beforeunload', () => {
    stop();
  });
}
