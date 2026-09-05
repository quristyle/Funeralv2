namespace JSini.Web.Abstractions;

/// <summary>
/// 화면 하나에 대해 따로 켜고 끄는 동작 권한.
/// <c>scom.system_menus</c> 의 <c>use_*</c> 컬럼과 일대일로 맞춘다.
///
/// Vue 의 <c>v-perm</c> 디렉티브가 하던 일을 여기서 이어받는다. 화면 안에서
/// 버튼 하나를 보일지 말지를 이 값으로 정한다 —
/// <c>&lt;PermissionView Action="MenuAction.Create"&gt;</c> 처럼 쓴다.
///
/// [보이지 않는 것과 막히는 것은 다르다]
///
/// 이 값으로 버튼을 감추는 것은 <b>정리</b>지 통제가 아니다. 실제 통제는 서버가 한다.
/// 감추기만 하고 서버에서 막지 않으면 "버튼이 없으니 안전하다" 는 착각이 남는다.
/// </summary>
public enum MenuAction
{
    /// <summary>화면 열람 (<c>use_view</c>). 라우트 진입 자체를 가른다.</summary>
    View,

    /// <summary>조회·검색 (<c>use_search</c>)</summary>
    Search,

    /// <summary>등록 (<c>use_create</c>)</summary>
    Create,

    /// <summary>수정 (<c>use_update</c>)</summary>
    Update,

    /// <summary>삭제 (<c>use_delete</c>)</summary>
    Delete,

    /// <summary>인쇄 (<c>use_print</c>)</summary>
    Print,

    /// <summary>엑셀 내려받기 (<c>use_excel</c>)</summary>
    Excel,

    /// <summary>화면마다 뜻이 다른 자리 1 (<c>use_cust1</c>)</summary>
    Cust1,

    /// <summary>화면마다 뜻이 다른 자리 2 (<c>use_cust2</c>)</summary>
    Cust2,

    /// <summary>화면마다 뜻이 다른 자리 3 (<c>use_cust3</c>)</summary>
    Cust3,

    /// <summary>화면마다 뜻이 다른 자리 4 (<c>use_cust4</c>)</summary>
    Cust4,

    /// <summary>화면마다 뜻이 다른 자리 5 (<c>use_cust5</c>)</summary>
    Cust5,

    /// <summary>화면마다 뜻이 다른 자리 6 (<c>use_cust6</c>)</summary>
    Cust6,

    /// <summary>화면마다 뜻이 다른 자리 7 (<c>use_cust7</c>)</summary>
    Cust7,

    /// <summary>화면마다 뜻이 다른 자리 8 (<c>use_cust8</c>)</summary>
    Cust8,
}
