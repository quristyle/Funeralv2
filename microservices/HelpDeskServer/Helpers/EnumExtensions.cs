using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace HelpDeskServer.Helpers;



/// <summary>
/// 열거형 확장 메서드
/// </summary>
public static class EnumExtensions {

  /// <summary>
  /// 디스플레이 이름 가져오기
  /// </summary>
  /// <param name="enumValue"></param>
  /// <returns></returns>
  public static string GetDisplayName(this Enum enumValue) {
    return enumValue.GetType()
                    .GetMember(enumValue.ToString())
                    .First()
                    .GetCustomAttribute<DisplayAttribute>()?
                    .GetName() ?? enumValue.ToString();
  }
}
