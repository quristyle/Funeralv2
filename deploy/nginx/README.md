# deploy/nginx — 운영 nginx 설정 정본

서버(`jin114.co.kr`)의 `/etc/nginx` 는 손으로 고쳐 왔다. 프론트를 컨테이너로
옮기면서 **정적 서빙 → 리버스 프록시**로 성격이 바뀌었으므로, 되돌릴 수 있게
정본을 저장소에 둔다.

| 파일 | 서버 위치 | 무엇을 |
|---|---|---|
| `00-websocket-upgrade.conf` | `/etc/nginx/conf.d/` | `$connection_upgrade` map (Blazor 회로) |
| `portal.jsini.co.kr.conf` | `/etc/nginx/sites-available/` | 업무 포털 → `127.0.0.1:5557` |
| `jsini.co.kr.conf` | `/etc/nginx/sites-available/` | 소개 사이트 → `127.0.0.1:5556` |

## 적용

```bash
sudo cp deploy/nginx/00-websocket-upgrade.conf /etc/nginx/conf.d/
sudo cp deploy/nginx/portal.jsini.co.kr.conf   /etc/nginx/sites-available/
sudo cp deploy/nginx/jsini.co.kr.conf          /etc/nginx/sites-available/
sudo nginx -t && sudo systemctl reload nginx
```

`sites-enabled` 의 심볼릭 링크는 이미 있다 — 파일 내용만 바뀐다.

**`nginx -t` 를 건너뛰지 않는다.** 설정이 틀린 채로 reload 하면 nginx 는 옛
설정으로 계속 돌지만, 다음 재기동에서 올라오지 않는다. 그 시차 때문에
"어제 고친 것" 과 "오늘 안 뜨는 것" 이 연결되지 않는다.

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
