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
using System.ComponentModel;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.AspNetCore.Mvc;
using HelpDeskServer.Services;
using Microsoft.EntityFrameworkCore;
using HtmlAgilityPack;


namespace HelpDeskServer.Utilities;

/// <summary>
/// push 관련 유틸리티 클래스.
/// </summary>
public class PushUtil {

  private static string StripHtml(string html) {
    if (string.IsNullOrWhiteSpace(html)) return string.Empty;
    var doc = new HtmlDocument();
    doc.LoadHtml(html);
    return doc.DocumentNode.InnerText;
  }


  /// <summary>
  /// 푸시 메시지 전송
  /// </summary>
  /// <param name="title"></param>
  /// <param name="body"></param>
  /// <param name="url"></param>
  /// <param name="subs"></param>
  /// <param name="sender"></param>
  /// <returns></returns>
  public static Task SendPushMsg(string title, string body, string url, IReadOnlyCollection<Models.PushSubscription> subs, IWebPushService sender) {
    // 간단한 유효성 검사
    if (subs == null || subs.Count == 0) {
      return Task.CompletedTask;
    }

    var descriptionText = StripHtml(body);
    if (descriptionText.Length > 50) {
      descriptionText = descriptionText.Substring(0, 50) + "...";
    }

    var message = new PushMessageDto {
      Title = title,
      Body = descriptionText,
      Url = url
    };

    // BroadcastAsync 호출을 기다리지 않고 Task를 얻습니다.
    var broadcastTask = sender.BroadcastAsync(subs, message, CancellationToken.None);

    // 실패 시 로깅 (호출부에서 ILogger를 전달하지 않으므로 Console.Error에 기록).
    broadcastTask.ContinueWith(t => {
      try {
        var ex = t.Exception;
        if (ex != null) {
          // 간단 로깅: 콘솔/에러에 남김. 필요하면 ILoggerFactory 인자를 추가해 교체하세요.
          Console.Error.WriteLine($"PushUtil.SendPushMsg failed: {ex}");
        }
      }
      catch (Exception logEx) {
        // 로깅 중 문제가 생기면 무시하지 말고 최후의 수단으로 출력
        Console.Error.WriteLine($"PushUtil.SendPushMsg logging failed: {logEx}");
      }
    }, TaskContinuationOptions.OnlyOnFaulted);

    // 즉시 완료된 Task 반환 — 호출부는 더 이상 대기하지 않음.
    return Task.CompletedTask;
  }
  public static async Task SendPushMsg_xxx(string title, string body, string url, IReadOnlyCollection<Models.PushSubscription> subs, IWebPushService sender) {


    var descriptionText = StripHtml(body);
    if (descriptionText.Length > 50) {
      descriptionText = descriptionText.Substring(0, 50) + "...";
    }

    var message = new PushMessageDto {
      Title = title,
      Body = descriptionText,
      Url = url
    };
    await sender.BroadcastAsync(subs, message, CancellationToken.None);

  }

}