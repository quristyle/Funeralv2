using System.Security.Cryptography;
using System.Text;
using JSini.Web.Http;

namespace JSini.Web.Components.Data;

/// <summary>모듈이 참조자료 통을 쓰는 손잡이. 나눠 써도 되는지를 여기서 고른다. 통(ReferenceDataStore)은 싱글턴이라 사용자를 물을 수 없다 — 사람을 가르는 열쇠는 회로 안의 이 껍데기가 만든다. 갈라 두면 부르는 자리에서 둘 중 하나를 고르게 되는데, 그 선택이 이 통의 유일한 위험이라 눈에 보이게 둔다.</summary>
public sealed class ReferenceData(ReferenceDataStore store, ITokenStore tokens)
{
    /// <summary>모두가 나눠 쓴다. 사용자와 무관한 자료에만 쓴다 — 무관하다는 것은 백엔드가 지금 요청의 신원을 안 본다는 뜻이라 코드를 읽어 근거를 부르는 자리에 적는다. 틀리면 남의 목록이 보이므로 확신이 없으면 PerUserAsync 를 쓴다.</summary>
    public Task<T?> SharedAsync<T>(string group, string key, Func<Task<T?>> load)
        where T : class
        => store.GetOrAddAsync(group, key, load);

    /// <summary>사람마다 따로 담는다. 백엔드가 신원을 볼 수 있는 자료에 쓴다. 열쇠에 접근 토큰의 지문을 섞어 다시 로그인해 토큰이 바뀌면 저절로 새로 읽는다(PortalBootstrapStore.KeyFor 와 같다). 토큰이 없으면(로그인 전) 통을 쓰지 않고 매번 읽는다 — 모두가 한 칸을 나눠 쓰게 되기 때문이다.</summary>
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

    /// <summary>이 묶음을 버린다. 담을 때 어느 쪽을 썼든 함께 버려진다 — 세대가 묶음 단위이기 때문이다.</summary>
    public void Invalidate(string group) => store.Invalidate(group);

    /// <summary>둘 이상의 업무 모듈이 함께 읽는 참조자료의 묶음 이름. 모듈 하나만 읽는 자료의 이름은 그 모듈 안에 둔다. 같은 엔드포인트를 두 모듈이 읽을 때는 이름이 같아야 통을 나눠 쓴다 — 이름을 같이 쓰면 버리는 일이 함께 일어나고, 각자 지으면 한쪽만 새 값을 본다. 값 자체는 형이 달라(각자 DTO) 통 안에서 따로 앉는다 — 열쇠에 형을 섞기 때문이며, 그러지 않으면 서로를 덮어쓴다(ReferenceDataStore.Key 머리말). 왕복은 사람 수와 무관하게 형마다 한 번이다.</summary>
    public static class Groups
    {
        /// <summary>포털 범용 셀렉트의 메타데이터(scom.biz_select_configs). 프로젝트관리와 헬프데스크가 같은 응답을 각자 읽는다.</summary>
        public const string BizSelectConfigs = "portal.biz-select-configs";
    }
}
