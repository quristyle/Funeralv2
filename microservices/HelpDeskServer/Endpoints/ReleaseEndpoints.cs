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
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Endpoints;

/// <summary> 배포 엔드포인트 </summary>
public static class ReleaseEndpoints {
  /// <summary>
  /// 배포 관련 엔드포인트를 애플리케이션에 매핑합니다.
  /// </summary>
  public static void MapReleaseEndpoints(this IEndpointRouteBuilder routes) {
    var group = routes.MapGroup("/api/build");


    group.MapPost("/release", async (HttpRequest httpRequest, AppDbContext db, IRabbitMqConnectionProvider provider, ILoggerFactory loggerFactory, IConfiguration configuration) => {
      var result = await ApiResponseBuilder.CreateAsync(async () => {

        var logger = loggerFactory.CreateLogger("ReleaseEndpoints");

        logger.LogInformation(" release RabbitMQ Connected = {Connected}", provider.IsConnected);

        if (provider.IsConnected) {
          try {
            using var channel = provider.Connection!.CreateModel();
            channel.QueueDeclare(queue: "run_script", durable: false, exclusive: false, autoDelete: false, arguments: null);

            var scriptPath = configuration.GetValue<string>("ShellScript:ReleaseRecept") ?? "/home/lee/projects/wrkScripts/ReleaseRecept.sh";

            string[] args = { "Release", "Release" };
            var payload = new { script = scriptPath, args };
            string json = JsonSerializer.Serialize(payload);
            var body = Encoding.UTF8.GetBytes(json);
            channel.BasicPublish(exchange: "", routingKey: "run_script", basicProperties: null, body: body);
          }
          catch (Exception ex) {
            logger.LogError(ex, "Failed to RabbitMQ.");
          }
        }


        var request = new ImprovementRequest {
          Title = "강제배포",
          Description = "관리자에 의해 강제 배포 처리합니다."
        };

        return request;
      }, "Release successfully.", 201);
      return result;
    })
    .DisableAntiforgery();


    group.MapPost("/release_ghub", async (HttpRequest httpRequest, AppDbContext db, IRabbitMqConnectionProvider provider, ILoggerFactory loggerFactory, IConfiguration configuration) => {
      var result = await ApiResponseBuilder.CreateAsync(async () => {

        var logger = loggerFactory.CreateLogger("ReleaseEndpoints");

        logger.LogInformation(" release_ghub RabbitMQ Connected = {Connected}", provider.IsConnected);


        if (provider.IsConnected) {
          try {
            using var channel = provider.Connection!.CreateModel();
            channel.QueueDeclare(queue: "run_script", durable: false, exclusive: false, autoDelete: false, arguments: null);

            var scriptPath = configuration.GetValue<string>("ShellScript:ReleaseGhub") ?? "/home/lee/projects/wrkScripts/ReleaseGhub.sh";

            string[] args = { "Release", "Release" };
            var payload = new { script = scriptPath, args };
            string json = JsonSerializer.Serialize(payload);
            var body = Encoding.UTF8.GetBytes(json);
            channel.BasicPublish(exchange: "", routingKey: "run_script", basicProperties: null, body: body);
          }
          catch (Exception ex) {
            logger.LogError(ex, "Failed to RabbitMQ.");
          }
        }


        var request = new ImprovementRequest {
          Title = "강제배포",
          Description = "관리자에 의해 강제 배포 처리합니다."
        };

        return request;
      }, "Release successfully.", 201);
      return result;
    })
    .DisableAntiforgery();



  }


}
