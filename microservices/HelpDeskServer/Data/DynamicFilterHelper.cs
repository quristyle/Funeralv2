using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core; // NuGet: System.Linq.Dynamic.Core
using System.Linq.Dynamic.Core.Parser;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace HelpDeskServer.Data {
  /// <summary>QueryString 기반 동적 필터·정렬·페이징·그룹·집계·HAVING 을 IQueryable 에 적용한다.</summary>
  public static class DynamicFilterHelper {
    // 민감 정보 필드(항상 제외)
    private static readonly HashSet<string> SensitiveFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
            "passwordHash"
        };
    // 지원 연산자: 접미사 _neq·_gt·_gte·_lt·_lte·_like(포함)·_left·_right·_in(|)·_between·_isnull, 그리고 groupBy·aggregate·having·orderBy·select·page.

    /// <summary>필터 → 그룹/집계 → having → 정렬 → 페이징 순서로 전체를 적용한다.</summary>
    public static IQueryable ApplyAll<T>(this IQueryable<T> query, IQueryCollection qs) {
      // 1) WHERE 필터 적용

      query = ApplyWhereFilters(query, qs);

      // 2) GROUP BY + AGGREGATE 처리 (있다면)
      if (qs.TryGetValue("groupBy", out var gb) && !string.IsNullOrWhiteSpace(gb)) {
        var grouped = ApplyGroupingAndAggregation(query, qs);
        // 3) HAVING (on grouped)
        if (qs.TryGetValue("having", out var having) && !string.IsNullOrWhiteSpace(having)) {
          grouped = grouped.Where(having.ToString());
        }

        // 4) select/remove projection (optional) - Grouping 결과에 대해서는 select만 지원
        grouped = ApplyProjection(grouped, qs);
        // 5) ordering on grouped results
        if (qs.TryGetValue("orderBy", out var order) && !string.IsNullOrWhiteSpace(order)) {
          grouped = grouped.OrderBy(order.ToString());
        }

        // 6) paging on grouped
        grouped = ApplyPaging(grouped, qs);

        return grouped;
      }
      else {
        // No group -> projection, order, paging on T
        IQueryable result = query;

        result = ApplyProjection(result, qs, typeof(T));
        // orderBy
        if (qs.TryGetValue("orderBy", out var orderByVal) && !string.IsNullOrWhiteSpace(orderByVal)) {
          result = result.OrderBy(orderByVal.ToString());
        }

        // paging
        result = ApplyPaging(result, qs);

        return result;
      }
    }

    /// <summary>select 또는 remove 파라미터로 프로젝션을 적용한다.</summary>
    private static IQueryable ApplyProjection(IQueryable query, IQueryCollection qs, Type? entityType = null) {
      // 1. select가 우선순위가 가장 높음
      if (qs.TryGetValue("select", out var selectVal) && !string.IsNullOrWhiteSpace(selectVal)) {
        // remove와 함께 들어온 경우, select 결과에서 항목/하위 항목을 제거해야 함
        var selectTokensOriginal = selectVal.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var selectTokens = new List<string>(selectTokensOriginal);

        // select 토큰에서 민감필드 및 그 하위 토큰 제거 (예: passwordHash, user.passwordHash)
        selectTokens = selectTokens
            .Where(t => {
              // 토큰이 정확히 민감필드명이거나, ".민감필드"로 끝나는 중첩 토큰인 경우 제외
              if (SensitiveFieldNames.Contains(t)) return false;
              foreach (var s in SensitiveFieldNames) {
                if (t.EndsWith("." + s, StringComparison.OrdinalIgnoreCase)) return false;
              }
              return true;
            })
            .ToList();


        if (qs.TryGetValue("remove", out var removeVal2) && !string.IsNullOrWhiteSpace(removeVal2) && entityType != null) {
          // remove 파싱: 최상위 키 제거와 중첩 제거를 분리
          var removeParts = removeVal2.ToString()
              .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

          var topLevelRemoves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
          var nestedRemoves = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

          foreach (var part in removeParts) {
            var idx = part.IndexOf('.')
; if (idx > 0) {
              var parent = part.Substring(0, idx);
              var child = part.Substring(idx + 1);
              if (!nestedRemoves.TryGetValue(parent, out var set)) {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                nestedRemoves[parent] = set;
              }
              set.Add(child);
            }
            else {
              topLevelRemoves.Add(part);
            }
          }

          // 1) select 토큰에서 최상위 제거 항목은 아예 토큰 자체를 제외
          selectTokens = selectTokens
              .Where(t => !topLevelRemoves.Contains(t))
              .ToList();

          // 2) 중첩 제거: 예) admin.photo 제거 요청이 있고 select에 'admin'이 있을 때

          for (int i = 0; i < selectTokens.Count; i++) {
            var token = selectTokens[i];
            if (nestedRemoves.TryGetValue(token, out var childrenToRemove)) {
              // parent 타입 메타데이터 추출
              var parentType = GetNestedPropertyType(entityType, token);
              if (parentType == null) continue;

              // select에 명시적으로 포함된 하위 토큰 수집 (예: admin.userName)
              var explicitChildren = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
              for (int t = 0; t < selectTokens.Count; t++) {
                var tk = selectTokens[t];
                if (tk.StartsWith(token + ".", StringComparison.OrdinalIgnoreCase)) {
                  var child = tk.Substring(token.Length + 1);
                  if (!string.IsNullOrWhiteSpace(child)) explicitChildren.Add(child);
                }
              }

              // parent의 단순형 속성 + select에 명시된 하위 속성만 포함, 제거 대상은 제외
              var childProps = new List<string>();
              foreach (var p in parentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if (!p.CanRead) continue;
                var name = p.Name;
                if (childrenToRemove.Contains(name)) continue;
                if (SensitiveFieldNames.Contains(name)) continue; // 민감 필드 제외

                if (explicitChildren.Contains(name) || IsSimpleType(p.PropertyType)) {
                  childProps.Add(name);
                }
              }

              // selectTokens에서 명시적 하위 토큰은 제거 (중첩 new로 대체됨)
              selectTokens.RemoveAll(s => s.StartsWith(token + ".", StringComparison.OrdinalIgnoreCase));

              if (childProps.Count == 0) {
                // 제거 후 남는 속성이 없으면, 전체 parent를 select에서 제거
                selectTokens.RemoveAt(i);
                i--;
                continue;
              }

              // 중첩 new 로 재작성: 
              //  - 참조형: parent.child as child
              //  - 값형(Non-Nullable): IIF(parent == null, null, parent.child) as child  => null-safe
              var nested = new StringBuilder();
              nested.Append("new (");
              for (int c = 0; c < childProps.Count; c++) {
                if (c > 0) nested.Append(", ");
                var child = childProps[c];
                var childPropInfo = parentType.GetProperty(child, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (childPropInfo != null) {
                  var childType = childPropInfo.PropertyType;
                  var effectiveType = Nullable.GetUnderlyingType(childType) ?? childType;
                  if (effectiveType.IsValueType && Nullable.GetUnderlyingType(childType) == null) {
                    // Non-nullable value type → null-safe로 승격
                    nested.Append($"IIF({token} == null, null, {token}.{child}) as {child}");
                  }
                  else {
                    nested.Append($"{token}.{child} as {child}");
                  }
                }
                else {
                  nested.Append($"{token}.{child} as {child}");
                }
              }
              nested.Append(") as ").Append(token);

              selectTokens[i] = nested.ToString();
            }
          }

        }


        var finalSelect = string.Join(", ", selectTokens);
        return query.Select($"new ({finalSelect})");
      }

      // 2. select가 없고 remove가 있을 경우 (entityType이 제공되어야 함)
      if (entityType != null && qs.TryGetValue("remove", out var removeVal) && !string.IsNullOrWhiteSpace(removeVal)) {
        var propertiesToRemove = removeVal.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allProperties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        var propertiesToSelect = allProperties
            .Where(p => !propertiesToRemove.Contains(p.Name) && !SensitiveFieldNames.Contains(p.Name))
            .Select(p => p.Name);

        if (propertiesToSelect.Any()) {
          var selectString = "new (" + string.Join(", ", propertiesToSelect) + ")";
          return query.Select(selectString);
        }
      }

      // 3. 둘 다 없으면 원본 IQueryable 반환하되, entityType이 있으면 민감 필드만 제거한 프로젝션 적용
      if (entityType != null) {
        var safeProps = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !SensitiveFieldNames.Contains(p.Name))
            .Select(p => p.Name)
            .ToList();
        if (safeProps.Any()) {
          var selectString = "new (" + string.Join(", ", safeProps) + ")";
          return query.Select(selectString);
        }
      }
      return query;
    }





    /// <summary>QueryString 의 where 필터를 파싱해 Where 절로 적용한다.</summary>
    private static IQueryable<T> ApplyWhereFilters<T>(IQueryable<T> query, IQueryCollection qs) {
      var whereParts = new List<string>();
      var parameters = new List<object>();

      var orGroupParts = new List<string>();

      int paramIndex = 0;

      foreach (var kv in qs) {
        var key = kv.Key;
        var rawVal = kv.Value.ToString();
        if (string.IsNullOrWhiteSpace(rawVal)) continue;

        // skip special keys handled elsewhere
        if (IsSpecialKey(key)) continue;





        Console.WriteLine($" xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx key : {key}");






        // OR 그룹 처리

        bool isOrGroup = false;
        // support both "_or_field_like" and "field_or_like" forms
        if (key.IndexOf("_or_", StringComparison.OrdinalIgnoreCase) >= 0) {
          isOrGroup = true;
          // replace "_or_" with single "_" then remove leading "_" if present
          key = key.Replace("_or_", "_", StringComparison.OrdinalIgnoreCase);
          if (key.StartsWith("_")) key = key.Substring(1);
        }



        // suffix parsing
        var op = "eq";
        string prop = key;

        if (key.EndsWith("_like", StringComparison.OrdinalIgnoreCase)) { op = "like"; prop = key[..^5]; }
        else if (key.EndsWith("_left", StringComparison.OrdinalIgnoreCase)) { op = "startswith"; prop = key[..^5]; }
        else if (key.EndsWith("_right", StringComparison.OrdinalIgnoreCase)) { op = "endswith"; prop = key[..^6]; }
        else if (key.EndsWith("_neq", StringComparison.OrdinalIgnoreCase)) { op = "neq"; prop = key[..^4]; }
        else if (key.EndsWith("_gt", StringComparison.OrdinalIgnoreCase)) { op = "gt"; prop = key[..^3]; }
        else if (key.EndsWith("_lt", StringComparison.OrdinalIgnoreCase)) { op = "lt"; prop = key[..^3]; }
        else if (key.EndsWith("_gte", StringComparison.OrdinalIgnoreCase)) { op = "gte"; prop = key[..^4]; }
        else if (key.EndsWith("_lte", StringComparison.OrdinalIgnoreCase)) { op = "lte"; prop = key[..^4]; }
        else if (key.EndsWith("_in", StringComparison.OrdinalIgnoreCase)) { op = "in"; prop = key[..^3]; }
        else if (key.EndsWith("_nin", StringComparison.OrdinalIgnoreCase)) { op = "nin"; prop = key[..^4]; }
        else if (key.EndsWith("_between", StringComparison.OrdinalIgnoreCase)) { op = "between"; prop = key[..^8]; }
        else if (key.EndsWith("_isnull", StringComparison.OrdinalIgnoreCase)) { op = "isnull"; prop = key[..^7]; }

        // Validate property exists on T (ignore if not) - MODIFIED to support nested properties
        var propType = GetNestedPropertyType(typeof(T), prop);
        if (propType == null) continue;

        // Build expression string for Dynamic LINQ
        switch (op) {
          case "eq":
          case "neq":
          case "gt":
          case "lt":
          case "gte":
          case "lte": {
              // convert simple type
              if (!TryConvertToType(rawVal, propType, out object? conv))
                continue;

              string symbol = op switch {
                "eq" => "==",
                "neq" => "!=",
                "gt" => ">",
                "lt" => "<",
                "gte" => ">=",
                "lte" => "<=",
                _ => "=="
              };

              if (isOrGroup) {
                orGroupParts.Add($"{prop} {symbol} @{paramIndex}");
                // add parameter immediately so parameter index matches placeholder
                parameters.Add(conv);

              }
              else {
                whereParts.Add($"{prop} {symbol} @{paramIndex}");
                parameters.Add(conv);
              }

              paramIndex++;
              break;
            }

          case "like": {
              // string contains
              if (propType != typeof(string)) continue;
              var part = $"{prop} != null AND {prop}.ToLower().Contains(@{paramIndex})";
              if (isOrGroup) {
                orGroupParts.Add(part);
                parameters.Add(rawVal.ToLower());
              }
              else {
                whereParts.Add(part);
                parameters.Add(rawVal.ToLower());
              }
              paramIndex++;
              break;
            }
          case "startswith": {
              if (propType != typeof(string)) continue;
              var part = $"{prop} != null AND {prop}.StartsWith(@{paramIndex})";
              if (isOrGroup) {
                orGroupParts.Add(part);
                parameters.Add(rawVal);
              }
              else {
                whereParts.Add(part);
                parameters.Add(rawVal);
              }
              paramIndex++;
              break;
            }
          case "endswith": {
              if (propType != typeof(string)) continue;
              var part = $"{prop} != null AND {prop}.EndsWith(@{paramIndex})";
              whereParts.Add(part); // OR 그룹에서는 잘 사용되지 않으므로 기본 그룹에만 추가
              parameters.Add(rawVal);
              paramIndex++;
              break;
            }
          case "in":
          case "nin": {
              // split by | and convert elements
              var parts = rawVal.Split('|', StringSplitOptions.RemoveEmptyEntries);
              var list = new List<object?>();
              foreach (var p in parts) {
                if (TryConvertToType(p, propType, out var cv)) {
                  list.Add(cv);
                }
              }
              if (list.Count == 0) continue;

              var part = op == "in" ? $"@{paramIndex}.Contains({prop})" : $"!@{paramIndex}.Contains({prop})";

              var typedArray = Array.CreateInstance(propType, list.Count);
              for (int i = 0; i < list.Count; i++) {
                typedArray.SetValue(list[i], i);
              }


              // add parameters immediately for consistent indexing; IN in OR group will be treated same
              whereParts.Add(part);
              parameters.Add(typedArray);

              paramIndex++;
              break;
            }
          case "between": {
              var parts = rawVal.Split('|', StringSplitOptions.RemoveEmptyEntries);
              if (parts.Length != 2) continue;
              if (!TryConvertToType(parts[0], propType, out var lo)) continue;
              if (!TryConvertToType(parts[1], propType, out var hi)) continue;
              var part = $"{prop} >= @{paramIndex} AND {prop} <= @{paramIndex + 1}";
              if (isOrGroup) {
                orGroupParts.Add(part);
                parameters.Add(lo);
                parameters.Add(hi);
              }
              else {
                whereParts.Add(part);
                parameters.Add(lo);
                parameters.Add(hi);
              }
              paramIndex += 2;
              break;
            }
          case "isnull": {
              var flag = rawVal.Trim().ToLower();
              if (flag == "true" || flag == "1") {
                if (isOrGroup) orGroupParts.Add($"{prop} == null");
                else whereParts.Add($"{prop} == null");
              }
              else {
                if (isOrGroup) orGroupParts.Add($"{prop} != null");
                else whereParts.Add($"{prop} != null");
              }
              break;
            }
        }
      }



      if (orGroupParts.Any()) {
        whereParts.Add($"({string.Join(" OR ", orGroupParts)})");
        // parameters were already added in loop to maintain correct indexing
      }


      if (whereParts.Count == 0) return query;

      var whereClause = string.Join(" AND ", whereParts);

      return query.Where(whereClause, parameters.ToArray());
    }

    /// <summary>groupBy + aggregate 를 처리한다. 결과는 dynamic IQueryable 이고, 집계 값은 Count_Id · Sum_Amount 같은 별칭으로 나온다.</summary>
    private static IQueryable ApplyGroupingAndAggregation<T>(IQueryable<T> query, IQueryCollection qs) {
      var groupByFields = qs["groupBy"].ToString()
          .Split(',', StringSplitOptions.RemoveEmptyEntries)
          .Select(s => s.Trim())
          .Where(s => !string.IsNullOrEmpty(s))
          .ToArray();

      if (groupByFields.Length == 0) return query;

      // Build key selector string for Dynamic LINQ: "new (Field1, Field2 as Alias2)"
      var keyBuilder = new StringBuilder("new (");
      for (int i = 0; i < groupByFields.Length; i++) {
        if (i > 0) keyBuilder.Append(", ");
        var f = groupByFields[i];
        keyBuilder.Append(f);
      }
      keyBuilder.Append(")");

      // Generate grouped IQueryable: .GroupBy("new (Field1, Field2)", "it")
      var grouped = query.GroupBy(keyBuilder.ToString(), "it");

      // Parse aggregate
      var aggregateList = new List<(string func, string field, string alias)>();
      if (qs.TryGetValue("aggregate", out var agg) && !string.IsNullOrWhiteSpace(agg)) {
        var parts = agg.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts) {
          // form: Func:Field or Func:*
          var p = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
          if (p.Length != 2) continue;
          var func = p[0].Trim();
          var field = p[1].Trim();
          var alias = $"{func}_{field}".Replace(".", "_");
          aggregateList.Add((func, field, alias));
        }
      }

      // Build projection: new (Key.Field1 as Field1, Key.Field2 as Field2, Count(it) as Count_Id, Sum(it.Amount) as Sum_Amount, ...)
      var projBuilder = new StringBuilder("new (");
      // add grouped key members
      for (int i = 0; i < groupByFields.Length; i++) {
        if (i > 0) projBuilder.Append(", ");
        var f = groupByFields[i];
        // Key.FieldName
        projBuilder.Append($"Key.{f} as {f}");
      }

      // add aggregates
      foreach (var ag in aggregateList) {
        if (projBuilder.Length > 5) projBuilder.Append(", ");
        var func = ag.func.ToLower();
        var field = ag.field;

        if (field == "*" || field == "1") {
          if (func == "count")
            projBuilder.Append($"Count() as {ag.alias}");
          else
            continue;
        }
        else {
          // 집계는 그룹 항목 위에서 호출된다(it.Amount 형태).
          var expr = func switch {
            "count" => $"Count(it.{field})", // Count(it.Field)
            "sum" => $"Sum(it.{field})",
            "avg" or "average" => $"Average(it.{field})",
            "max" => $"Max(it.{field})",
            "min" => $"Min(it.{field})",
            _ => null
          };
          if (expr != null)
            projBuilder.Append($"{expr} as {ag.alias}");
        }
      }

      projBuilder.Append(")");

      // Select on grouped
      var selected = grouped.Select(projBuilder.ToString()); // IQueryable<dynamic>

      return selected;
    }

    /// <summary>페이징. page 는 1부터, pageSize=0 이면 페이징하지 않는다.</summary>
    private static IQueryable ApplyPaging(IQueryable query, IQueryCollection qs) {
      int page = 1;
      int pageSize = 0;
      if (qs.TryGetValue("page", out var pVal) && int.TryParse(pVal, out var p)) page = Math.Max(1, p);
      if (qs.TryGetValue("pageSize", out var psVal) && int.TryParse(psVal, out var ps)) pageSize = Math.Max(0, ps);

      if (pageSize > 0) {
        int skip = (page - 1) * pageSize;
        query = query.Skip(skip).Take(pageSize);
      }

      return query;
    }

    /// <summary>특수 제어 키(다른 곳에서 처리)인지 판정한다.</summary>
    private static bool IsSpecialKey(string key) {
      return key.Equals("groupBy", StringComparison.OrdinalIgnoreCase)
          || key.Equals("aggregate", StringComparison.OrdinalIgnoreCase)
          || key.Equals("having", StringComparison.OrdinalIgnoreCase)
          || key.Equals("orderBy", StringComparison.OrdinalIgnoreCase)
          || key.Equals("page", StringComparison.OrdinalIgnoreCase)
          || key.Equals("pageSize", StringComparison.OrdinalIgnoreCase)
          || key.Equals("select", StringComparison.OrdinalIgnoreCase)
          || key.Equals("remove", StringComparison.OrdinalIgnoreCase);
    }

    private static Type? GetNestedPropertyType(Type type, string propertyName) {
      Type? currentType = type;
      foreach (var part in propertyName.Split('.')) {
        if (currentType == null) return null;
        var propInfo = currentType.GetProperty(part, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (propInfo == null) return null;
        currentType = propInfo.PropertyType;
      }
      return currentType;
    }

    private static bool IsSimpleType(Type type) {
      var t = Nullable.GetUnderlyingType(type) ?? type;
      return t.IsPrimitive
          || t.IsEnum
          || t == typeof(string)
          || t == typeof(decimal)
          || t == typeof(DateTime)
          || t == typeof(Guid)
          || t == typeof(DateOnly)
          || t == typeof(TimeOnly);
    }

    /// <summary>원본 문자열을 대상 타입으로 바꾼다. Nullable&lt;&gt; 과 enum 을 지원한다.</summary>

    // true 를 돌려줄 때 converted 가 채워진다는 약속(호출부의 null 경고를 없앤다).
    private static bool TryConvertToType(string raw, Type targetType, [NotNullWhen(true)] out object? converted) {
      converted = null;
      try {
        var destType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (destType.IsEnum) {
          converted = Enum.Parse(destType, raw, ignoreCase: true);
          return true;
        }

        if (destType == typeof(Guid)) {
          converted = Guid.Parse(raw);
          return true;
        }

        if (destType == typeof(bool)) {
          if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase)) {
            converted = true; return true;
          }
          if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase)) {
            converted = false; return true;
          }
        }

        // 시간대가 붙은 날짜는 UTC 로 굳힌다. 그냥 TryParse 하면 Kind=Local 이 되어 timestamptz 질의가 500 으로 터진다(docs/utc-time.md).
        // 시간대 없는 글자는 Unspecified 로 두면 Npgsql 이 UTC 로 친다.
        if (destType == typeof(DateTime)) {
          if (HasTimeZone(raw)
              && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)) {
            converted = dto.UtcDateTime; // Kind = Utc
            return true;
          }

          if (DateTime.TryParse(raw, out var dt)) {
            converted = dt;
            return true;
          }
        }

        // numeric types
        converted = Convert.ChangeType(raw, destType);
        return converted is not null;
      }
      catch {
        return false;
      }
    }

    /// <summary>날짜 글자에 시간대가 붙었는지(끝 Z 또는 시각 뒤 ±hh:mm). 날짜 구분자를 음수 오프셋으로 읽지 않도록 시각이 시작된 뒤만 본다.</summary>
    private static bool HasTimeZone(string raw) {
      var s = raw.Trim();
      if (s.Length == 0) return false;
      if (s.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) return true;

      var timeAt = s.IndexOfAny(new[] { 'T', 't', ' ' });
      if (timeAt < 0) return false;

      var tail = s.Substring(timeAt + 1);
      return tail.IndexOf('+') >= 0 || tail.IndexOf('-') >= 0;
    }
  }
}
