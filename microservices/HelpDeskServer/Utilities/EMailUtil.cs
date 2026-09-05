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
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HelpDeskServer.Utilities;

/// <summary>
/// EMail 관련 유틸리티 클래스.
/// </summary>
public class EMailUtil {



  /// <summary>
  /// 이메일 전송
  /// </summary>
  /// <param name="mailTos"></param>
  /// <param name="title"></param>
  /// <param name="bodys"></param>
  /// <param name="provider"></param>
  /// <param name="loggerFactory"></param>
  /// <param name="configuration"></param>
  /// <returns></returns>
  public static async Task SendEmail(string mailTos
  , string title
  , string bodys
  , IRabbitMqConnectionProvider provider
  , ILoggerFactory loggerFactory
  , IConfiguration configuration
  ) {


    // 독자의 주소로 보내도록 처리 수정 하자... 나중에....

    var logger = loggerFactory.CreateLogger("RequestEndpoints");
    if (provider.IsConnected) {
      try {


        string msgQPath = Environment.GetEnvironmentVariable("MessageQueue__Path") ?? "/home/lee/projects/msgQ";

        string jsonFilePath = $"{msgQPath}/email_" + Guid.NewGuid().ToString() + ".json";




        // var emailData = new { title, body = bodys, mailto = mailTos };
        // string jsonContent = JsonSerializer.Serialize(emailData, new JsonSerializerOptions { WriteIndented = false });
        // await File.WriteAllTextAsync(jsonFilePath, jsonContent);

        var emailData = new { title, body = bodys, mailto = mailTos };
        string jsonContent = JsonSerializer.Serialize(emailData, new JsonSerializerOptions { WriteIndented = false });

        //string base64String = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{title}|{mailTos}|{bodys}"));




        //await File.WriteAllTextAsync(jsonFilePath, base64String, Encoding.UTF8);
        await File.WriteAllTextAsync(jsonFilePath, jsonContent, Encoding.UTF8);


        var scriptPath = configuration.GetValue<string>("ShellScript:WrkReceptMail") ?? "/home/lee/projects/wrkScripts/wrkReceptMail.sh";
        await using var channel = await provider.Connection!.CreateChannelAsync();
        await channel.QueueDeclareAsync(queue: "run_script", durable: false, exclusive: false, autoDelete: false, arguments: null);



        string[] args = { title, jsonFilePath };
        var payload = new { script = scriptPath, args };
        string json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);
        await channel.BasicPublishAsync(exchange: "", routingKey: "run_script", body: body);

      }
      catch (Exception ex) {
        logger.LogError(ex, "Failed to publish message to RabbitMQ.");
      }

    }



  }


  /// <summary>
  /// 이메일 전송 (Git Push 용)
  /// </summary>
  /// <param name="subs"></param>
  /// <param name="title"></param>
  /// <param name="bodys"></param>
  /// <param name="provider"></param>
  /// <param name="loggerFactory"></param>
  /// <param name="configuration"></param>
  /// <returns></returns>
  /* // git push 용 이메일 전송 사용안함.
  public static async Task SendEmailGitPush(IReadOnlyCollection<Models.PushSubscription> subs
  , string title
  , string bodys
  , IRabbitMqConnectionProvider provider
  , ILoggerFactory loggerFactory
  , IConfiguration configuration
  ) {


    // 독자의 주소로 보내도록 처리 수정 하자... 나중에....

    var logger = loggerFactory.CreateLogger("RequestEndpoints");
    if (provider.IsConnected) {
      try {
        using var channel = provider.Connection!.CreateModel();
        channel.QueueDeclare(queue: "run_script", durable: false, exclusive: false, autoDelete: false, arguments: null);

        var scriptPath = configuration.GetValue<string>("ShellScript:WrkRecept") ?? "/home/lee/projects/wrkScripts/wrkRecept.sh";

        string[] args = { title, bodys };
        var payload = new { script = scriptPath, args };
        string json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);
        channel.BasicPublish(exchange: "", routingKey: "run_script", basicProperties: null, body: body);
      }
      catch (Exception ex) {
        logger.LogError(ex, "Failed to publish message to RabbitMQ.");
      }

    }



  }
*/


  public static string NormalizeMailTos(string mailTos) {
    if (string.IsNullOrWhiteSpace(mailTos))
      return string.Empty;

    var result = mailTos
        .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim())
        .Where(IsValidEmail)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    return string.Join(";", result);
  }

  private static bool IsValidEmail(string email) {
    try {
      var addr = new MailAddress(email);
      return addr.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
    }
    catch {
      return false;
    }
  }


  /*
  // 구독 기준의 이메일 전송은 사용하지 않음.

    public static async Task SendEmailJinNets(IReadOnlyCollection<Models.PushSubscription> subs
      , string title
      , string bodys
      , IRabbitMqConnectionProvider provider
      , ILoggerFactory loggerFactory
      , IConfiguration configuration) {

      string adminMailTos = "";
      string customMailTos = "";
      foreach (var sub in subs) {
        adminMailTos += sub.Admin?.Email + ";";
        customMailTos += sub.Customer?.Email + ";";
      }


      await SendEmailJinNets(adminMailTos
       , title
       , bodys
       , provider
       , loggerFactory
       , configuration);
      await SendEmailJinNets(customMailTos
       , title
       , bodys
       , provider
       , loggerFactory
       , configuration);


    }


  */


  public static async Task SendEmailJinNets(string mailTos
    , string title
    , string bodys
    , IRabbitMqConnectionProvider provider
    , ILoggerFactory loggerFactory
    , IConfiguration configuration) {


    // mailTos  에 여러개 중복 메일 주소가 있을 수 있다. 

    var logger = loggerFactory.CreateLogger("EMailUtil.SendEmailJinNets");
    mailTos = NormalizeMailTos(mailTos);


    if (string.IsNullOrWhiteSpace(mailTos)) {
      logger.LogWarning("SendEmailJinNets called with empty mailTos.");
      return;// Task.CompletedTask;
    }

    try {
      // 설정에서 값 읽기(없으면 기본값 사용)
      var host = configuration.GetValue<string>("Email:JinNets:Host") ?? "jinnets.co.kr";
      var port = configuration.GetValue<int?>("Email:JinNets:Port") ?? 587;
      var user = configuration.GetValue<string>("Email:JinNets:User") ?? "suport@jinnets.co.kr";
      var pass = configuration.GetValue<string>("Email:JinNets:Password") ?? "b0927bbZ1!";
      var fromDisplay = configuration.GetValue<string>("Email:JinNets:FromDisplay") ?? "지원팀";
      var useSsl = configuration.GetValue<bool?>("Email:JinNets:UseSsl") ?? true; // STARTTLS on port 587
      var ignoreCertErrors = configuration.GetValue<bool?>("Email:IgnoreCertificateErrors") ?? false;
      var timeoutMs = configuration.GetValue<int?>("Email:TimeoutMs") ?? 15000;

      if (string.IsNullOrWhiteSpace(mailTos)) {
        logger.LogWarning("SendEmailJinNets called with empty mailTos.");
        return;// Task.CompletedTask;
      }

      // (옵션) TLS 인증서 검증 무시 (테스트 전용)
      if (ignoreCertErrors) {
        ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
      }

      using var message = new MailMessage();
      message.From = new MailAddress(user, fromDisplay);
      // 여러 수신자 지원 (쉼표 또는 세미콜론 구분)
      var tos = mailTos.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var to in tos) {
        message.To.Add(to.Trim());
      }


      message.Headers.Add("Content-Language", "ko");



      message.Subject = title ?? string.Empty;
      message.Body = bodys ?? string.Empty;
      message.IsBodyHtml = true;
      message.BodyEncoding = Encoding.UTF8;
      message.SubjectEncoding = Encoding.UTF8;

      using var client = new SmtpClient(host, port) {
        EnableSsl = useSsl,
        Credentials = new NetworkCredential(user, pass),
        DeliveryMethod = SmtpDeliveryMethod.Network,
        Timeout = timeoutMs
      };

      // SendMailAsync 사용
      await client.SendMailAsync(message);

      /*
            var broadcastTask = client.SendMailAsync(message);



            // 실패 시 로깅 (호출부에서 ILogger를 전달하지 않으므로 Console.Error에 기록).
            broadcastTask.ContinueWith(t => {
              try {
                var ex = t.Exception;
                if (ex != null) {
                  // 간단 로깅: 콘솔/에러에 남김. 필요하면 ILoggerFactory 인자를 추가해 교체하세요.
                  Console.Error.WriteLine($"SendEmailJinNets failed: {ex}");
                }
              }
              catch (Exception logEx) {
                // 로깅 중 문제가 생기면 무시하지 말고 최후의 수단으로 출력
                Console.Error.WriteLine($"SendEmailJinNets logging failed: {logEx}");
              }
            }, TaskContinuationOptions.OnlyOnFaulted);

            logger.LogInformation("SendEmailJinNets succeeded to {tos} via {host}:{port}", mailTos, host, port);
            // 즉시 완료된 Task 반환 — 호출부는 더 이상 대기하지 않음.
            return Task.CompletedTask;

      */


    }
    catch (Exception ex) {
      var logger2 = loggerFactory.CreateLogger("EMailUtil.SendEmailJinNets");
      logger2.LogError(ex, "SendEmailJinNets failed to send email to {tos}", mailTos);
      throw;
    }
  }


}