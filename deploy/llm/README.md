# deploy/llm — LLM 장비(호스트명 `llm`)의 수집기 정본

추론을 돌리는 GPU 장비 한 대(`192.168.219.227`, RTX 3090 ×2)의 설정은 그동안
그 장비 안에만 있었다. 포털의 **「LLM 장비 상태」(`/admin/status/llm`)** 가 읽는
것이 바로 이 수집기인데, 저장소에는 받아 쓰는 쪽(`LlmHardwareModels.cs`)만 있고
**주는 쪽이 없어서** 화면이 왜 그렇게 말하는지 코드로 따라갈 수 없었다.
그래서 정본을 여기 둔다.

| 파일 | 장비 위치 | 무엇을 |
|---|---|---|
| `llm-metrics` | `/usr/local/bin/llm-metrics` | 수집기 본체(파이썬, 11436 포트에 JSON 한 장) |
| `llm-metrics.service` | `/etc/systemd/system/` | 그것을 띄우는 유닛 |
| `nvidia-reload.sh` | `/usr/local/bin/nvidia-reload` | 드라이버 판 어긋남을 재부팅 없이 푸는 손질 |

## 길

```
포털 → 게이트웨이 → AIAgentServer → 장비 nginx:11434 /metrics → 수집기 127.0.0.1:11436
```

nginx(`/etc/nginx/sites-available/lmstudio`)가 `Bearer` 하나로 가르고 운영서버
IP 만 받는다. **그 설정은 토큰이 평문으로 들어 있어 저장소에 두지 않는다.**

## 적용

```bash
scp deploy/llm/llm-metrics      root@192.168.219.227:/root/
scp deploy/llm/nvidia-reload.sh root@192.168.219.227:/root/
ssh root@192.168.219.227 '
  python3 -c "import ast;ast.parse(open(\"/root/llm-metrics\",encoding=\"utf-8\").read())" &&
  install -o root -g root -m 755 /root/llm-metrics      /usr/local/bin/llm-metrics &&
  install -o root -g root -m 755 /root/nvidia-reload.sh /usr/local/bin/nvidia-reload &&
  systemctl restart llm-metrics && systemctl is-active llm-metrics'
```

**`ast.parse` 를 건너뛰지 않는다.** 문법이 깨진 채 넣으면 유닛이 `Restart=always`
로 5초마다 되살아나며 계속 죽고, 화면에는 「LLM 장비에 닿지 못했습니다」만 떠서
**장비가 꺼진 것과 구분되지 않는다.**

## 드라이버 판 어긋남 — 「GPU 를 읽지 못했습니다」

2026-10-11 에 unattended-upgrades 가 nvidia 를 595.84 → 595.99.02 로 올렸다.
**올라가 있는 커널 모듈은 그대로 595.84 였다.** 그래서 NVML 초기화가 막히고
`nvidia-smi` 가 `Driver/library version mismatch` 로 끝났다.

그런데 **추론은 멀쩡히 돌았다** — `llama-server` 가 이미 잡아 둔 GPU 핸들을
그대로 쓰기 때문이다. 「서비스는 정상인데 GPU 칸만 비어 있다」는, 원인을 짚기
나쁜 모양이 된다. 실제로 사흘 동안 그 상태였다.

짚는 법 — 두 판을 나란히 본다:

```bash
cat /proc/driver/nvidia/version   # 지금 올라가 있는 커널 모듈
modinfo nvidia | grep ^version    # 설치돼 있는 것
```

푸는 법 둘:

| | 무엇이 끊기나 | 위험 |
|---|---|---|
| `nvidia-reload` (권함) | 추론 1~2분 | SSH 가 안 끊긴다. 실패해도 서비스만 되살아난다 |
| 재부팅 | 장비 전체 | **부팅해 본 적 없는 새 커널로 넘어간다** — 안 돌아오면 장비 앞에 가야 한다 |

```bash
ssh root@192.168.219.227 '
  systemd-run --unit=nvidia-reload --collect --service-type=oneshot \
      /bin/bash /usr/local/bin/nvidia-reload'
ssh root@192.168.219.227 'journalctl -u nvidia-reload --no-pager -o cat'
```

**SSH 안에서 맨손으로 돌리지 않는다.** `gdm` 을 끊는 동안 연결이 흔들리면
모듈만 내려간 채로 남는다. systemd 가 들고 있게 맡긴다.

### 또 날 수 있다

꾸러미 자동 갱신을 그대로 두는 한 **드라이버가 올라갈 때마다 다시 난다.**
막으려면 장비에서 자동 갱신에서 빼면 되지만, 그만큼 드라이버 보안 갱신도 멈춘다:

```bash
# /etc/apt/apt.conf.d/50unattended-upgrades 의 Package-Blacklist 에
"nvidia-"; "libnvidia-";
```

지금은 **막지 않고 보이게 해 두었다** — 수집기가 어긋남을 알아보고 화면에
「올라가 있는 커널 모듈 A, 설치된 라이브러리 B … 다시 올려야 합니다」까지 적는다.
그 전에는 까닭이 통째로 사라져 **「nvidia-smi 실행 실패」 한 줄**이었다
(`run()` 이 종료코드가 0 이 아니면 빈 문자열을 돌려주어 stderr 를 버렸다).
