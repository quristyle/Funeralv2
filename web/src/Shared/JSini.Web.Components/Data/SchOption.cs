namespace JSini.Web.Components.Data;

/// <summary>조회 조건 고르개의 한 항목. DxComboBox 에 그대로 담는다. 익명 형식 대신 이름을 붙인 까닭은 접힌 조회줄에 값으로 이름을 되찾아 적어야 해서다 — 익명 형식은 밖에서 이름을 부를 수 없다. Value 가 null 을 받는 것은 「전체」 항목 때문이다.</summary>
/// <param name="Value">서버로 실어 보내는 값. 「전체」는 null.</param>
/// <param name="Text">사람이 읽는 이름. 접힌 조회줄에도 이 글자가 적힌다.</param>
public sealed record SchOption(string? Value, string Text);
