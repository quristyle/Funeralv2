# 보내는 메일 (SMTP) 설정

포털에서 나가는 메일은 **전부 한 길로 지나간다** —
NotificationServer 의 `POST /emails/send`(SMTP 직발송)다.

| 무엇 | 부르는 서비스 | 언제 |
|---|---|---|
| 비밀번호 찾기 링크 | AuthServer | 로그인 화면의 「비밀번호를 잊으셨나요」 |
| 가입 신청 안내 | AuthServer | 가입 신청·승인 |
| 소개 사이트 문의 접수 | SiteServer | jsini.co.kr 문의 등록 |
| AI 작업 완료 알림 | ProjMngServer | 지시에 「끝나면 메일로」를 켠 경우 |
| 관리자 메일 보내기 | 포털 관리 화면 | 사람이 직접 |

그래서 **이 설정 하나가 비면 위의 것이 모두 멈춘다.**

## 어디에 넣나

비밀이 아닌 값(호스트·포트·계정·보낸이 이름)은
`microservices/NotificationServer/appsettings.json` 의 `EmailSettings` 에 이미
적혀 있다. 채워야 하는 것은 **비밀번호 하나**다.

- 개발 장비 — `scripts/secrets.env` 에 `EmailSettings__Password=…`
  (또는 `microservices/NotificationServer/appsettings.Local.json`)
- 운영 서버 — `/srv/jsini/config/NotificationServer/appsettings.Local.json`

```json
{
  "EmailSettings": {
    "Password": "여기에 메일 계정 비밀번호"
  }
}
```

compose 에는 넣지 않는다. 운영의 비밀값은 `/srv/jsini/config/<서비스>/appsettings.Local.json`
마운트로만 들어간다(`deploy/docker/docker-compose.prod.yml` 머리말).

호스트나 계정을 바꿔야 하면 같은 파일에서 `Host`·`Port`·`User` 를 함께 덮어쓴다.
기본값은 HelpDeskServer 가 예전부터 쓰던 발송 계정과 같다
(`microservices/HelpDeskServer/Utilities/EMailUtil.cs`).

포트는 **587 이 STARTTLS, 465 가 접속부터 SSL** 이다. `SmtpEmailSender` 가
포트 번호로 가르므로 465 로 바꿀 때 따로 켤 스위치는 없다.

## 들어갔는지 확인

기동 로그 한 줄에 나온다.

```
NotificationServer 시작. 푸시=사용 가능 메일(SMTP)=사용 가능 (jinnets.co.kr:587) 메일(큐)=… 배포알림=…
```

`메일(SMTP)=설정 없음 (EmailSettings:Password)` 이면 **아직 안 들어간 것이다.**
이 줄 다음에 무엇이 멈추는지 적은 경고가 한 번 더 뜬다.

```
docker compose logs notify | grep -i smtp
```

사람 손으로 한 통 보내 보려면 포털 관리의 **메일 보내기** 화면을 쓴다.
그쪽은 직발송이라 실패하면 그 자리에서 까닭을 말해 준다.

## 안 왔다는 말을 들었을 때

비밀번호 찾기는 **아이디가 있는지 알려 주지 않으려고 화면에 언제나
「보냈습니다」를 띄운다.** 그래서 무슨 일이 있었는지는 로그에만 있다.

```
docker compose logs auth | grep 비밀번호
```

| 로그 | 뜻 | 할 일 |
|---|---|---|
| `링크를 보냈다` | 메일 서버까지 갔다 | 받는 쪽 스팸함을 본다 |
| `없는 아이디` | 아이디를 잘못 적었다 | — |
| `이메일이 계정과 다르다 (등록 N건)` | 계정에 있는 주소와 다른 것을 적었다 | 계정 관리에서 등록된 주소를 확인 |
| `계정에 이메일이 없다` | 계정에 주소가 아예 없다 | 관리자가 계정 관리에서 넣는다 |
| `메일 발송 실패: HTTP 502` | SMTP 가 거절했다 | notify 로그를 본다 |
| `메일 발송 예외` | notify 를 못 불렀다 | `Notify__BaseUrl` 과 notify 컨테이너 상태 |

## 링크 주소

메일에 실리는 링크는 AuthServer 의 `Portal:BaseUrl` 로 만든다. 운영 compose 가
`Portal__BaseUrl: https://portal.jsini.co.kr` 로 덮어쓰고 있다 — **메일함에서 여는
주소라 컨테이너망 이름이 아니라 바깥에서 보이는 도메인이어야 한다.**
비어 있으면 `http://localhost:5557` 이 그대로 나가 메일은 도착하는데 아무도 열지 못한다.
