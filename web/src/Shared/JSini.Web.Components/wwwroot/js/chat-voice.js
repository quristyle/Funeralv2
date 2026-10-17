/**
 * AI쳇 — 말소리 녹음 (받아쓰기·음성 모드 공용).
 *
 * 마이크를 열어 한 마디를 녹음하고, 그 바이트를 base64 로 돌려준다.
 * 글로 바꾸는 일은 **서버가 한다** — 까닭은 `deploy/stt/README.md` 에 있다
 * (브라우저 받아쓰기는 아이폰에 없고, 말소리가 바깥으로 나간다).
 *
 * [왜 회로로 넘기나]
 *
 * 브라우저가 게이트웨이를 직접 부르는 편이 짧다. 그런데 게이트웨이의 CORS 는
 * 옛 포털 주소 하나만 열려 있어 개발에서 깨지고, 브라우저에는 토큰도 없다.
 * 회로의 수신 한도는 이미 4MB 라(`JSiniWebApp.cs`) 한 마디(수십 KB)는 넉넉히
 * 지나간다. 그래서 base64 로 C# 에 넘기고 **서버가 게이트웨이에 붙는다.**
 *
 * [소리 크기는 회로로 보내지 않는다]
 *
 * 말하는 동안 오브가 맥박치려면 소리 크기가 계속 필요하다. 그것을 C# 으로
 * 올리면 초당 수십 번의 회로 왕복이 생긴다 — 사람 하나가 말하는 동안
 * 서버가 그만큼 깨어난다. 그래서 **JS 가 CSS 변수에 직접 적는다.**
 * 화면은 그 변수만 보고 커졌다 작아진다.
 */

/** 녹음이 이보다 길어지면 스스로 끊는다. 서버 상한(10MB)에 닿기 한참 전이다. */
const MAX_MS = 120000;

/** 이보다 작은 소리는 말이 아닌 것으로 친다 (RMS, 0~1). */
const SILENCE_RMS = 0.012;

/** 말이 끊긴 뒤 이만큼 더 조용하면 한 마디가 끝난 것으로 친다. */
const SILENCE_HOLD_MS = 1300;

/**
 * 말을 꺼내기 전에 기다려 주는 시간. **이것이 없으면 입을 떼기도 전에 끊긴다** —
 * 마이크를 열자마자 조용한 것은 당연하기 때문이다.
 */
const LEAD_IN_MS = 2500;

let stream = null;
let recorder = null;
let chunks = [];
let helper = null;

let audioCtx = null;
let analyser = null;
let levelTimer = null;
let levelTarget = null;

let startedAt = 0;
let lastLoudAt = 0;
let vadOn = false;
let autoStopped = false;

/** 녹음이 끝나면 풀리는 약속. `stop()` 이 기다린다. */
let stopPromise = null;
let resolveStop = null;

export function isSupported() {
  return typeof window !== 'undefined'
    && typeof MediaRecorder !== 'undefined'
    && !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
}

/**
 * 브라우저가 낼 수 있는 녹음 종류를 고른다.
 *
 * 크롬·파이어폭스는 webm/opus, **사파리는 mp4/aac** 를 준다. 어느 쪽이든
 * 받아쓰기 서버가 ffmpeg 로 풀어 주므로 여기서는 되는 것을 쓰면 된다.
 * 빈 문자열은 "브라우저가 알아서" 라는 뜻이다.
 */
function pickMime() {
  const want = [
    'audio/webm;codecs=opus',
    'audio/webm',
    'audio/mp4',
    'audio/ogg;codecs=opus',
  ];

  for (const m of want) {
    try {
      if (MediaRecorder.isTypeSupported(m)) return m;
    } catch (e) {
      /* isTypeSupported 자체가 없는 브라우저 */
    }
  }
  return '';
}

function extFor(mime) {
  if (!mime) return 'webm';
  if (mime.indexOf('mp4') >= 0) return 'mp4';
  if (mime.indexOf('ogg') >= 0) return 'ogg';
  return 'webm';
}

/** 소리 크기를 재서 CSS 변수에 적고, 조용해지면 한 마디가 끝났다고 알린다. */
function startMetering() {
  stopMetering();

  try {
    const Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return;

    audioCtx = new Ctx();
    const src = audioCtx.createMediaStreamSource(stream);
    analyser = audioCtx.createAnalyser();
    analyser.fftSize = 512;
    src.connect(analyser);

    const buf = new Uint8Array(analyser.fftSize);

    levelTimer = setInterval(() => {
      if (!analyser) return;

      analyser.getByteTimeDomainData(buf);

      let sum = 0;
      for (let i = 0; i < buf.length; i++) {
        const v = (buf[i] - 128) / 128;
        sum += v * v;
      }
      const rms = Math.sqrt(sum / buf.length);

      // 화면에 직접 적는다. 회로를 거치지 않는다.
      if (levelTarget) {
        // 말소리의 RMS 는 0.2 를 잘 안 넘어서 그대로 쓰면 오브가 거의 안 움직인다.
        const shown = Math.min(1, rms * 5);
        levelTarget.style.setProperty('--jsini-voice-level', shown.toFixed(3));
      }

      const now = Date.now();
      if (rms >= SILENCE_RMS) {
        lastLoudAt = now;
      }

      if (now - startedAt > MAX_MS) {
        autoStop('max');
        return;
      }

      // 말이 끊긴 뒤 한참 조용하면 한 마디가 끝난 것이다.
      // **입을 떼기 전(LEAD_IN_MS)에는 세지 않는다.**
      if (vadOn
        && now - startedAt > LEAD_IN_MS
        && now - lastLoudAt > SILENCE_HOLD_MS) {
        autoStop('silence');
      }
    }, 100);
  } catch (e) {
    // 소리 크기를 못 재도 녹음은 된다. 오브가 안 움직이고 자동 종료가 안 될 뿐이다.
  }
}

function stopMetering() {
  if (levelTimer) {
    clearInterval(levelTimer);
    levelTimer = null;
  }
  if (levelTarget) {
    levelTarget.style.setProperty('--jsini-voice-level', '0');
  }
  analyser = null;
  if (audioCtx) {
    try { audioCtx.close(); } catch (e) { /* 이미 닫혔다 */ }
    audioCtx = null;
  }
}

function autoStop(why) {
  if (autoStopped || !recorder || recorder.state !== 'recording') return;
  autoStopped = true;

  try {
    recorder.stop();
  } catch (e) {
    /* 이미 멎었다 */
  }

  // C# 에게 알린다 — 화면이 "듣는 중" 을 거두고 받아쓰기로 넘어간다.
  if (helper) {
    try {
      helper.invokeMethodAsync('OnVoiceAutoStop', why);
    } catch (e) {
      /* 회로가 닫혔다 */
    }
  }
}

/**
 * 소리 크기를 적어 넣을 칸을 일러 준다.
 *
 * **`start` 와 갈라 두었다.** 오브는 음성 모드를 열어야 생기는데, C# 쪽에서
 * 「열었다」와 「그려졌다」는 같은 때가 아니다 — `StateHasChanged()` 는 그
 * 자리에서 그리지 않는다. 한 함수에 묶으면 **아직 없는 칸**을 넘기게 된다.
 * 그래서 칸을 넘기는 일은 그려진 뒤(`OnAfterRenderAsync`)에 따로 한다.
 */
export function setMeterTarget(el) {
  levelTarget = el || null;
}

/**
 * 마이크를 열고 녹음을 시작한다.
 *
 * @param dotNetHelper 자동 종료를 알려 줄 C# 쪽 참조. 없어도 된다.
 * @param autoEnd 조용해지면 스스로 끊을지(음성 모드). 거짓이면 사람이 끊는다.
 * @returns 열었나. 거짓이면 권한이 없거나 마이크가 없다.
 */
export async function start(dotNetHelper, autoEnd) {
  if (!isSupported()) return false;

  await discard();

  helper = dotNetHelper || null;
  vadOn = !!autoEnd;
  autoStopped = false;
  chunks = [];

  try {
    stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        // **울림 지우기를 켠다.** 읽어주기 소리가 다시 마이크로 들어오면
        // AI 가 제 말을 받아쓰게 된다.
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    });
  } catch (e) {
    // 권한을 거절했거나, 마이크가 없거나, 보안 맥락(HTTPS)이 아니다.
    // **적어 둔 칸은 그대로 둔다** — 허락을 받고 다시 누르면 그 오브가
    // 여전히 거기 있다. 여기서 비우면 그다음부터 영영 안 움직인다.
    helper = null;
    return false;
  }

  const mime = pickMime();

  try {
    recorder = mime ? new MediaRecorder(stream, { mimeType: mime }) : new MediaRecorder(stream);
  } catch (e) {
    await discard();
    return false;
  }

  stopPromise = new Promise((resolve) => { resolveStop = resolve; });

  recorder.ondataavailable = (e) => {
    if (e.data && e.data.size > 0) chunks.push(e.data);
  };

  recorder.onstop = () => {
    if (resolveStop) {
      const r = resolveStop;
      resolveStop = null;
      r();
    }
  };

  startedAt = Date.now();
  lastLoudAt = startedAt;

  recorder.start();
  startMetering();

  return true;
}

/**
 * 녹음을 멈추고 소리를 돌려준다.
 *
 * @returns `{ base64, contentType, fileName, ms }`. 녹음 중이 아니었으면 null.
 */
export async function stop() {
  if (!recorder) return null;

  const mime = recorder.mimeType || 'audio/webm';
  const ms = Date.now() - startedAt;

  if (recorder.state === 'recording') {
    try {
      recorder.stop();
    } catch (e) {
      /* 이미 멎었다 */
    }
  }

  // **마지막 조각이 올 때까지 기다린다.** 안 기다리면 말끝이 잘린다.
  if (stopPromise) {
    try {
      await stopPromise;
    } catch (e) {
      /* 무시 */
    }
  }

  stopMetering();
  releaseStream();

  const blob = new Blob(chunks, { type: mime });
  chunks = [];
  recorder = null;
  stopPromise = null;
  helper = null;

  if (blob.size === 0) return null;

  const buf = await blob.arrayBuffer();

  return {
    base64: toBase64(buf),
    contentType: mime,
    fileName: 'utterance.' + extFor(mime),
    ms: ms,
  };
}

/** 녹음을 버린다. 마이크도 닫는다. */
export async function discard() {
  if (recorder && recorder.state === 'recording') {
    try {
      recorder.stop();
    } catch (e) {
      /* 무시 */
    }
  }

  if (resolveStop) {
    const r = resolveStop;
    resolveStop = null;
    r();
  }

  stopMetering();
  releaseStream();

  chunks = [];
  recorder = null;
  stopPromise = null;
  helper = null;
}

export function isRecording() {
  return !!recorder && recorder.state === 'recording';
}

function releaseStream() {
  if (!stream) return;

  // **반드시 꺼야 한다.** 안 끄면 브라우저 탭에 녹음 표시가 계속 켜져 있고,
  // 사람은 포털이 몰래 듣고 있다고 읽는다.
  try {
    stream.getTracks().forEach((t) => t.stop());
  } catch (e) {
    /* 무시 */
  }
  stream = null;
}

/**
 * 바이트를 base64 로. **한 번에 다 넘기지 않는다** — `String.fromCharCode` 에
 * 수십만 개를 펼쳐 넣으면 인자 수 제한에 걸려 통째로 실패한다.
 */
function toBase64(buffer) {
  const bytes = new Uint8Array(buffer);
  const step = 0x8000;
  let s = '';

  for (let i = 0; i < bytes.length; i += step) {
    s += String.fromCharCode.apply(null, bytes.subarray(i, i + step));
  }

  return btoa(s);
}

if (typeof window !== 'undefined') {
  window.addEventListener('beforeunload', () => {
    discard();
  });
}
