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

namespace HelpDeskServer.Data {
  /// <summary>
  /// 동적 필터/정렬/페이징/그룹/집계/HAVING 등 DB에서 가능한 대부분의 기능을
  /// QueryString 기반으로 생성해 IQueryable 에 적용해 주는 유틸리티입니다.
  /// </summary>
  public static class DynamicFilterHelper {
    // 민감 정보 필드(항상 제외)
    private static readonly HashSet<string> SensitiveFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
            "passwordHash"
        };
    // 단순 주석: Query string parsing / supported operators
    // 지원되는 Query parameter 패턴 (예시)
    // Field=val                -> equals
    // Field_neq=val            -> !=
    // Field_gt=val             -> >
    // Field_gte=val            -> >=
    // Field_lt=val             -> <
    // Field_lte=val            -> <=
    // Field_like=val           -> contains (string)
    // Field_left=val           -> startswith (string)
    // Field_right=val          -> endswith (string)
    // Field_in=val1|val2|val3  -> IN (split by |)
    // Field_between=low|high   -> between (for comparable)
    // Field_isnull=true        -> is null / isnotnull
    // groupBy=Field1,Field2
    // aggregate=Count:Id,Sum:Amount,Avg:Score,Max:Price,Min:Price
    // having=Count_Id>5        -> using alias from aggregate (Count_Id)
    // orderBy=Field asc|desc[,Field2 desc]
    // select=Field1,Field2     -> projection
    // page=1&pageSize=20

    /// <summary>
    /// QueryString 기반으로 전체 흐름(필터->그룹/집계->having->정렬->페이징)을 적용합니다.
    /// </summary>
    /// <typeparam name="T">원본 엔티티 타입</typeparam>
    /// <param name="query">IQueryable 원본</param>
    /// <param name="qs">HttpContext.Request.Query 또는 IQueryCollection</param>
    /// <returns>동적 변형이 적용된 IQueryable (groupBy 없으면 IQueryable&lt;T&gt;, 있으면 IQueryable&lt;dynamic&gt;)</returns>
    public static IQueryable ApplyAll<T>(this IQueryable<T> query, IQueryCollection qs) {
      // 1) WHERE 필터 적용

      query = ApplyWhereFilters(query, qs);

      // 2) GROUP BY + AGGREGATE 처리 (있다면)
      if (qs.TryGetValue("groupBy", out var gb) && !string.IsNullOrWhiteSpace(gb)) {
        var grouped = ApplyGroupingAndAggregation(query, qs);
        // grouped is IQueryable (dynamic type)
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

    /// <summary>
    /// select 또는 remove 쿼리 파라미터를 기반으로 프로젝션을 적용합니다.
    /// </summary>
    private static IQueryable ApplyProjection(IQueryable query, IQueryCollection qs, Type? entityType = null) {
      //return query;
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





    /// <summary>
    /// QueryString 으로 들어온 where 필터들을 파싱하여 IQueryable 에 Where 절로 적용합니다.
    /// </summary>
    private static IQueryable<T> ApplyWhereFilters<T>(IQueryable<T> query, IQueryCollection qs) {
      var whereParts = new List<string>();
      var parameters = new List<object>();

      var orGroupParts = new List<string>();
      //var orGroupParameters = new List<object>();

      int paramIndex = 0;

      foreach (var kv in qs) {
        var key = kv.Key;
        var rawVal = kv.Value.ToString();
        if (string.IsNullOrWhiteSpace(rawVal)) continue;

        // skip special keys handled elsewhere
        if (IsSpecialKey(key)) continue;





        Console.WriteLine($" xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx key : {key}");





        // OR 그룹 처리
        // bool isOrGroup = false;
        // if (key.StartsWith("_or_", StringComparison.OrdinalIgnoreCase)) {
        //   isOrGroup = true;
        //   key = key[4..];
        // }

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

                //orGroupParameters.Add(conv);
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
                //orGroupParameters.Add(rawVal.ToLower());
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
                //orGroupParameters.Add(rawVal);
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

              // if (isOrGroup) {
              //   // OR 그룹 내의 IN은 지원이 복잡하므로, 현재는 기본 AND 그룹만 지원.
              //   whereParts.Add(part);
              //   parameters.Add(typedArray);
              // }
              // else {
              //   whereParts.Add(part);
              //   parameters.Add(typedArray);
              // }

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
                // orGroupParameters.Add(lo);
                // orGroupParameters.Add(hi);
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

      // if (orGroupParts.Any()) {
      //   whereParts.Add($"({string.Join(" OR ", orGroupParts)})");
      //   parameters.AddRange(orGroupParameters);
      // }


      if (orGroupParts.Any()) {
        whereParts.Add($"({string.Join(" OR ", orGroupParts)})");
        // parameters were already added in loop to maintain correct indexing
      }


      if (whereParts.Count == 0) return query;

      var whereClause = string.Join(" AND ", whereParts);

      return query.Where(whereClause, parameters.ToArray());
    }

    /// <summary>
    /// groupBy + aggregate 처리를 수행합니다.
    /// groupBy=Field1,Field2
    /// aggregate=Count:Id,Sum:Amount,Avg:Score,Max:Price,Min:Price
    /// 결과는 dynamic IQueryable 로 반환됩니다.
    /// </summary>
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
        // also add alias same as field
        // keyBuilder.Append($" as {f}");
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
          // we call aggregate on the group items: e.g., Sum(it.Amount) needs "it." prefix in Dynamic LINQ aggregate context
          // Dynamic.Core supports e.g. Sum(it, "Amount")? simpler: use aggregate functions like Sum(it.Amount)
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

    /// <summary>
    /// 페이징 적용 (page=1 기준, pageSize=0 이면 페이징 없음)
    /// </summary>
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

    /// <summary>
    /// Helper: checks whether key is special control key (handled elsewhere)
    /// </summary>
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

    /// <summary>
    /// Try to convert raw string to target type (supports Nullable&lt;&gt; and enums)
    /// </summary>

    private static bool TryConvertToType(string raw, Type targetType, out object? converted) {
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

        // DateTime ISO parse
        if (destType == typeof(DateTime)) {
          if (DateTime.TryParse(raw, out var dt)) {
            converted = dt;
            return true;
          }
        }

        // numeric types
        converted = Convert.ChangeType(raw, destType);
        return true;
      }
      catch {
        return false;
      }
    }
  }
}
