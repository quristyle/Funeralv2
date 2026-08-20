using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

/// <summary>
/// 동적 객체 처리를 위한 헬퍼 클래스
/// </summary>
public static class DynamicHelper
{
    /// <summary>
    /// 객체를 속성 이름과 값의 딕셔너리로 변환합니다.
    /// </summary>
    /// <param name="obj">변환할 객체</param>
    /// <returns>속성 이름과 값으로 구성된 딕셔너리</returns>
    public static IDictionary<string, object?> ToDictionary(object obj)
    {
        var dict = new Dictionary<string, object?>();

        // DynamicClass 같은 경우
        if (obj.GetType().Name.StartsWith("DynamicClass"))
        {
            foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(obj))
            {
                dict[prop.Name] = prop.GetValue(obj);
            }
        }
        else
        {
            // 일반적인 익명 타입/POCO
            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                dict[prop.Name] = prop.GetValue(obj);
            }
        }

        return dict;
    }
}
