using HelpDeskServer.Dtos;
using HelpDeskServer.Utilities;
using HelpDeskServer.Data;

namespace HelpDeskServer.Services;

/// <summary>
/// Contact Service
/// </summary>
public class ContactService {
  private readonly IRabbitMqConnectionProvider _rabbitMqProvider;
  private readonly ILoggerFactory _loggerFactory;
  private readonly IConfiguration _configuration;
  private readonly IServiceScopeFactory _serviceScopeFactory;
  private readonly IWebPushService _sender;
  private readonly IAdminService _adminService;

  /// <summary>
  /// 생성자
  /// </summary>
  /// <param name="rabbitMqProvider"></param>
  /// <param name="loggerFactory"></param>
  /// <param name="configuration"></param>
  public ContactService(IRabbitMqConnectionProvider rabbitMqProvider, ILoggerFactory loggerFactory, IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory,
        IWebPushService sender,
        IAdminService adminService) {
    _rabbitMqProvider = rabbitMqProvider;
    _serviceScopeFactory = serviceScopeFactory;
    _sender = sender;
    _loggerFactory = loggerFactory;
    _configuration = configuration;
    _adminService = adminService;
  }

  /// <summary>
  /// Contact Us 이메일 전송
  /// </summary>
  /// <param name="contactUsDto"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException"></exception>
  public async Task SendContactEmailAsync(ContactUsDto contactUsDto) {
    // 설정정보에 지정된 수신자 이메일 주소
    var recipientEmail = _configuration.GetValue<string>("Vapid:Subject")?.Replace("mailto:", "");


    if (string.IsNullOrEmpty(recipientEmail)) {
      throw new InvalidOperationException("수신자 이메일 주소가 설정되지 않았습니다.");
    }

    var subject = $"[Contact Us] {contactUsDto.Subject ?? "No Subject"} - by {contactUsDto.Name}";
    var body = $"""
            <p><strong>이름:</strong> {contactUsDto.Name}</p>
            <p><strong>이메일:</strong> {contactUsDto.Email}</p>
            <hr />
            <p>{contactUsDto.Message.Replace(Environment.NewLine, "<br />")}</p>
            """;


    using var scope = _serviceScopeFactory.CreateScope();
    var adminEmails = await _adminService.GetAdminEmailsForNotificationAsync();
    string mailTos = string.Join(";", adminEmails);
    await EMailUtil.SendEmailJinNets(recipientEmail, subject, body + mailTos, _rabbitMqProvider, _loggerFactory, _configuration);


    var store = scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>();
    var adminSubscriptions = await store.GetAdminSubscriptionsAsync();
    await PushUtil.SendPushMsg($"[Contact Us] - {contactUsDto.Name}", $"{contactUsDto.Message.Replace(Environment.NewLine, "<br />")}", "/", adminSubscriptions, _sender);




    //await EMailUtil.SendEmail(recipientEmail, subject, body, _rabbitMqProvider, _loggerFactory, _configuration);
  }
}
