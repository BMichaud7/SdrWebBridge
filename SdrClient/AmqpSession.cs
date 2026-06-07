using Amqp;
using Amqp.Framing;
using AmqpMessage    = Amqp.Message;
using AmqpProperties = Amqp.Framing.Properties;

namespace SdrClient;

public sealed class AmqpSession : IDisposable
{
    private Connection?    _connection;
    private Session?       _session;
    private SenderLink?    _sender;
    private ReceiverLink?  _receiver;

    public event Action<string>? MessageReceived;
    public event Action<string>? StatusChanged;

    public bool IsConnected => _connection != null;

    public void Connect(string host, int port, string user, string password)
    {
        Disconnect();
        var address = new Address(host, port, user, password, "/", "amqp");
        _connection = new Connection(address);
        _session    = new Session(_connection);
        _sender     = new SenderLink(_session, "sdr-sender",   "sdr.task.request");
        _receiver   = new ReceiverLink(_session, "sdr-receiver", "sdr.task.response");
        _receiver.Start(50, OnMessage);
        StatusChanged?.Invoke("Connected");
    }

    public void Disconnect()
    {
        try { _receiver?.Close(); }   catch { }
        try { _sender?.Close(); }     catch { }
        try { _session?.Close(); }    catch { }
        try { _connection?.Close(); } catch { }
        _receiver = _sender = null;
        _session  = null;
        _connection = null;
        StatusChanged?.Invoke("Disconnected");
    }

    public void Send(string json)
    {
        if (_sender is null) throw new InvalidOperationException("Not connected");
        var msg = new AmqpMessage(json);
        msg.Properties = new AmqpProperties { ContentType = "application/json" };
        _sender.Send(msg, null, null);
    }

    private void OnMessage(IReceiverLink link, Message msg)
    {
        try
        {
            link.Accept(msg);
            var body = msg.Body is string s ? s : msg.Body?.ToString() ?? "";
            MessageReceived?.Invoke(body);
        }
        catch { }
    }

    public void Dispose() => Disconnect();
}
