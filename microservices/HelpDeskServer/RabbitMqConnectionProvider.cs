using RabbitMQ.Client;

/// <summary>
/// RabbitMQ 연결을 제공하는 인터페이스
/// </summary>
public class RabbitMqConnectionProvider : IRabbitMqConnectionProvider
{
  /// <summary>RabbitMQ 연결 인스턴스</summary>
  public IConnection? Connection { get; }
  /// <summary>연결 활성 여부</summary>
  public bool IsConnected => Connection?.IsOpen ?? false;

  /// <summary>
  /// 생성자
  /// </summary>
  /// <param name="connection">RabbitMQ 연결</param>
  public RabbitMqConnectionProvider(IConnection? connection)
  {
    Connection = connection;
  }
}
