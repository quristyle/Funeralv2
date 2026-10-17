# deploy/nginx — 운영 nginx 설정 정본

서버(`jin114.co.kr`)의 `/etc/nginx` 는 손으로 고쳐 왔다. 프론트를 컨테이너로
옮기면서 **정적 서빙 → 리버스 프록시**로 성격이 바뀌었으므로, 되돌릴 수 있게
정본을 저장소에 둔다.

| 파일 | 서버 위치 | 무엇을 |
|---|---|---|
| `00-websocket-upgrade.conf` | `/etc/nginx/conf.d/` | `$connection_upgrade` map (Blazor 회로) |
| `portal.jsini.co.kr.conf` | `/etc/nginx/sites-available/` | 업무 포털 → `127.0.0.1:5557` |
| `jsini.co.kr.conf` | `/etc/nginx/sites-available/` | 소개 사이트 → `127.0.0.1:5556` |
| `html/50x.html` | `/srv/jsini/nginx-errors/` | 뒷단이 죽었을 때의 안내 화면 (502 · 503 · 504) |

## 적용

```bash
sudo cp deploy/nginx/00-websocket-upgrade.conf /etc/nginx/conf.d/
sudo cp deploy/nginx/portal.jsini.co.kr.conf   /etc/nginx/sites-available/
sudo cp deploy/nginx/jsini.co.kr.conf          /etc/nginx/sites-available/

# 오류 화면은 sudo 가 필요 없다 — /srv/jsini 는 lee 소유다.
mkdir -p /srv/jsini/nginx-errors
cp deploy/nginx/html/50x.html /srv/jsini/nginx-errors/
chmod 644 /srv/jsini/nginx-errors/50x.html   # 워커가 www-data 로 읽는다

sudo nginx -t && sudo systemctl reload nginx
```

`sites-enabled` 의 심볼릭 링크는 이미 있다 — 파일 내용만 바뀐다.

**오류 화면만 고쳤으면 reload 도 필요 없다.** nginx 가 매번 파일에서 읽는다 —
`cp` 한 순간부터 바뀐 화면이 나간다. 설정(`error_page` · `ssi` · `root`)을
건드렸을 때만 reload 한다.

**`nginx -t` 를 건너뛰지 않는다.** 설정이 틀린 채로 reload 하면 nginx 는 옛
설정으로 계속 돌지만, 다음 재기동에서 올라오지 않는다. 그 시차 때문에
"어제 고친 것" 과 "오늘 안 뜨는 것" 이 연결되지 않는다.

## 뒷단이 죽었을 때의 화면

컨테이너가 죽었거나 아직 안 떴으면 nginx 가 502 를 낸다. 기본값은 nginx 가
들고 있는 흰 바탕에 「502 Bad Gateway」 한 줄이라, 보는 사람에게는 **회사
서비스가 망가진 것인지 자기 망이 끊긴 것인지** 구분이 안 간다.

`html/50x.html` 이 그 자리를 대신한다. 두 사이트가 **같은 파일 하나**를 쓴다.

| 어디서 | 무엇이 나가나 |
|---|---|
| `/` 아래 (사람이 보는 길) | 브랜드 안내 화면 (`text/html`) |
| `/api/` 아래 (프로그램이 보는 길) | `ApiResponse` 모양의 JSON |

**`/api/` 를 가른 이유** — 이 길로 오는 것은 사람이 아니라 프로그램이다
(플레이어 · 북마크 · 공지 본문에 박힌 내려받기 주소). HTML 을 돌려주면 JSON 을
기다리던 쪽이 파싱 오류로 죽어서, **진짜 원인(게이트웨이가 죽었다)이 엉뚱한
오류 뒤로 숨는다.**

### 화면이 하는 일

- **바깥을 하나도 부르지 않는다.** 스타일 · 로고 · 파비콘이 전부 파일 안에
  있다. 그것들을 따로 받아 오게 두면 **전부 다시 502 로 떨어진다** — 하필
  가장 망가져 보이면 안 될 화면이 제일 망가져 보인다.
- **되살아났는지 스스로 물어본다.** 5초 뒤부터 `HEAD` 로 한 번씩 두드리고,
  500 미만이 오면(404 여도 서버는 산 것이다) 보던 화면으로 되돌아간다.
  간격은 5 → 10 → … → 30초로 늘어나고, 보지 않는 탭에서는 세지 않는다.
  새로고침을 반복하지 않는 이유는 죽어 있는 동안 화면이 깜빡이기 때문이다.
- **숫자는 SSI 가 채운다.** 502 · 503 · 504 가 한 파일을 쓰고, 화면의
  「오류」 줄에만 실제 코드가 박힌다. `ssi on;` 을 빠뜨리면 **그 자리가 빈 채로
  나가고 화면은 멀쩡해 보인다.**
- 글꼴은 장비 것을 쓴다. S-CoreDream 은 한 무게에 350KB 라 파일에 품을 수 없고,
  서버에서 받아 오려는 순간 위의 첫 줄이 깨진다. 브랜드는 로고(SVG)와 5색 ·
  각진 모서리로 낸다.

### 눈으로 확인하기

운영을 죽여 볼 수는 없으므로, 죽은 포트를 뒷단으로 삼은 nginx 를 **5만번대
포트에** 따로 띄워 본다(운영 nginx 와 pid · 포트가 달라 서로 모른다).

```bash
# 설정에서 TLS 와 인증서 줄만 걷어내고(그 줄은 root 만 읽는다),
# 뒷단 포트를 아무도 없는 55199 로 돌린 사본을 만들어 -t 와 curl 을 돌린다.
/usr/sbin/nginx -t -c /tmp/.../nginx.conf
curl -s -o /dev/null -w '%{http_code} %{content_type}\n' http://127.0.0.1:55111/
```

`nginx -t` 를 건너뛰지 않는다는 위의 규칙이 여기에도 그대로 걸린다.

## 되돌리기

프론트 컨테이너가 안 뜨는 동안 옛 화면으로 버텨야 하면, 이 파일들 이전 판을
git 이력에서 꺼내 되돌린다. 옛 정적 산출물(`/srv/jsini/portal`,
`/srv/jsini/site`)은 **당분간 지우지 않는다** — 되돌아갈 자리가 있어야 한다.

```bash
git log --oneline -- deploy/nginx
git show <커밋>:deploy/nginx/portal.jsini.co.kr.conf
```

## 이 설정이 전제하는 것

- 컨테이너는 **127.0.0.1 로만** 공개된다(`docker-compose.prod.yml`).
  바깥에서 직접 닿을 수 없으니 `X-Forwarded-*` 를 위조해 넣을 자리가 없고,
  그래서 포털 컨테이너가 `ASPNETCORE_FORWARDEDHEADERS_ENABLED` 로 신뢰
  대역을 열어 둘 수 있다. **포트를 0.0.0.0 으로 여는 순간 그 전제가 깨진다.**
- 인증서 경로는 기존 certbot 배치 그대로다. 도메인을 더하면 certbot 을
  먼저 돌리고 이 파일에 `server_name` 을 더한다.
