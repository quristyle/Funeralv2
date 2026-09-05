using System.Data;
using System.Text.Json;
using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 프로시저 결과를 그리드용 표로 옮기는 부분.
///
/// [여기가 프로젝트관리 이관의 급소다]
///
/// 화면 27개가 전부 이 변환을 거친다. 여기서 타입 하나를 잘못 옮기면
/// 그 컬럼을 쓰는 모든 화면에서 정렬이 이상해지거나 값이 빈다. 그런데 증상은
/// "어떤 화면의 어떤 컬럼만 이상하다" 로 나타나서 원인을 좇기 어렵다.
///
/// 값이 <see cref="JsonElement"/> 로 온다는 점이 특히 함정이다 — 행을
/// <c>Dictionary&lt;string, object?&gt;</c> 로 역직렬화하면 숫자도 날짜도
/// 전부 JsonElement 이고, 그대로 DataRow 에 넣으면 타입이 안 맞아 터진다.
/// </summary>
public sealed class ProjMngTableTests
{
    /// <summary>서버에서 오는 모양 그대로 만든다 — 값은 JsonElement 다.</summary>
    private static ProjMngRow Row(string json)
    {
        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var row = new ProjMngRow();
        foreach (var (key, value) in parsed)
        {
            row[key] = value;
        }
        return row;
    }

    private static ProjMngResult Result(Dictionary<string, string> cols, params ProjMngRow[] rows) =>
        new() { Cols = cols, Rows = [.. rows] };

    [Fact]
    public void 컬럼_메타의_순서를_그대로_지킨다()
    {
        var table = ProjMngTable.From(Result(new()
        {
            ["cm_cd"] = "System.String",
            ["cm_srt"] = "System.Int32",
            ["cm_nm"] = "System.String",
        }));

        // 프로시저가 정한 순서가 곧 화면 순서다. 사전을 다시 정렬하면 안 된다.
        Assert.Equal(["cm_cd", "cm_srt", "cm_nm"], table.ColumnOrder);
    }

    [Fact]
    public void 타입_이름을_실제_타입으로_옮긴다()
    {
        var table = ProjMngTable.From(Result(new()
        {
            ["s"] = "System.String",
            ["i"] = "System.Int32",
            ["l"] = "System.Int64",
            ["d"] = "System.Decimal",
            ["b"] = "System.Boolean",
            ["t"] = "System.DateTime",
        }));

        Assert.Equal(typeof(string), table.Table.Columns["s"]!.DataType);
        Assert.Equal(typeof(int), table.Table.Columns["i"]!.DataType);
        Assert.Equal(typeof(long), table.Table.Columns["l"]!.DataType);
        Assert.Equal(typeof(decimal), table.Table.Columns["d"]!.DataType);
        Assert.Equal(typeof(bool), table.Table.Columns["b"]!.DataType);
        Assert.Equal(typeof(DateTime), table.Table.Columns["t"]!.DataType);
    }

    /// <summary>
    /// 모르는 타입은 문자열로 둔다. 프로시저가 새 타입을 돌려주기 시작해도
    /// 그 컬럼만 문자열로 보일 뿐 화면은 뜬다 — 안 뜨는 것보다 낫다.
    /// </summary>
    [Fact]
    public void 모르는_타입은_문자열로_둔다()
    {
        var table = ProjMngTable.From(Result(new() { ["x"] = "System.SomethingNew" }));

        Assert.Equal(typeof(string), table.Table.Columns["x"]!.DataType);
    }

    [Fact]
    public void JsonElement_값을_컬럼_타입으로_바꾼다()
    {
        var table = ProjMngTable.From(Result(
            new()
            {
                ["s"] = "System.String",
                ["i"] = "System.Int32",
                ["b"] = "System.Boolean",
                ["t"] = "System.DateTime",
            },
            Row("""{"s":"가나","i":42,"b":true,"t":"2025-05-02T09:30:00"}""")));

        var row = table.Table.Rows[0];

        Assert.Equal("가나", row["s"]);
        Assert.Equal(42, row["i"]);
        Assert.Equal(true, row["b"]);
        Assert.Equal(new DateTime(2025, 5, 2, 9, 30, 0), row["t"]);
    }

    /// <summary>
    /// 프로시저가 불리언을 <c>"true"</c> · <c>"True"</c> · <c>"1"</c> 로 섞어 돌려준다.
    /// 셋 다 참이어야 한다 — 하나라도 놓치면 그 화면의 체크박스가 전부 꺼진 채 보인다.
    /// </summary>
    [Theory]
    [InlineData("\"true\"", true)]
    [InlineData("\"True\"", true)]
    [InlineData("\"1\"", true)]
    [InlineData("true", true)]
    [InlineData("\"false\"", false)]
    [InlineData("false", false)]
    public void 불리언은_문자열로_와도_읽는다(string json, bool expected)
    {
        var table = ProjMngTable.From(Result(
            new() { ["b"] = "System.Boolean" },
            Row($$"""{"b":{{json}}}""")));

        Assert.Equal(expected, table.Table.Rows[0]["b"]);
    }

    /// <summary>
    /// 바꾸지 못하는 값은 비운다. 한 칸이 이상하다고 화면 전체가 죽으면
    /// 사용자는 무엇이 문제인지 알 수 없다.
    /// </summary>
    [Fact]
    public void 바꾸지_못하는_값은_비운다()
    {
        var table = ProjMngTable.From(Result(
            new() { ["i"] = "System.Int32" },
            Row("""{"i":"숫자아님"}""")));

        Assert.Equal(DBNull.Value, table.Table.Rows[0]["i"]);
    }

    [Fact]
    public void null_과_없는_컬럼은_비운다()
    {
        var table = ProjMngTable.From(Result(
            new() { ["a"] = "System.String", ["b"] = "System.String" },
            Row("""{"a":null}""")));

        Assert.Equal(DBNull.Value, table.Table.Rows[0]["a"]);
        Assert.Equal(DBNull.Value, table.Table.Rows[0]["b"]);
    }

    /// <summary>
    /// <b>방금 읽은 자료는 "변경 없음" 이어야 한다.</b>
    ///
    /// AcceptChanges 를 안 하면 모든 행이 Added 로 남아, 사용자가 아무것도
    /// 고치지 않았는데 저장을 누르면 전량이 다시 서버로 나간다.
    /// </summary>
    [Fact]
    public void 조회_직후에는_변경이_없다()
    {
        var table = ProjMngTable.From(Result(
            new() { ["a"] = "System.String" },
            Row("""{"a":"1"}"""), Row("""{"a":"2"}""")));

        Assert.False(table.HasChanges);
        Assert.Empty(table.ChangedRows());
    }

    [Fact]
    public void 고친_행만_저장_대상이_된다()
    {
        var table = ProjMngTable.From(Result(
            new() { ["a"] = "System.String" },
            Row("""{"a":"1"}"""), Row("""{"a":"2"}""")));

        table.Table.Rows[1]["a"] = "고침";

        Assert.True(table.HasChanges);

        var changed = table.ChangedRows();
        Assert.Single(changed);
        Assert.Equal("고침", changed[0]["a"]);
    }

    [Fact]
    public void 더한_행도_저장_대상이_된다()
    {
        var table = ProjMngTable.From(Result(new() { ["a"] = "System.String" }));

        var row = table.Table.NewRow();
        row["a"] = "새것";
        table.Table.Rows.Add(row);

        var changed = table.ChangedRows();
        Assert.Single(changed);
        Assert.Equal("새것", changed[0]["a"]);
    }

    /// <summary>
    /// 저장 뒤 표시를 지우지 않으면 다음 저장 때 같은 행이 또 나간다.
    /// 다시 조회하지 않는 화면에서 실제로 문제가 된다.
    /// </summary>
    [Fact]
    public void AcceptChanges_뒤에는_저장_대상이_없다()
    {
        var table = ProjMngTable.From(Result(
            new() { ["a"] = "System.String" }, Row("""{"a":"1"}""")));

        table.Table.Rows[0]["a"] = "고침";
        Assert.True(table.HasChanges);

        table.AcceptChanges();

        Assert.False(table.HasChanges);
        Assert.Empty(table.ChangedRows());
    }

    /// <summary>
    /// 저장 대상에서 <c>DBNull</c> 은 <c>null</c> 로 나가야 한다.
    /// DBNull 을 그대로 직렬화하면 <c>{}</c> 가 되어 프로시저가 빈 객체를 받는다.
    /// </summary>
    [Fact]
    public void 저장_대상의_빈_값은_null_로_나간다()
    {
        var table = ProjMngTable.From(Result(new() { ["a"] = "System.String" }));

        table.Table.Rows.Add(table.Table.NewRow());

        Assert.Null(table.ChangedRows()[0]["a"]);
    }

    [Fact]
    public void 컬럼_메타가_없으면_빈_표다()
    {
        Assert.Empty(ProjMngTable.From(new ProjMngResult()).ColumnOrder);
        Assert.Empty(ProjMngTable.From(ProjMngResult.Empty).ColumnOrder);
    }
}
