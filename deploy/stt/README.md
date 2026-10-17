# deploy/stt — 받아쓰기(STT) 서버 정본

AI쳇을 **말로** 쓰는 길의 뒷단이다. 브라우저가 녹음한 한 마디를 글로 바꿔 준다.

| 파일 | 장비 위치 | 무엇을 |
|---|---|---|
| `docker-compose.yml` | `/srv/stt/docker-compose.yml` | 받아쓰기 서버(speaches, OpenAI 호환) |
| `stt-nginx.conf.example` | — | 장비 nginx 에 덧붙일 자리의 **본보기** |

> **2026-10-17 에 올렸다.** `Systran/faster-whisper-small` · CPU(int8) ·
> `127.0.0.1:11437`. 그날 재어 본 값 — 장비 안에서 말소리 한 마디 **1.8초**,
> 운영 포털에서 끝까지 **1.76초**, 무음 조사 **72ms**. GPU 는 그때 이미
> 96·97% 차 있어(추론이 쓴다) CPU 말고는 자리가 없었다.

## 길

```
브라우저(녹음) → 회로 → 포털 → 게이트웨이 → AIAgentServer
                                                  ↓
                            장비 nginx:11434 /stt/ → speaches 127.0.0.1:11437
```

추론(`/v1/`)·임베딩(`/embed/`)과 **같은 장비, 같은 nginx, 같은 토큰**이다.
그래서 AIAgentServer 에 주소 설정이 따로 없다 — `AI:Providers:jsini` 의
`ApiBase` 에서 호스트만 떼어 `/stt/v1/audio/transcriptions` 를 붙인다
(`SpeechClient.ResolveUrl`). 개발 PC 와 운영이 저절로 갈린다.

## 왜 브라우저가 가진 받아쓰기를 안 쓰나

크롬에는 `SpeechRecognition` 이 있다. 그것을 쓰면 서버가 하나도 필요 없다.
그런데 두 가지가 걸린다.

- **아이폰에는 아예 없다.** Safari 가 그 규격을 지원하지 않아서, 휴대폰으로
  포털을 보는 사람의 절반이 음성 기능을 못 쓴다.
- **말소리가 바깥으로 나간다.** 크롬의 받아쓰기는 소리를 구글 서버로 보낸다.
  사내 업무를 말로 묻는 자리라 그것은 받아들일 수 없다.

서버를 하나 세우면 둘 다 없어진다. 녹음 자체(`MediaRecorder`)는 아이폰에서도
되기 때문이다. 대신 **잃는 것이 하나 있다** — 말하는 동안 글자가 따라 붙는
실시간 받아쓰기는 이 길로 안 된다. 녹음이 끝나야 글이 나온다.
그것까지 하려면 소리를 흘려보내는 길(WebSocket)을 따로 열어야 한다.

읽어주기(TTS)는 반대다. 브라우저 것이 대개 기기 안에서 처리되고 아이폰도 되므로
**서버를 안 쓴다**(`JSini.Web.Components/wwwroot/js/speech.js`).

## 왜 CPU 인가

그 장비의 GPU 는 추론(Qwen3-Next-80B)이 거의 다 쓰고 있다. 임베딩 서버(BGE-M3)를
CPU 로 돌리는 것과 같은 까닭이다. 받아쓰기는 임베딩과 달리 **GPU 를 안 써도 되는
일**이라, 모자라면 아예 다른 장비로 옮겨도 된다 — 그때는 `Stt:Endpoint` 에
그 주소를 적는다(그쪽이 이긴다).

## 모델 고르기

`WHISPER__MODEL` 하나를 바꾸는 일이다. 한국어는 모델이 커질수록 눈에 띄게 낫다 —
특히 **짧은 말**과 **사람·화면 이름** 같은 고유명사에서 갈린다.

| 모델 | 대략 크기 | 쓸 자리 |
|---|---|---|
| `Systran/faster-whisper-small` | ~0.5GB | 기본값. 짧은 지시("새 대화 열어 줘")에 쓸 만하다 |
| `Systran/faster-whisper-medium` | ~1.5GB | 긴 문장을 받아쓸 때. CPU 로는 느려진다 |
| `Systran/faster-whisper-large-v3` | ~3GB | 가장 정확하다. **CPU 로는 권하지 않는다** |

**속도는 장비에서 직접 재야 한다.** CPU 성능과 동시 사용자 수에 따라 크게
갈려서, 여기 숫자를 적어 두면 틀린 기대를 심는다. 재는 자리는 포털의
「AI 기능 상태」 화면이다 — 무음 하나를 보내 본 왕복 시간을 보여 준다.

모델을 바꾸면 **AIAgentServer 쪽 `Stt:Model` 도 같이 바꾼다.** 두 곳이 어긋나면
받아쓰기 서버가 요청받은 모델을 새로 내려받기 시작하고, 그 한 번이 몇 분 걸린다.

## 올리기

```bash
ssh jsini-gpu-root 'mkdir -p /srv/stt'
scp deploy/stt/docker-compose.yml jsini-gpu-root:/srv/stt/
ssh jsini-gpu-root 'cd /srv/stt && docker compose up -d'
```

모델을 미리 받아 둔다. **이것을 건너뛰면 첫 녹음이 몇 분을 기다린다:**

```bash
ssh jsini-gpu-root 'docker exec jsini-stt huggingface-cli download Systran/faster-whisper-small'
```

nginx 에 자리를 낸다 — `stt-nginx.conf.example` 의 블록을 추론·임베딩과 같은
server 블록, **`location /` 바로 위**에 덧붙인다. 그 블록이 나머지를 전부
추론 서버로 넘기므로 뒤에 두면 받아쓰기가 거기로 가서 404 가 난다.

토큰을 블록 안에 적지 않는다 — 그 파일 맨 위의
`map $http_authorization $llm_client` 하나가 이미 가르고 있고, 블록마다
다시 적으면 키를 바꾸는 날 **고친 자리와 안 고친 자리**가 생긴다.

```bash
ssh jsini-gpu-root 'cp -a /etc/nginx/sites-available/lmstudio /root/lmstudio.bak.$(date +%F-%H%M%S)'
# … 블록을 끼운 뒤 …
ssh jsini-gpu-root 'nginx -t && systemctl reload nginx'
```

**`nginx -t` 를 건너뛰지 않는다.** 이 파일 하나가 추론·임베딩·자동완성까지
들고 있어서, 문법이 깨진 채 reload 하면 **AI 가 통째로 멎는다.**

## 확인

장비 안에서 먼저. 무음을 보내 `{"text": ...}` 가 오면 선 것이다:

```bash
ssh jsini-gpu-root 'curl -s -F file=@/usr/share/sounds/alsa/Front_Center.wav \
  -F model=Systran/faster-whisper-small -F language=en \
  http://127.0.0.1:11437/v1/audio/transcriptions'
```

**무음이 아니라 말소리를 보낸다.** 무음은 `{"text":""}` 를 주는데 그것은
「돌긴 돈다」까지만 말해 준다 — 모델이 반쯤 망가져도 같은 답이 나온다.
그 장비에 들어 있는 `Front_Center.wav` 가 사람 목소리라 그대로 쓸 수 있고,
`{"text":"Front. Center."}` 가 나오면 끝까지 선 것이다.

토큰 없이 nginx 를 거쳐 보면 **401 이어야 맞다**:

```bash
ssh jsini-gpu-root 'curl -s -o /dev/null -w "%{http_code}\n" \
  -F file=@/usr/share/sounds/alsa/Front_Center.wav -F model=Systran/faster-whisper-small \
  http://127.0.0.1:11434/stt/v1/audio/transcriptions'
```

그다음 포털에서 — **「AI 기능 상태」 화면의 받아쓰기 칸**이 정본이다.
설정만 읽는 것이 아니라 무음 하나를 실제로 보내 본 결과를 보여 준다
(`AiFeatureProbe.MeasureSpeechAsync`). 2분 동안 재어 두므로, 방금 고친 것을
보려면 「지금 확인」을 누른다.

## 안 될 때

| 화면에 뜨는 말 | 거의 늘 이것이다 |
|---|---|
| 주소가 설정돼 있지 않습니다 | `AI:Providers:jsini` 가 비었다. 개발 PC 에서 흔하다 |
| 401 로 답했습니다 | nginx 의 토큰과 `AI:Providers:jsini:ApiKey` 가 다르다 |
| 404 로 답했습니다 | nginx 에 `/stt/` 자리를 안 냈거나 reload 를 안 했다 |
| 제때 답하지 않았습니다 (3분) | 모델을 내려받는 중이다. 위의 미리 받기를 건너뛴 경우 |
| 닿지 못했습니다 | 컨테이너가 안 떠 있다. `docker compose ps` 로 본다 |

**받아쓰기가 끊겨도 대화는 멀쩡하다.** 글로 묻는 길은 그대로라서 서비스 상태로는
안 갈린다 — RAG·웹 검색과 같은 갈래다. 화면에서는 마이크 단추만 접힌다.

## GPU 로 올리기

VRAM 이 나면 이미지 꼬리와 런타임만 바꾼다:

```yaml
image: ghcr.io/speaches-ai/speaches:latest-cuda
environment:
  - WHISPER__MODEL=Systran/faster-whisper-large-v3
  - WHISPER__COMPUTE_TYPE=float16
deploy:
  resources:
    reservations:
      devices:
        - driver: nvidia
          count: 1
          capabilities: [gpu]
```

**먼저 VRAM 이 실제로 남는지 본다.** 추론이 쓰고 있는 자리를 밀면 `llama-server`
가 죽고, 그때는 받아쓰기가 아니라 **AI 전체가 멎는다.** 추론 보조(MTP)를 켰다가
서비스가 죽었던 것과 같은 갈래의 실수다.

```bash
ssh root@192.168.219.227 'nvidia-smi --query-gpu=memory.used,memory.total --format=csv'
```
