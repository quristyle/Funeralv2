namespace JSini.Web.Abstractions;

/// <summary>화면 하나에 대해 따로 켜고 끄는 동작 권한. scom.system_menus 의 use_* 칸과 일대일로 맞춘다. 버튼을 감추는 것은 정리일 뿐 통제가 아니다. 실제 통제는 서버가 한다.</summary>
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
