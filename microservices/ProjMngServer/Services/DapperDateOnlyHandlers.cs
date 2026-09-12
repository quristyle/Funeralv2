using System.Data;
using Dapper;

namespace ProjMngServer.Services;

/// <summary>
/// Dapper 가 <see cref="DateOnly"/> 를 <b>파라미터로</b> 다루게 한다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 필요한가 — 실제로 밟았다]
/// </para>
///
/// <para>
/// PostgreSQL 의 <c>date</c> 칸을 <see cref="DateOnly"/> 로 <b>읽는 것</b>은
/// Npgsql 이 알아서 한다. 그런데 <b>쓰는 것</b>은 Dapper 를 먼저 지나는데,
/// Dapper 는 아는 타입 표에서 <c>DbType</c> 을 찾다가 못 찾으면 그 자리에서
/// 던진다.
/// </para>
///
/// <code>
/// System.NotSupportedException:
///   The member PrjEdt of type System.DateOnly cannot be used as a parameter value
/// </code>
///
/// <para>
/// <b>조회는 멀쩡하고 저장만 죽는다</b>는 것이 이 오류의 나쁜 점이다. 화면을
/// 열어 목록을 보는 데까지는 아무 표시가 없다가, 저장을 누르는 순간
/// 「서버 내부 오류」가 난다.
/// </para>
///
/// <para>
/// [왜 <c>DateTime</c> 으로 바꿔 쓰지 않나]
/// </para>
///
/// <para>
/// 그러면 DTO 가 <c>DateTime</c> 이 되고, 시각이 없는 값에 <c>00:00:00</c> 이
/// 따라다닌다. 옛 화면이 <c>yyyy-MM-dd</c> 로 <b>잘라 보여 주던</b> 것이
/// 그래서였다 — 자료형이 맞으면 자를 일이 없다. 그릇을 바꾸는 대신 통로를
/// 하나 놓는다.
/// </para>
///
/// <para>
/// [등록하는 곳이 한 곳이다]
/// </para>
///
/// <para>
/// <c>Program.cs</c> 에서 <see cref="Register"/> 를 한 번 부른다. 서비스마다
/// 부르면 빠뜨리는 서비스가 생기고, 그 서비스의 저장만 죽는다.
/// </para>
/// </remarks>
public static class DapperDateOnlyHandlers
{
    /// <summary>기동할 때 한 번 부른다.</summary>
    public static void Register()
    {
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyHandler());
    }

    private sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;

            // Npgsql 은 DateOnly 를 그대로 받는다. DbType 만 알려 주면 된다 —
            // DateTime 으로 바꾸면 시간대 처리가 한 겹 더 끼어든다.
            parameter.Value = value;
        }

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly date => date,
            DateTime time => DateOnly.FromDateTime(time),
            string text => DateOnly.Parse(text),
            _ => throw new InvalidCastException($"{value?.GetType().Name} 을(를) DateOnly 로 읽을 수 없습니다."),
        };
    }

    /// <summary>
    /// <c>DateOnly?</c> 는 <b>따로 등록해야 한다.</b> Dapper 는 널 허용 타입을
    /// 바탕 타입의 처리기로 자동으로 풀어 주지 않는다 — 한쪽만 등록하면
    /// 「비어 있는 날짜」를 보낼 때만 같은 예외가 난다.
    /// </summary>
    private sealed class NullableDateOnlyHandler : SqlMapper.TypeHandler<DateOnly?>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly? value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.HasValue ? value.Value : DBNull.Value;
        }

        public override DateOnly? Parse(object value) => value switch
        {
            null or DBNull => null,
            DateOnly date => date,
            DateTime time => DateOnly.FromDateTime(time),
            string text => DateOnly.Parse(text),
            _ => throw new InvalidCastException($"{value.GetType().Name} 을(를) DateOnly 로 읽을 수 없습니다."),
        };
    }
}
