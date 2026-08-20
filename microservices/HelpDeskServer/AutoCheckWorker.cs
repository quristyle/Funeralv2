using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HelpDeskServer.Services;
using HelpDeskServer.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HelpDeskServer.Data;
using HelpDeskServer.Models;

namespace HelpDeskServer.Services {

  /// <summary>
  /// AutoCheckWorker
  /// </summary>
  public class AutoCheckWorker : BackgroundService {
    private readonly ILogger<AutoCheckWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IWebPushService _sender;
    private readonly IRabbitMqConnectionProvider _provider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;



    /// <summary>
    /// 생성자
    /// </summary>
    public AutoCheckWorker(
        ILogger<AutoCheckWorker> logger,
        IServiceScopeFactory serviceScopeFactory,
        IWebPushService sender,
        IRabbitMqConnectionProvider provider,
        ILoggerFactory loggerFactory,
        IConfiguration configuration) {
      _logger = logger;
      _serviceScopeFactory = serviceScopeFactory;
      _sender = sender;
      _provider = provider;
      _loggerFactory = loggerFactory;
      _configuration = configuration;
      _httpClient = new HttpClient();
    }

    /// <summary>
    /// 실행 메서드
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
      _logger.LogInformation($"AutoCheckWorker is starting.{stoppingToken.IsCancellationRequested}");

      while (!stoppingToken.IsCancellationRequested) {

        try {
          // Scoped 서비스를 사용하기 위해 새로운 스코프 생성
          using var scope = _serviceScopeFactory.CreateScope();
          var store = scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>();
          var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
          var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
          var reslist = db.Requests.Where(c => c.Id == 231).ToList();


          string title = "[알림] 자동 대기 항목: kepware 라이선스 만료";
          string content = "kepware 라이선스가 곧 만료됩니다. 라이선스를 갱신해 주세요.";

          var adminSubscriptions = await store.GetAdminSubscriptionsAsync();

          foreach (var req in reslist) {



            if (req.Status == ImprovementStatus.Pending || req.CompletededAt > DateTime.UtcNow.AddDays(-4)) {
              if( req.Status == ImprovementStatus.Pending
              &&  req.CreatedAt > DateTime.UtcNow.AddDays(-1).AddHours(-12) ) {
                title = "[위험] kepware 라이선스 만료";
                await PushUtil.SendPushMsg(title, content, "/request_detail?id=231", adminSubscriptions, _sender);

                var Emails = await adminService.GetAdminEmailsForNotificationAsync();
                string vmailTos = string.Join(";", Emails);
                // 이메일 알림
                await EMailUtil.SendEmailJinNets(vmailTos, title, content, _provider, _loggerFactory, _configuration);
              }
              continue;
            }
            req.Status = ImprovementStatus.Pending;
            req.AdminId = null;
            req.Admin = null;
            req.CreatedAt = DateTime.UtcNow;
            req.CompletededAt = null;
            req.UserCompletededAt = null;

            // db.Requests.Update(req); // 자동 감지됨 update 구문 필요 없음.
            await db.SaveChangesAsync();

            await PushUtil.SendPushMsg(title, content, "/request_detail?id=231", adminSubscriptions, _sender);

            var adminEmails = await adminService.GetAdminEmailsForNotificationAsync();
            string mailTos = string.Join(";", adminEmails);
            // 이메일 알림
            await EMailUtil.SendEmailJinNets(mailTos, title, content, _provider, _loggerFactory, _configuration);

          }

        }
        catch (Exception ex) {
        }
        finally {
          // 1시간 마다 체크 실행
          await Task.Delay(TimeSpan.FromMinutes(20), stoppingToken);
        }
      }
    }
  }
}