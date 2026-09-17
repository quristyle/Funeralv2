# AI 작업 실행기 — 장비 쪽 (0단계)

설계는 [docs/ai-task-runner.md](../../docs/ai-task-runner.md).

실행기는 **컨테이너가 아니다.** 배포 이미지 열둘은 그대로 열둘이고, 이것은
운영 서버 호스트에서 systemd 로 상주한다 — CLI 와 로그인 자격이 호스트에
있기 때문이다.

---

## 왜 이 단계를 1단계보다 먼저 하나

실행 계정을 나누지 않기로 했고(`lee`), CLI 는 권한을 다 열어 돌린다
(`--dangerously-skip-permissions`). 그래서 **남은 경계가 systemd 유닛
하나뿐**이다.

유닛을 나중에 붙이면 「일단 되게 해 놓은」 상태로 운영 서버에서 AI 가 도는
기간이 생긴다. 그 기간이 이 기능에서 제일 위험한 시간이다.

---

## 먼저 — **코드가 운영에 가 있어야 한다**

실행기만 띄운다고 되지 않는다. 실행기가 부르는 `/api/ai-runner/*` 는
**`ProjMngServer` 에 새로 생긴 경로**라, 운영 컨테이너가 옛 이미지면 404 다.

```
운영 컨테이너: ghcr.io/quristyle/funeralv2-projmng:6e9fff5…   ← AI 작업 코드가 없다
```

그러니 순서가 이렇다.

| 순서 | 무엇 | 왜 |
|---|---|---|
| 1 | 코드를 `main` 에 올린다 | 배포가 돌아 `ProjMngServer` 가 새 코드로 뜬다 |
| 2 | 표를 확인한다 | `deploy/sql/projmng-ai-tasks-*.sql` — **이미 반영돼 있다**(2026-09-17) |
| 3 | 서버에 토큰을 넣는다 | 없으면 집어가기를 **거절한다** |
| 4 | 실행기를 올리고 유닛을 넣는다 | 아래 |
| 5 | 담장이 막는지 확인한다 | 아래 |

**1번이 곧 배포다.** 이미지 열둘이 말려 올라가고 운영이 새 코드로 바뀐다 —
AI 작업 기능만 나가는 것이 아니다.

---

## 넣기

### ① 서버 쪽 토큰 (배포가 끝난 뒤)

```bash
# 아무 긴 문자열이면 된다. 실행기와 **같은 값**이어야 한다.
openssl rand -hex 24
```

`/srv/jsini/config/ProjMngServer/appsettings.Local.json` 에 더한다:

```json
{
  "AiTasks": {
    "RunnerToken": "<위에서 만든 값>",
    "QueueHost": "host.docker.internal",
    "PortalUrl": "https://portal.jsini.co.kr",
    "NotifyUrl": "http://host.docker.internal:5460"
  }
}
```

> **`QueueHost` 를 `localhost` 로 두면 안 된다.** RabbitMQ 는 호스트에서 돌고
> `ProjMngServer` 는 컨테이너 안이라, 거기서 `localhost` 는 자기 자신이다.
> 넣기에 실패해도 실행기의 주기 조회가 집으므로 기능은 돌지만, **종이 안 울려
> 매번 최대 1분 늦는다.**

고친 뒤 그 컨테이너만 다시 띄운다:

```bash
cd /srv/jsini && docker compose restart projmng
docker compose logs -f projmng | grep -i "AI 작업 감시자"
```

### ② 실행기 말기 — **개발 장비에서** 한다

운영 장비의 .NET SDK 는 8.0 이고 이 프로그램은 net10.0 이라 **거기서는 빌드가
안 된다.** 자체 포함으로 말아서 통째로 올린다(약 110MB).

```bash
# 개발 장비에서
cd ~/Funeralv2/tools/AiTaskRunner
dotnet publish -c Release -r linux-x64 --self-contained true -o /tmp/ai-runner

ssh jsini-prod "mkdir -p /home/lee/ai-task-runner"
rsync -a --delete /tmp/ai-runner/ jsini-prod:/home/lee/ai-task-runner/
ssh jsini-prod "chmod +x /home/lee/ai-task-runner/AiTaskRunner"
```

### ③ 장비 쪽 설정과 폴더

```bash
# 운영 장비에서
mkdir -p /home/lee/ai-workspaces
sudo mkdir -p /srv/ai-targets && sudo chown lee:lee /srv/ai-targets   # 폴더 대상을 쓸 때만

tee /home/lee/ai-task-runner/appsettings.Local.json >/dev/null <<'JSON'
{
  "Runner": {
    "RunnerToken": "<서버에 넣은 것과 같은 값>"
  }
}
JSON
chmod 600 /home/lee/ai-task-runner/appsettings.Local.json
```

### ④ 유닛

```bash
sudo cp /home/lee/Funeralv2/deploy/ai-runner/ai-task-runner.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now ai-task-runner
journalctl -u ai-task-runner -f
```

기동 로그에 이렇게 떠야 한다:

```
실행기 jsini-prod 시작 · 서버 http://127.0.0.1:5450 · CLI [claude,antigravity] · 동시 5 · 조회 60초
큐 ai_task@localhost 를 듣습니다.
```

> **`ServerUrl` 이 `127.0.0.1:5450` 인 것이 맞다.** 그 포트는 컨테이너가
> 호스트 루프백에 열어 둔 자리다(`docker-compose.prod.yml`). 게이트웨이를
> 거치지 않는다 — 실행기 경로는 게이트웨이에 열지 않는다(설계 9.8).

---

## 넣고 나서 반드시 확인하는 것

담장은 **설정 파일 한 장에 얹혀 있다.** 유닛을 고치면 없어지므로, 넣은 뒤
한 번은 실제로 막혔는지 본다. 가장 확실한 방법은 **AI 작업으로 시켜 보는
것**이다 — 아래 넷을 각각 한 건씩 돌려 전부 실패해야 한다.

| 시킬 일 | 막혀야 하는 이유 |
|---|---|
| `/srv/jsini/config` 를 읽어라 | DB 비밀번호·JWT 키 (③) |
| `docker ps` 를 해라 | docker 그룹 = 사실상 root (②) |
| `sudo -n true` 를 해라 | 권한 상승 (①) |
| `/etc` 에 파일을 써라 | `ProtectSystem=strict` (④) |

**`/srv/jsini/.env` 읽기는 성공해야 한다** — 안에 `TAG` 한 줄뿐이고
되돌리기에 그 값이 필요해서 일부러 열어 두었다.

---

## 대상 뿌리를 늘릴 때

두 곳을 **함께** 고친다. 한쪽만 고치면 증상이 갈린다.

| 어디 | 안 고치면 |
|---|---|
| 서버 `AiTasks:AllowedRoots` | 화면이 등록을 거절한다 — 「허용된 뿌리 아래여야 합니다」 |
| 이 유닛의 `ReadWritePaths` | 등록은 되는데 **실행만 실패한다** |

뒤엣것이 더 나쁘다. 등록이 됐으니 사람은 되는 줄 알고, 실패는 실행기
로그에만 남는다.

---

## 멈추기

```bash
sudo systemctl stop ai-task-runner      # 실행기만 멈춘다. 요청은 쌓인다
```

서버 쪽 킬 스위치는 `AiTasks:Enabled=false` 다 — 그쪽은 **요청 자체를**
막는다. 둘은 다르다:

- 실행기를 멈추면 요청은 그대로 쌓이고, 다시 띄우면 밀린 것부터 돈다.
- `Enabled=false` 면 큐에 넣지 않는다(요청은 DB 에 남는다).

돌고 있는 건을 멈추려면 **화면에서 취소**한다. 실행기가 하트비트로 그것을
보고 프로세스를 자식까지 죽인다.
