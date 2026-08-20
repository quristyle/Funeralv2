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

namespace HelpDeskServer.Services {
  // Health Check API 응답을 위한 DTO
  /// <summary>
  /// 헬스체크 응답
  /// </summary>
  public class HealthCheckResponse {

    [JsonPropertyName("startTime")]
    public string StartTime { get; set; }


    [JsonPropertyName("uptime")]
    public string Uptime { get; set; }




    [JsonPropertyName("healthChecks")]
    public HealthChecks healthChecks { get; set; }



  }



  public class HealthChecks {
    /// <summary>
    /// 상태
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }
    /// <summary>
    /// 체크 항목들
    /// </summary>
    [JsonPropertyName("checks")]
    public List<HealthCheckItem> Checks { get; set; }
  }


  /// <summary>
  /// 헬스체크 항목
  /// </summary>
  public class HealthCheckItem {
    /// <summary>
    /// 이름
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }


    /// <summary>
    /// 상태
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }
  }

  /// <summary>
  /// 헬스체크 워커
  /// </summary>
  public class HealthCheckWorker : BackgroundService {
    private readonly ILogger<HealthCheckWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IWebPushService _sender;
    private readonly IRabbitMqConnectionProvider _provider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 이전 상태가 비정상이었는지 추적하는 플래그
    /// </summary>
    private bool _wasLastCheckUnhealthy = false; // 이전 상태가 비정상이었는지 추적하는 플래그

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="sender"></param>
    /// <param name="provider"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="configuration"></param>
    public HealthCheckWorker(
        ILogger<HealthCheckWorker> logger,
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
      _logger.LogInformation($"HealthCheckWorker is starting.{stoppingToken.IsCancellationRequested}");

      while (!stoppingToken.IsCancellationRequested) {
        bool isCurrentlyUnhealthy = false;
        string currentUnhealthyReason = "";

        try {
          // Scoped 서비스를 사용하기 위해 새로운 스코프 생성
          using var scope = _serviceScopeFactory.CreateScope();
          var store = scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>();
          var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
          var response = await _httpClient.GetAsync("https://nums.hanjucorp.co.kr/oadr/health", stoppingToken);
          if (response.IsSuccessStatusCode) { // 회신이 왔을때.



            _logger.LogInformation($"HealthCheckWorker response IsSuccessStatusCode.{stoppingToken.IsCancellationRequested}");

            var jsonResponse = await response.Content.ReadAsStringAsync(stoppingToken);


            _logger.LogInformation($"HealthCheckWorker jsonResponse.{jsonResponse}");

            var healthData_root = JsonSerializer.Deserialize<HealthCheckResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var healthData = healthData_root?.healthChecks;


            var unhealthyChecks = healthData?.Checks?.Where(c => c.Status != "Healthy").ToList();


            _logger.LogInformation($"HealthCheckWorker healthData.{healthData?.Status}");

            _logger.LogInformation($"HealthCheckWorker unhealthyChecks.{unhealthyChecks}");
            _logger.LogInformation($"HealthCheckWorker unhealthyChecks.Any().{unhealthyChecks.Any()}");




            if (healthData?.Status != "Healthy" || (unhealthyChecks != null && unhealthyChecks.Any())) { // 응답이 있는데.. 문제가 있는 경우.


              //_logger.LogWarning($"unhealthyChecks : {unhealthyChecks}");



              isCurrentlyUnhealthy = true;
              currentUnhealthyReason = string.Join(", ", unhealthyChecks?.Select(c => c.Name) ?? Enumerable.Empty<string>());


              //_logger.LogWarning($"currentUnhealthyReason : {currentUnhealthyReason}");



              // 이전에는 정상이였는데, 지금 비정상이 된 경우에만 알림
              if (!_wasLastCheckUnhealthy && currentUnhealthyReason != "") {
                _logger.LogWarning($"Unhealthy services detected: {currentUnhealthyReason}");
                var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
                // 푸시 알림
                await PushUtil.SendPushMsg($"[긴급] 서비스 이상", $"다음 서비스가 비정상 상태입니다: {currentUnhealthyReason}", "/health-check", adminSubscriptions, _sender);

                var adminEmails = await adminService.GetAdminEmailsForNotificationAsync();
                string mailTos = string.Join(";", adminEmails);
                // 이메일 알림
                await EMailUtil.SendEmailJinNets(mailTos, $"[긴급] 서비스 이상: {currentUnhealthyReason}", $"다음 서비스가 비정상 상태입니다: {currentUnhealthyReason}", _provider, _loggerFactory, _configuration);


              }
            }
          }
          else { // 회신이 오지 않을때.


            _logger.LogInformation($"HealthCheckWorker response IsSuccessStatusCode xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.{stoppingToken.IsCancellationRequested}");


            isCurrentlyUnhealthy = true;
            currentUnhealthyReason = $"API 응답 실패 (상태 코드: {response.StatusCode})";

            // 이전에는 정상이였는데, 지금 비정상이 된 경우에만 알림
            if (!_wasLastCheckUnhealthy) {
              _logger.LogWarning($"Failed to get successful response from health check API. Status code: {response.StatusCode}");
              var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
              // 푸시 알림
              await PushUtil.SendPushMsg($"[긴급] 서버 에러", $"Health Check API가 {response.StatusCode} 상태코드로 응답했습니다.", "/health-check", adminSubscriptions, _sender);

              var adminEmails = await adminService.GetAdminEmailsForNotificationAsync();
              string mailTos = string.Join(";", adminEmails);
              // 이메일 알림
              await EMailUtil.SendEmailJinNets(mailTos, $"[긴급] 서버 응답 에러: {response.StatusCode}", $"Health Check API가 {response.StatusCode} 상태코드로 응답했습니다.", _provider, _loggerFactory, _configuration);
            }
          }
        }
        catch (Exception ex) {
          isCurrentlyUnhealthy = true;
          currentUnhealthyReason = $"HealthCheckWorker 오류: {ex.Message}";
          _logger.LogError(ex, "An error occurred while checking health API.");
        }
        finally {
          // 상태 변경 감지 및 처리
          if (isCurrentlyUnhealthy) {
            _wasLastCheckUnhealthy = true;
          }
          else {
            // 이전에 비정상이었는데, 지금 정상으로 돌아온 경우
            if (_wasLastCheckUnhealthy) {
              _logger.LogInformation("System has recovered to a healthy state.");
              using var scope = _serviceScopeFactory.CreateScope();
              var store = scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>();
              var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
              var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
              await PushUtil.SendPushMsg("[정상화] 서비스 상태 복구", "모든 서비스가 정상 상태로 복구되었습니다.", "/health-check", adminSubscriptions, _sender);


              var adminEmails = await adminService.GetAdminEmailsForNotificationAsync();
              string mailTos = string.Join(";", adminEmails);
              await EMailUtil.SendEmailJinNets(mailTos, "[정상화] 서비스 상태 복구", "모든 서비스가 정상 상태로 복구되었습니다.", _provider, _loggerFactory, _configuration);



            }
            _wasLastCheckUnhealthy = false;
            _logger.LogInformation("Health check successful. All services are healthy.");
          }

          // 1분마다 체크 실행

          if (_wasLastCheckUnhealthy) { // 비정상 이였던 경우에는 
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

          }
          else {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

          }
          //await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
      }
    }
  }
}