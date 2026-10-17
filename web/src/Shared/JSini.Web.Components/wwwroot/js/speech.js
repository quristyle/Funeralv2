/**
 * 읽어주기 (TTS, Web Speech API) — **공용 부품이다.**
 *
 * 브라우저의 speechSynthesis 를 사용하여 한국어 글을 문장/항목 단위로 순차 재생한다.
 *
 * [쓰는 자리 둘]
 *
 * 1. 프로젝트관리 지시 상세 — 처리 요약을 통째로 읽는다 (`speak`).
 *    글이 다 있는 채로 시작하는 자리다.
 * 2. AI쳇 음성 모드 — 답이 오는 대로 문장씩 읽는다
 *    (`beginStream` / `pushStream` / `endStream`).
 *    **답이 다 오기를 기다리면 안 되는 자리다** — 긴 답을 끝까지 기다리면
 *    사람은 몇 초 동안 아무 소리도 못 듣고, 음성 대화의 값이 사라진다.
 *
 * 원래 ProjMng 안에 있던 것을 올렸다. 아래 브라우저 버그 대응이 한 벌뿐이어야
 * 해서다 — 복사해 두면 한쪽에만 고침이 들어가고, 그 어긋남은 「지시 상세에서는
 * 되는데 음성 모드에서는 중간에 끊긴다」로 나타난다.
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

/**
 * 읽을 글이 **아직 더 올 수 있나.**
 *
 * 음성 모드에서 참이 된다. 이 값이 참인 동안에는 큐가 비어도 "다 읽었다" 로
 * 치지 않는다 — 다음 문장이 오는 중일 뿐이다. 거짓으로 뒀다가는 문장 사이마다
 * 화면이 「말하는 중 → 끝남 → 말하는 중」으로 깜빡인다.
 */
let streamOpen = false;

/** 큐가 비어 멈춰 서 있나. 다음 글이 오면 여기서 다시 굴린다. */
let awaitingMore = false;

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
  streamOpen = false;
  awaitingMore = false;
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
    // **아직 더 올 수 있으면 멈춰 서서 기다린다.** 여기서 끝냈다가는
    // 문장 사이마다 "다 읽었다" 가 화면으로 올라가 상태가 깜빡인다.
    if (streamOpen) {
      awaitingMore = true;
      return;
    }

    // 읽을 것을 다 읽었음
    cleanup();
    notifyState(false);
    return;
  }

  awaitingMore = false;
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

/**
 * 흘려 읽기를 연다 — AI쳇 음성 모드가 쓴다.
 *
 * 이 뒤로 `pushStream` 으로 문장을 밀어 넣으면 **오는 대로 읽는다.**
 * 다 밀어 넣었으면 `endStream` 으로 닫는다.
 *
 * `speak` 와 갈라 둔 까닭: `speak` 는 글이 다 있는 채로 시작해서 큐가 비면
 * 끝나는 물건이다. 스트리밍에서는 큐가 비는 일이 **읽기가 끝난 것이 아니라
 * 다음 문장을 기다리는 것**이라, 그 둘을 한 함수에 담으면 조건이 엉킨다.
 */
export function beginStream(dotNetHelper) {
  if (!isSupported()) {
    return false;
  }

  stop();

  currentHelper = dotNetHelper;
  currentChunks = [];
  currentChunkIndex = 0;
  isCanceled = false;
  isSpeakingState = true;
  streamOpen = true;
  awaitingMore = true;

  notifyState(true);
  startWatchdog();
  return true;
}

/**
 * 읽을 글을 뒤에 붙인다. 멈춰 서 있었으면 다시 굴린다.
 *
 * **문장 단위로 넣는 것은 부르는 쪽의 몫이다.** 글자가 오는 대로 한 자씩
 * 넣으면 한 글자짜리 발화가 줄줄이 생겨서 말이 끊어져 들린다 — C# 쪽이
 * 문장 끝(`.!?` · 줄바꿈)까지 모았다가 한 번에 넣는다.
 */
export function pushStream(text) {
  if (!streamOpen || isCanceled || !text) {
    return false;
  }

  const chunks = splitIntoChunks(text);
  if (chunks.length === 0) {
    return false;
  }

  for (const c of chunks) {
    currentChunks.push(c);
  }

  // 멈춰 서 있었으면 다시 굴린다. 읽는 중이면 `onFinish` 가 알아서 집어 간다.
  if (awaitingMore) {
    awaitingMore = false;
    speakNextChunk();
  }

  return true;
}

/**
 * 더 올 글이 없다고 알린다. **남은 큐는 끝까지 읽는다.**
 *
 * 이미 큐가 비어 멈춰 서 있었으면 그 자리에서 끝난다.
 */
export function endStream() {
  if (!streamOpen) {
    return;
  }

  streamOpen = false;

  if (awaitingMore) {
    cleanup();
    notifyState(false);
  }
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
