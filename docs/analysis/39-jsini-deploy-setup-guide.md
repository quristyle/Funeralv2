# 39. jsini 배포 구축 가이드 — 성공 명령어 기록 (수동 재현용)

2026-08-30 에 jsini.co.kr(회사 사이트)·portal.jsini.co.kr(업무 포털) 자동배포를
구축하며 **실제로 성공한 명령만** 단계별로 기록한다. 서버를 새로 만들거나
수동으로 복구할 때 이 순서대로 따라 하면 된다.

- 운영서버: `ssh lee@jin114.co.kr -p 31010` (Ubuntu 24.04, 호스트명 han)
- 저장소: https://github.com/quristyle/Funeralv2.git (기본 브랜치 main)
- 비밀값은 이 문서에 없다 — 서버의 `/srv/jsini/config/` 와 각 담당자만 안다.

## 확정된 구조 요약

```
git push (main) → GitHub Actions 빌드 → GHCR push → 서버 러너 pull·up
```

| 항목 | 내용 |
|---|---|
| 백엔드 | Docker Compose, 이미지 6종 (gateway·auth·file·site·notify·life), 내부 포트 8080 통일 |
| 게이트웨이 | 유일하게 호스트 공개: `127.0.0.1:5265` — nginx 만 여기로 프록시 |
| 프론트 | 컨테이너 없음. nginx 정적 서빙 (`/srv/jsini/site`, `/srv/jsini/portal`) |
| 비밀값 | `/srv/jsini/config/<서비스>/appsettings.Local.json` 을 컨테이너에 ro 마운트 |
| DB | 서버 네이티브 PostgreSQL 16. **내부에서도 포트 31015 다** (postgresql.conf 에 port=31015) |
| 유지 | nginx · PostgreSQL · RabbitMQ · 구 헬프데스크(help_publish)는 네이티브 그대로 |

## 0. 사전 정리 (한 번만 한 것)

- 서비스 종료한 goldenbar 제거:
  ```bash
  sudo systemctl stop goldbRestApi && sudo systemctl disable goldbRestApi
  sudo rm /etc/systemd/system/goldbRestApi.service && sudo systemctl daemon-reload
  rm -rf /home/lee/goldb_publish /home/lee/projects/goldenbar/goldenbar_front/dist
  ```
- nginx 를 한 파일(`sites-available/default`)에서 **도메인별 파일로 분리**했다.
  현재 구조는 `/etc/nginx/sites-available/*.conf` 를 보면 된다. 분리 전 원본 백업:
  `/home/lee/backup/nginx-split-20260829-131524/`

## 1. 보안 정리 (public 저장소 전제 조건)

VAPID 키가 추적 파일에, 실명·이메일·비밀번호 해시가 docs 에 있었다. 재현 절차:

```bash
# 새 VAPID 키쌍 생성 (P-256, base64url) — 값은 출력해서 Local.json 에만 넣는다
python -c "
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives import serialization
import base64
k = ec.generate_private_key(ec.SECP256R1())
b = lambda x: base64.urlsafe_b64encode(x).rstrip(b'=').decode()
print('PUB=', b(k.public_key().public_bytes(serialization.Encoding.X962, serialization.PublicFormat.UncompressedPoint)))
print('PRIV=', b(k.private_numbers().private_value.to_bytes(32,'big')))"
```

히스토리 정리 (문자열 치환표 `replacements.txt` 를 만들어서):

```bash
pip install git-filter-repo
git clone --mirror https://github.com/quristyle/Funeralv2.git mirror.git
cd mirror.git
git bundle create ../pre-rewrite-backup.bundle --all   # 되돌릴 백업
python -m git_filter_repo --replace-text ../replacements.txt
git remote add origin https://github.com/quristyle/Funeralv2.git
git push --mirror origin                                # 강제 덮어쓰기
# 로컬 저장소는: git fetch origin && git reset --mixed origin/main
```

주의: force push 후에도 GitHub 캐시에 옛 커밋이 남는다 — Support 에 GC 요청.
키는 히스토리를 지워도 **반드시 재발급**한다 (한 번 공개된 키는 유출로 간주).

## 2. DNS + 인증서

DNS 는 등록기관에서 A/CNAME 을 서버 IP(175.200.10.40)로. 발급은 webroot 방식 —
80 포트의 catch-all(`default`)이 `/var/www/html` 을 서빙하므로 검증이 통과한다.

```bash
sudo certbot certonly --webroot -w /var/www/html -d jsini.co.kr -d www.jsini.co.kr --cert-name jsini.co.kr --expand -n
sudo certbot certonly --webroot -w /var/www/html -d portal.jsini.co.kr --cert-name portal.jsini.co.kr -n
```

갱신은 기존 `certbot.timer` 가 자동으로 한다. **주의: 각 도메인의 80 블록에
`/.well-known/acme-challenge/` 는 리다이렉트에서 제외**해야 갱신이 안 깨진다
(아래 6절 nginx 설정에 반영돼 있다).

## 3. 운영 설정 (/srv/jsini)

```bash
sudo mkdir -p /srv/jsini && sudo chown lee:lee /srv/jsini
mkdir -p /srv/jsini/{config,site,portal,files}
chmod 700 /srv/jsini/config
# 각 서비스 폴더에 appsettings.Local.json 을 만들고 chmod 600
```

서비스별 Local.json 에 들어가는 키 (값은 서버 파일 참조):

| 서비스 | 필요한 키 |
|---|---|
| ApiGateway | `Jwt.Key`(운영 전용 새 값) + `ReverseProxy.Clusters.*` 컨테이너 주소 덮어쓰기 |
| AuthServer | `ConnectionStrings.jsinicore`, `JwtSettings.SecretKey`(게이트웨이와 동일) |
| FileServer | `ConnectionStrings.jsinifileconn`, `Storage.LocalPath=/data/files`, `Storage.FallbackUrl` |
| SiteServer | `ConnectionStrings.jsinisiteconn` |
| NotificationServer | `ConnectionStrings.jsinicore`, `Jwt.Key`, `Vapid.*`(운영 전용 쌍), `EmailSettings.*` |
| LifeEnvServer | `ConnectionStrings.jsinilifeenvconn`, `Weather.ServiceKey` |

- 접속 문자열의 Host 는 `host.docker.internal` (compose 가 host-gateway 로 매핑),
  Port 는 **31015**. JWT 키는 개발 환경과 다른 값을 새로 만든다:
  `python -c "import secrets,base64; print(base64.b64encode(secrets.token_bytes(48)).decode())"`
- `SkipPasswordCheck` 는 코드가 `IsDevelopment()` 일 때만 허용하므로
  Production 컨테이너에서는 자동으로 꺼진다 — 별도 설정 불필요.

게이트웨이 라우팅 덮어쓰기 (ApiGateway 의 Local.json 에 추가):

```json
"ReverseProxy": { "Clusters": {
  "auth-cluster":         { "Destinations": { "destination1": { "Address": "http://auth:8080" } } },
  "file-cluster":         { "Destinations": { "destination1": { "Address": "http://file:8080" } } },
  "site-cluster":         { "Destinations": { "destination1": { "Address": "http://site:8080" } } },
  "notification-cluster": { "Destinations": { "destination1": { "Address": "http://notify:8080" } } },
  "life-cluster":         { "Destinations": { "destination1": { "Address": "http://life:8080" } } } } }
```

DB 접속 검증:

```bash
PGPASSWORD=<암호> psql -h localhost -p 31015 -U funeralv2 -d jsiniportal -tAc "select 1"
PGPASSWORD=<암호> psql -h localhost -p 31015 -U funeralv2 -d jsinisite   -tAc "select 1"
PGPASSWORD=<암호> psql -h localhost -p 31015 -U ghub      -d ghub        -tAc "select 1"
```

## 4. Docker

```bash
sudo apt-get install -y docker.io docker-compose-v2
sudo usermod -aG docker lee          # 재로그인 후 적용
sudo systemctl enable --now docker
```

이미지 빌드 (컨텍스트는 저장소 루트 — Common 공유 프로젝트 참조 때문):

```bash
git clone --depth 1 https://github.com/quristyle/Funeralv2.git /srv/jsini/src
cd /srv/jsini/src
# 서비스명 → 프로젝트 매핑: gateway=ApiGateway, auth=AuthServer, file=FileServer,
#                           site=SiteServer, notify=NotificationServer, life=LifeEnvServer
docker build -f deploy/docker/Dockerfile \
  --build-arg PROJ_DIR=microservices/AuthServer --build-arg PROJ_NAME=AuthServer \
  -t ghcr.io/quristyle/funeralv2-auth:local .
# (나머지 5종 동일 패턴 — /srv/jsini/build-images.sh 에 전체 루프가 있다)
```

compose 기동:

```bash
cp /srv/jsini/src/deploy/docker/docker-compose.prod.yml /srv/jsini/docker-compose.yml
echo "TAG=local" > /srv/jsini/.env
cd /srv/jsini && docker compose up -d
docker compose ps
```

검증 — 게이트웨이 → 서비스 → DB 관통 (틀린 계정에 401 JSON 이 오면 전부 정상):

```bash
curl -s -w "\n%{http_code}\n" -X POST http://127.0.0.1:5265/api/auth/login \
  -H "Content-Type: application/json" -d '{"userId":"smoke","password":"x"}'
```

게이트웨이 로그의 `ai-cluster`/`funeral-cluster` 등 프로브 실패는 1차 범위 밖
서비스라 정상이다.

## 5. nginx (프론트 도메인 2건)

설정 파일 전문은 서버의 `/etc/nginx/sites-available/jsini.co.kr.conf` ·
`portal.jsini.co.kr.conf` 에 있다. 핵심 구조:

- `jsini.co.kr`: root `/srv/jsini/site`, vite-ssg 라우팅
  `try_files $uri $uri/ $uri.html /index.html`. www 는 301 로 본 도메인 통일.
- `portal.jsini.co.kr`: root `/srv/jsini/portal`, SPA 폴백 `try_files $uri /index.html`,
  `client_max_body_size 200M` (파일 업로드).
- 두 도메인 모두 `/api/` → `http://127.0.0.1:5265/api/` (게이트웨이 컨테이너).
- 80 블록: `location /.well-known/acme-challenge/ { root /var/www/html; }` 를
  리다이렉트보다 먼저 둔다 (인증서 갱신 경로).

```bash
sudo ln -sf /etc/nginx/sites-available/jsini.co.kr.conf /etc/nginx/sites-enabled/
sudo ln -sf /etc/nginx/sites-available/portal.jsini.co.kr.conf /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

검증:

```bash
curl -sk -o /dev/null -w "%{http_code}\n" https://jsini.co.kr/          # 200
curl -sk -o /dev/null -w "%{http_code}\n" https://portal.jsini.co.kr/   # 200
curl -sk -X POST https://portal.jsini.co.kr/api/auth/login \
  -H "Content-Type: application/json" -d '{"userId":"smoke","password":"x"}'  # 401 JSON
```

## 6. 남은 단계 (이 문서 작성 시점 기준)

- self-hosted 러너 설치 + GitHub Actions 워크플로 (경로 필터, GHCR push, 태그 배포)
- **public 저장소 + 러너 필수 설정**: GitHub → Settings → Actions →
  "Require approval for all external contributors". 배포 워크플로는 `push`(main)
  이벤트에만 러너를 쓴다 — fork PR 에 러너를 절대 배정하지 않는다.
- 첫 배포: 프론트 빌드 산출물을 `/srv/jsini/site`·`/srv/jsini/portal` 에 배치
  (지금은 자리표시자 index.html 만 있다), 백엔드는 GHCR 태그로 전환.
- 운영 배포/롤백 요령: `/srv/jsini/.env` 의 `TAG` 를 바꾸고
  `docker compose pull && docker compose up -d`. 롤백은 이전 태그로 동일하게.
