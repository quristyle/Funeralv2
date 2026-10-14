using System.Reflection;
using JSini.Web.HelpDesk.Components.Pages;
using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class ProcedureResultTests
{
    private static readonly MethodInfo IsDateTimeTypeMethod =
        typeof(ProcedureResult).GetMethod("IsDateTimeType", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("IsDateTimeType method not found.");

    private static readonly MethodInfo IsDateTypeMethod =
        typeof(ProcedureResult).GetMethod("IsDateType", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("IsDateType method not found.");

    private static readonly Type ParameterType =
        typeof(ProcedureResult).GetNestedType("ProcedureParameter", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ProcedureParameter type not found.");

    private static bool InvokeIsDateTimeType(string? dataType) =>
        (bool)IsDateTimeTypeMethod.Invoke(null, [dataType])!;

    private static bool InvokeIsDateType(string? dataType) =>
        (bool)IsDateTypeMethod.Invoke(null, [dataType])!;

    [Theory]
    [InlineData("date", true, false)]
    [InlineData("DATE", true, false)]
    [InlineData("Date", true, false)]
    [InlineData("date(10)", true, false)]
    [InlineData("datetime", false, true)]
    [InlineData("DATETIME", false, true)]
    [InlineData("DateTime", false, true)]
    [InlineData("datetime2", false, true)]
    [InlineData("smalldatetime", false, true)]
    [InlineData("timestamp", false, true)]
    [InlineData("varchar", false, false)]
    [InlineData("int", false, false)]
    [InlineData("", false, false)]
    [InlineData(null, false, false)]
    public void 인자_자료형_판별_검사(string? dataType, bool expectedIsDate, bool expectedIsDateTime)
    {
        Assert.Equal(expectedIsDate, InvokeIsDateType(dataType));
        Assert.Equal(expectedIsDateTime, InvokeIsDateTimeType(dataType));
    }

    [Fact]
    public void ProcedureParameter_Date타입_날짜_설정시_yyyyMMdd_문자열_반환()
    {
        var param = Activator.CreateInstance(ParameterType)!;
        var dataTypeProp = ParameterType.GetProperty("DataType")!;
        var valueProp = ParameterType.GetProperty("Value")!;
        var dateValueProp = ParameterType.GetProperty("DateValue")!;

        dataTypeProp.SetValue(param, "date");

        dateValueProp.SetValue(param, new DateTime(2026, 10, 14, 15, 30, 45));
        var value = (string?)valueProp.GetValue(param);
        Assert.Equal("2026-10-14", value);

        var parsedDate = (DateTime?)dateValueProp.GetValue(param);
        Assert.NotNull(parsedDate);
        Assert.Equal(new DateTime(2026, 10, 14), parsedDate.Value);
    }

    [Fact]
    public void ProcedureParameter_DateTime타입_일시_설정시_yyyyMMdd_HHmmss_문자열_반환()
    {
        var param = Activator.CreateInstance(ParameterType)!;
        var dataTypeProp = ParameterType.GetProperty("DataType")!;
        var valueProp = ParameterType.GetProperty("Value")!;
        var dateValueProp = ParameterType.GetProperty("DateValue")!;

        dataTypeProp.SetValue(param, "datetime");

        dateValueProp.SetValue(param, new DateTime(2026, 10, 14, 15, 30, 45));
        var value = (string?)valueProp.GetValue(param);
        Assert.Equal("2026-10-14 15:30:45", value);

        var parsedDate = (DateTime?)dateValueProp.GetValue(param);
        Assert.NotNull(parsedDate);
        Assert.Equal(new DateTime(2026, 10, 14, 15, 30, 45), parsedDate.Value);
    }

    [Fact]
    public void ProcedureParameter_DateValue_null_설정시_Value도_null()
    {
        var param = Activator.CreateInstance(ParameterType)!;
        var valueProp = ParameterType.GetProperty("Value")!;
        var dateValueProp = ParameterType.GetProperty("DateValue")!;

        valueProp.SetValue(param, "2026-10-14");
        dateValueProp.SetValue(param, null);

        Assert.Null(valueProp.GetValue(param));
        Assert.Null(dateValueProp.GetValue(param));
    }
}
