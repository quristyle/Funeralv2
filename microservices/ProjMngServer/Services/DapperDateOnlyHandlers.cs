using System.Data;
using Dapper;

namespace ProjMngServer.Services;

/// <summary>
/// Dapper 가 <see cref="DateOnly"/> 를 <b>파라미터로</b> 다루게 한다.
/// </summary>
/// Npgsql 은 DateOnly 를 읽고 쓰지만 Dapper 는 파라미터로 쓸 때 DbType 을 못 찾아 NotSupportedException 을 던진다 — 조회는 멀쩡하고 저장만 죽는다.
/// DateTime 으로 바꾸면 00:00:00 이 따라다니므로 그릇을 바꾸지 않고 통로를 놓는다. 등록은 Program.cs 에서 한 번만 부른다(서비스마다 부르면 빠뜨린 서비스의 저장만 죽는다).
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
