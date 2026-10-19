namespace JSini.Web.Admin.Components.Pages;

public partial class PushGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/admin/push/send", "메시지 발송", "고른 사람에게 푸시·메일을 보낸다"),
        ("/admin/push/dashboard", "푸시 현황", "발송 건수 · 성공률 · 실패 사유"),
        ("/admin/push/logs", "발송 이력", "한 건씩 들여다본다"),
        ("/admin/push/history", "내 알림함", "내가 받은 알림"),
        ("/admin/mail/send", "메일 보내기", "사람에게 쓰는 편지 — 보낸 글이 내 보낸함에 남는다"),

        // 이 업무의 화면이 아니다 — 알림 설정은 장례식장 환경설정과 같은 판을 열어 그쪽으로 합쳤고, 길잡이는 그 자리를 가리킨다.
        ("/funeral/setting/environment", "내 알림 설정", "수신 스위치 · 이 브라우저 · 등록된 기기 — 환경설정으로 옮겼다"),
    ];
}
