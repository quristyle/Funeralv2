namespace JSini.Web.Admin.Components.Pages;

public partial class PushGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/admin/push/send", "메시지 발송", "고른 사람에게 푸시·메일을 보낸다"),
        ("/admin/push/dashboard", "푸시 현황", "발송 건수 · 성공률 · 실패 사유"),
        ("/admin/push/logs", "발송 이력", "한 건씩 들여다본다"),
        ("/admin/push/history", "내 알림함", "내가 받은 알림"),

        // **이 업무의 화면이 아니다.** 여기 있던 「알림 설정」은 장례식장
        // 「환경설정」과 같은 판을 열고 있어서 그쪽 하나로 합쳤다. 길잡이에서
        // 빼 버리면 「알림 관리에 있던 내 설정이 없어졌다」가 되므로 새 자리를
        // 가리켜 둔다(GroupLinks 는 권한으로 거르지 않는다 — 그 부품 머리말).
        ("/funeral/setting/environment", "내 알림 설정", "수신 스위치 · 이 브라우저 · 등록된 기기 — 환경설정으로 옮겼다"),
    ];
}
