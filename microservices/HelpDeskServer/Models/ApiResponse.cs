using System.Collections;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;



using HelpDeskServer.Models;
using Microsoft.AspNetCore.Routing;
using System.Dynamic;
using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Data;
using System.Linq.Dynamic.Core;
using HelpDeskServer.Dtos;
using HelpDeskServer.Helpers;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;



namespace HelpDeskServer.Models {
  /// <summary>
  /// API 응답을 위한 표준 래퍼 클래스
  /// </summary>
  /// <typeparam name="T">데이터 페이로드의 타입</typeparam>
  public class ApiResponse<T> {
    /// <summary>요청 성공 여부</summary>
    public bool Success { get; set; }

    /// <summary>응답 메시지</summary>
    public string Message { get; set; }

    /// <summary>실제 데이터</summary>
    public T? Data { get; set; }

    /// <summary>부가 정보</summary>
    public Metadata? Meta { get; set; }

    /// <summary>
    /// ApiResponse의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="success">요청 성공 여부</param>
    /// <param name="message">응답 메시지</param>
    /// <param name="data">실제 데이터</param>
    /// <param name="meta">부가 정보</param>
    public ApiResponse(bool success, string message, T? data, Metadata? meta) {
      Success = success;
      Message = message;
      Data = data;
      Meta = meta;
    }
  }

  /// <summary>
  /// 응답에 대한 부가 정보
  /// </summary>
  public class Metadata {
    /// <summary>요청 시작 시간 (UTC)</summary>
    public string RequestTime { get; set; } = string.Empty;

    /// <summary>요청 완료 시간 (UTC)</summary>
    public string CompletionTime { get; set; } = string.Empty;

    /// <summary>총 처리 소요 시간</summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary>데이터 행의 수</summary>
    public int? RowCount { get; set; }

    /// <summary>데이터 열(속성)의 수</summary>
    public int? ColumnCount { get; set; }
  }

  /// <summary>
  /// 표준 API 응답 객체를 생성하는 헬퍼 클래스
  /// </summary>
  /// <remarks>
  /// 이 클래스는 API 응답을 일관된 형식으로 래핑하고, 메타데이터를 추가하며, 민감한 정보를 필터링하는 역할을 합니다.
  /// </remarks>
  public static class ApiResponseBuilder {
    private static readonly HashSet<string> SensitiveFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
            "passwordHash",
            "password",
        };

    private const int MaxTraverseDepth = 7;
    /// <summary>
    /// 비동기 작업을 실행하고 결과를 표준 API 응답 형식으로 래핑합니다.
    /// </summary>
    /// <typeparam name="T">반환될 데이터의 타입</typeparam>
    /// <param name="action">실행할 비동기 작업</param>
    /// <param name="successMessage">성공 시 메시지</param>
    /// <param name="successStatusCode">성공 시 HTTP 상태 코드</param>
    /// <returns>IResult 형태의 표준 API 응답</returns>
    public static async Task<IResult> CreateAsync<T>(Func<Task<T?>> action, string successMessage = "Request processed successfully.", int successStatusCode = 200) where T : class {
      var stopwatch = Stopwatch.StartNew();
      var requestTime = DateTime.UtcNow;

      try {
        var data = await action();
        stopwatch.Stop();
        var completionTime = DateTime.UtcNow;

        if (data == null) {
          return Results.NotFound(new ApiResponse<object>(false, "Resource not found.", null, CreateMetadata(requestTime, completionTime, stopwatch.ElapsedMilliseconds)));
        }


        object? _data = null;



        // data가 IDictionary나 ExpandoObject인 경우 'data' 속성 확인
        if (data is IDictionary<string, object> dataDict && dataDict.TryGetValue("data", out var dataValue)) {
          _data = dataValue;
        }
        else if (data is System.Dynamic.ExpandoObject expando) {
          var expandoDict = (IDictionary<string, object?>)expando;
          if (expandoDict.TryGetValue("data", out var expandoDataValue)) {
            _data = expandoDataValue;
          }
          else {
            _data = data;
          }
        }
        else {
          // 일반 객체인 경우 반사를 사용하여 'data' 속성 확인
          var dataProperty = data.GetType().GetProperty("data", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
          if (dataProperty != null) {
            _data = dataProperty.GetValue(data);
          }
          else {
            _data = data;
          }
        }





        // 데이터 변환 로직 호출
        var processedData = ProcessData(_data);
        processedData = RemoveSensitive(processedData);


        int? rowCount = null;
        int? colCount = null;

        if (processedData is IEnumerable enumerable) {
          var items = new List<object>();
          foreach (var item in enumerable) items.Add(item);

          rowCount = items.Count;
          if (rowCount > 0) {
            colCount = items[0].GetType().GetProperties().Length;
          }
        }
        else if (processedData != null) {
          rowCount = 1;
          colCount = processedData.GetType().GetProperties().Length;
        }

        var meta = CreateMetadata(requestTime, completionTime, stopwatch.ElapsedMilliseconds, rowCount, colCount);
        var response = new ApiResponse<object>(true, successMessage, processedData, meta);

        // 원본 data 객체에서 totalpagecount와 totalcount 추출
        int? totalPageCount = null;
        int? totalCount = null;

        if (data is IDictionary<string, object> totalDict) {
          if (totalDict.TryGetValue("totalpagecount", out var tpValue) && tpValue is int tp)
            totalPageCount = tp;
          if (totalDict.TryGetValue("totalcount", out var tcValue) && tcValue is int tc)
            totalCount = tc;
        }
        else if (data is System.Dynamic.ExpandoObject totalExpando) {
          var totalExpandoDict = (IDictionary<string, object?>)totalExpando;
          if (totalExpandoDict.TryGetValue("totalpagecount", out var expTpValue) && expTpValue is int expTp)
            totalPageCount = expTp;
          if (totalExpandoDict.TryGetValue("totalcount", out var expTcValue) && expTcValue is int expTc)
            totalCount = expTc;
        }
        else {
          var tpProperty = data.GetType().GetProperty("totalpagecount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
          var tcProperty = data.GetType().GetProperty("totalcount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
          if (tpProperty != null)
            totalPageCount = tpProperty.GetValue(data) as int?;
          if (tcProperty != null)
            totalCount = tcProperty.GetValue(data) as int?;
        }

        // totalpagecount나 totalcount가 있으면 응답 객체에 추가
        if (totalPageCount.HasValue || totalCount.HasValue) {
          var responseObj = new {
            success = response.Success,
            message = response.Message,
            data = response.Data,
            meta = response.Meta,
            totalpagecount = totalPageCount,
            totalcount = totalCount
          };

          return successStatusCode switch {
            201 => Results.Created(string.Empty, responseObj),
            _ => Results.Ok(responseObj),
          };
        }

        return successStatusCode switch {
          201 => Results.Created(string.Empty, response),
          _ => Results.Ok(response),
        };
      }
      catch (Exception ex) {
        stopwatch.Stop();
        var completionTime = DateTime.UtcNow;
        // 실제 운영 환경에서는 ex.Message 대신 일반적인 오류 메시지를 사용하고, ex는 로깅.
        var response = new ApiResponse<object>(false, $"An error occurred: {ex.Message}", null, CreateMetadata(requestTime, completionTime, stopwatch.ElapsedMilliseconds));
        return Results.BadRequest(response);
      }
    }

    private static Metadata CreateMetadata(DateTime reqTime, DateTime compTime, long duration, int? rowCount = null, int? colCount = null) => new Metadata {
      RequestTime = reqTime.ToString("o"), // ISO 8601 format
      CompletionTime = compTime.ToString("o"),
      Duration = $"{duration}ms",
      RowCount = rowCount,
      ColumnCount = colCount
    };


    /// <summary>
    /// 데이터를 순회하며 ToExpandoWithEnumNames를 적용합니다.
    /// </summary>
    private static object? ProcessData(object? data) {
      if (data is null) {
        return null;
        //return data;
      }

      // 데이터가 컬렉션(List, Array 등)인 경우
      if (data is IEnumerable collection && data is not string) {
        var list = collection.Cast<object>().ToList();

        // If the collection is already a list of ExpandoObject/Dictionary, don't process it further.
        if (list.Any() && list.First() is IDictionary<string, object>) {
          return list;
        }

        // 각 항목에 대해 변환 함수를 적용합니다.
        return list.Select(item => item.ToExpandoWithEnumNames()).ToList();
      }

      // 단일 객체인 경우
      return data.ToExpandoWithEnumNames();
    }

    private static object? RemoveSensitive(object? data) {
      return RemoveSensitiveInternal(data, new Dictionary<object, object?>(ReferenceEqualityComparer.Instance), 0);
    }

    /// <summary>
    /// 응답에서 민감 필드를 제거한 사본을 만든다.
    ///
    /// EF 의 탐색 속성 때문에 객체 그래프에 순환이 있다(고객 → 회사 → 고객 목록 → 같은 고객).
    /// 이미 지나온 객체를 만나면 <b>가공 전 원본</b>을 돌려주면 안 된다.
    /// 그렇게 하면 순환 경로를 타고 들어간 지점에서 passwordHash 가 그대로 응답에 실린다.
    /// 그래서 방문 여부만 표시하지 않고, 정제한 결과를 기억해 두었다가 재방문 시 그것을 돌려준다.
    /// (자기참조 형태가 되지만 직렬화는 ReferenceHandler.IgnoreCycles 가 처리한다)
    /// </summary>
    private static object? RemoveSensitiveInternal(object? data, Dictionary<object, object?> sanitized, int depth) {
      if (data is null) return null;
      if (depth > MaxTraverseDepth) return null;

      var t = data.GetType();
      if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid)) {
        return data;
      }

      if (sanitized.TryGetValue(data, out var already)) {
        return already;
      }

      if (data is IDictionary<string, object?> dict) {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        // 자식을 돌기 전에 등록해 둔다. 순환이 돌아왔을 때 이 사전을 그대로 가리키게 된다.
        sanitized[data] = result;
        foreach (var kv in dict) {
          if (SensitiveFieldNames.Contains(kv.Key)) continue;
          result[kv.Key] = RemoveSensitiveInternal(kv.Value, sanitized, depth + 1);
        }
        return result;
      }

      if (data is IEnumerable enumerable && data is not string) {
        var list = new List<object?>();
        sanitized[data] = list;
        foreach (var item in enumerable) {
          list.Add(RemoveSensitiveInternal(item, sanitized, depth + 1));
        }
        return list;
      }

      var objDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
      sanitized[data] = objDict;
      foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
        if (prop.GetIndexParameters().Length != 0) continue;
        var name = prop.Name;
        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        if (SensitiveFieldNames.Contains(name) || SensitiveFieldNames.Contains(camel)) continue;
        var value = prop.GetValue(data);
        objDict[camel] = RemoveSensitiveInternal(value, sanitized, depth + 1);
      }
      return objDict;
    }

    /// <summary>참조 동일성으로 객체를 비교한다. 순환 탐지에 쓴다.</summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object> {
      public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
      public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
      public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
  }



}
