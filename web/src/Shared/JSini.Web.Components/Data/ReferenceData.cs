using System.Security.Cryptography;
using System.Text;
using JSini.Web.Http;

namespace JSini.Web.Components.Data;

/// <summary>
/// 모듈이 참조자료 통을 쓰는 손잡이. <b>나눠 써도 되는지를 여기서 고른다.</b>
///
/// [왜 통을 직접 주지 않나]
///
/// <see cref="ReferenceDataStore"/> 는 싱글턴이라 사용자를 물을 수 없다
/// (<see cref="ITokenStore"/> 가 scoped 다 — 싱글턴에 넣으면 모든 사용자가
/// 한 사람의 토큰을 나눠 갖는다). 그래서 사람을 가르는 열쇠는 <b>회로 안에
/// 있는 이 껍데기</b>가 만든다.
///
/// 갈라 두면 부르는 자리에서 <b>둘 중 하나를 고르게</b> 되는 것이 덤이다.
/// 그 선택이 이 통의 유일한 위험이므로 눈에 보이는 편이 낫다.
/// </summary>
public sealed class ReferenceData(ReferenceDataStore store, ITokenStore tokens)
{
    /// <summary>
    /// <b>모두가 나눠 쓴다.</b> 사용자와 무관한 자료에만 쓴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「무관하다」는 것은 <b>백엔드가 지금 요청의 신원을 안 본다</b>는 뜻이고,
    /// 그것은 코드를 읽어야 알 수 있다. 부르는 자리에 근거를 적는다 —
    /// 「바뀔 것 같지 않다」는 근거가 아니다.
    /// </para>
    ///
    /// <para>
    /// 틀렸을 때 나는 일이 <b>남의 목록이 보이는 것</b>이라, 확신이 없으면
    /// <see cref="PerUserAsync"/> 를 쓴다. 그쪽도 업무 전환마다 다시 읽는
    /// 문제는 똑같이 없앤다 — 잃는 것은 사람 사이의 중복뿐이다.
    /// </para>
    /// </remarks>
    public Task<T?> SharedAsync<T>(string group, string key, Func<Task<T?>> load)
        where T : class
        => store.GetOrAddAsync(group, key, load);

    /// <summary>
    /// <b>사람마다 따로 담는다.</b> 백엔드가 신원을 볼 수 있는 자료에 쓴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 열쇠에 접근 토큰의 지문을 섞는다. 토큰 자체는 담지 않고, 다시 로그인해
    /// 토큰이 바뀌면 열쇠가 달라져 <b>저절로 새로 읽는다</b> —
    /// <see cref="Layout.PortalBootstrapStore.KeyFor"/> 와 같은 방식이다.
    /// </para>
    ///
    /// <para>
    /// 토큰이 없으면(로그인 전) 통을 쓰지 않고 매번 읽는다. 그 상태에서 부르는
    /// 참조자료는 없지만, 있다면 <b>모두가 한 칸을 나눠 쓰는 것</b>이 되므로
    /// 아예 담지 않는 편이 맞다.
    /// </para>
    /// </remarks>
    public async Task<T?> PerUserAsync<T>(string group, string key, Func<Task<T?>> load)
        where T : class
    {
        var token = await tokens.GetAccessTokenAsync();

        if (token is not { Length: > 0 })
        {
            return await load();
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var who = Convert.ToHexString(digest.AsSpan(0, 8));

        return await store.GetOrAddAsync(group, $"{who}/{key}", load);
    }

    /// <summary>
    /// 이 묶음을 버린다. 담을 때 어느 쪽을 썼든 함께 버려진다 —
    /// 세대가 묶음 단위이기 때문이다.
    /// </summary>
    public void Invalidate(string group) => store.Invalidate(group);

    /// <summary>
    /// <b>둘 이상의 업무 모듈이 함께 읽는</b> 참조자료의 묶음 이름.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 모듈 하나만 읽는 자료의 이름은 그 모듈 안에 둔다
    /// (<c>CommonCodeClient.Group</c> 처럼). 여기 올라오는 것은 <b>같은
    /// 엔드포인트를 두 모듈이 읽는 경우</b>뿐이고, 그때는 이름이 같아야
    /// 통을 나눠 쓴다.
    /// </para>
    ///
    /// <para>
    /// 이름을 같이 쓰면 <b>버리는 일이 함께 일어난다</b> — 한쪽에서
    /// <see cref="Invalidate"/> 하면 양쪽이 새로 읽는다. 각자 지으면 한쪽만
    /// 새 값을 보게 되고, 그 어긋남은 「어떤 화면에서만 옛 코드가 보인다」로
    /// 나타나 재현 조건을 찾기 나쁘다.
    /// </para>
    ///
    /// <para>
    /// <b>값 자체는 나눠 쓰지 못하는 경우가 있다.</b> 두 모듈이 같은 응답을
    /// 각자 자기 DTO 로 받으면(범용 셀렉트 설정이 그렇다) 담기는 형이 달라서
    /// 통 안에서 따로 앉는다 — 통이 열쇠에 형을 섞기 때문이다. 그러지 않으면
    /// 서로를 덮어쓴다(<c>ReferenceDataStore.Key</c> 머리말). 모듈은 서로를
    /// 참조하지 못하므로(규칙 2) 그 중복은 구조가 정한 값이고, 그래도 왕복은
    /// 사람 수·전환 수와 무관하게 <b>형마다 한 번</b>으로 줄어든다.
    /// </para>
    /// </remarks>
    public static class Groups
    {
        /// <summary>
        /// 포털 범용 셀렉트의 메타데이터 (<c>scom.biz_select_configs</c>).
        /// 프로젝트관리와 헬프데스크가 <c>auth/system/biz-select/configs</c> 를
        /// 각자 읽는다 — 같은 응답이다.
        /// </summary>
        public const string BizSelectConfigs = "portal.biz-select-configs";
    }
}
