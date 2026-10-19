namespace JSini.Web.Abstractions;

/// <summary>
/// 화면을 가리키는 변하지 않는 열쇠. DB 메뉴가 URL 대신 이것을 든다.
/// 열쇠를 URL 에서 떼면 URL 은 코드가 소유하고(바꿔도 DB 는 모른다), 메뉴 관리 화면은 실려 있는 화면 목록에서 고르므로 없는 화면을 가리킬 방법이 없다.
/// 열쇠→URL 변환은 기동 때 만든 사전 조회라 라우팅 속도는 그대로다. 열쇠는 화면마다 정확히 하나이고 저장소 전체에서 유일하며 {모듈}. 으로 시작한다. 포괄 라우트에는 붙이지 않는다.
/// 한 번 정한 열쇠는 바꾸지 않는다. 바꾸면 DB 의 그 메뉴가 화면을 잃는다.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RouteKeyAttribute(string key) : Attribute
{
    /// <summary>화면을 가리키는 열쇠(<c>funeral.room-status</c>).</summary>
    public string Key { get; } = key;
}
