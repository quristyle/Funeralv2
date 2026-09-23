namespace JSini.Web.Components.Data;

/// <summary>
/// 조회 조건 고르개의 한 항목. <c>DxComboBox</c> 에 그대로 담는다 —
/// <c>TextFieldName="Text"</c> · <c>ValueFieldName="Value"</c>.
///
/// <para>
/// [익명 형식이던 것에 이름을 붙인 까닭]
/// </para>
///
/// <para>
/// 목록을 화면에 넘기는 데는 <c>new { Text = "완료", Value = "y" }</c> 로
/// 충분했다. 그런데 휴대폰에서 조회 판이 접히면서 <b>값으로 이름을 되찾는</b>
/// 자리가 생겼다(<see cref="SchSummary"/>) — 머리줄에 「y」가 아니라 「완료」를
/// 적어야 한다. 익명 형식은 밖에서 이름을 부를 수 없어 그 자리를 쓸 수 없다.
/// </para>
///
/// <para>
/// <see cref="Value"/> 가 <c>null</c> 을 받는 것은 <b>「전체」 항목</b> 때문이다.
/// 그 줄은 조건을 싣지 않는다는 뜻이라 값이 없다.
/// </para>
/// </summary>
/// <param name="Value">서버로 실어 보내는 값. 「전체」는 <c>null</c>.</param>
/// <param name="Text">사람이 읽는 이름. 접힌 조회줄에도 이 글자가 적힌다.</param>
public sealed record SchOption(string? Value, string Text);
