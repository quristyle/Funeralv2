using RabbitMQ.Client;

/// <summary>
/// RabbitMQ 연결을 제공하는 인터페이스
/// </summary>
public interface IRabbitMqConnectionProvider
{
    /// <summary>
    /// 활성 RabbitMQ 연결 인스턴스
    /// </summary>
    IConnection? Connection { get; }
    /// <summary>
    /// RabbitMQ 연결 활성 여부
    /// </summary>
    bool IsConnected { get; }
}
