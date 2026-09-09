namespace DotNetDrManhattan;

public sealed record Event(string Name, IReadOnlyDictionary<string, string>? Attributes = null)
{
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        Attributes is null ? new Dictionary<string, string>() : new Dictionary<string, string>(Attributes);

    public Event WithAttribute(string key, string value) =>
        this with { Attributes = Merge(Attributes, new Dictionary<string, string> { [key] = value }) };

    public Event WithAttributes(IReadOnlyDictionary<string, string> values) =>
        this with { Attributes = Merge(Attributes, values) };

    internal static IReadOnlyDictionary<string, string> Merge(
        IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string>? right)
    {
        var merged = new Dictionary<string, string>();

        if (left is not null)
        {
            foreach (var item in left)
            {
                merged[item.Key] = item.Value;
            }
        }

        if (right is not null)
        {
            foreach (var item in right)
            {
                merged[item.Key] = item.Value;
            }
        }

        return merged;
    }
}

public interface IEventObserver
{
    void OnEvent(Event @event);
}

public sealed class DelegateEventObserver : IEventObserver
{
    private readonly Action<Event> _handler;

    public DelegateEventObserver(Action<Event> handler) => _handler = handler;

    public void OnEvent(Event @event) => _handler(@event);
}

public interface IEventEnricher
{
    Event Enrich(Event @event);
}

public sealed class DelegateEventEnricher : IEventEnricher
{
    private readonly Func<Event, Event> _handler;

    public DelegateEventEnricher(Func<Event, Event> handler) => _handler = handler;

    public Event Enrich(Event @event) => _handler(@event);
}

public interface IEventBus
{
    void Subscribe(IEventObserver observer);
    void Unsubscribe(IEventObserver observer);
    void Publish(Event @event);
}

public delegate void ObserverErrorHandler(IEventObserver observer, Event @event, Exception error);

public sealed class DefaultEventBus : IEventBus
{
    private readonly List<IEventObserver> _observers = new();
    private readonly IReadOnlyList<IEventEnricher> _enrichers;
    private readonly ObserverErrorHandler? _onObserverError;

    public DefaultEventBus(
        IReadOnlyList<IEventEnricher>? enrichers = null,
        ObserverErrorHandler? onObserverError = null)
    {
        _enrichers = enrichers ?? Array.Empty<IEventEnricher>();
        _onObserverError = onObserverError;
    }

    public void Subscribe(IEventObserver observer) => _observers.Add(observer);

    public void Unsubscribe(IEventObserver observer) => _observers.Remove(observer);

    public void Publish(Event @event)
    {
        var enriched = _enrichers.Aggregate(@event, (current, enricher) => enricher.Enrich(current));

        foreach (var observer in _observers.ToArray())
        {
            try
            {
                observer.OnEvent(enriched);
            }
            catch (Exception error)
            {
                _onObserverError?.Invoke(observer, enriched, error);
            }
        }
    }
}

public sealed record CommonMetadata(
    string AppVersion,
    string? Platform = null,
    string? Environment = null,
    IReadOnlyDictionary<string, string>? Extra = null)
{
    public IReadOnlyDictionary<string, string> AsAttributes()
    {
        var attributes = new Dictionary<string, string>
        {
            ["app.version"] = AppVersion
        };

        if (!string.IsNullOrWhiteSpace(Platform))
        {
            attributes["platform"] = Platform;
        }

        if (!string.IsNullOrWhiteSpace(Environment))
        {
            attributes["environment"] = Environment;
        }

        if (Extra is not null)
        {
            foreach (var item in Extra)
            {
                attributes[item.Key] = item.Value;
            }
        }

        return attributes;
    }
}

public sealed class CommonMetadataEnricher : IEventEnricher
{
    private readonly CommonMetadata _metadata;

    public CommonMetadataEnricher(CommonMetadata metadata) => _metadata = metadata;

    public Event Enrich(Event @event) => @event.WithAttributes(_metadata.AsAttributes());
}

public sealed record HttpError(int Code, string? Type = null, string? Message = null);

public sealed record Protocol(string Name)
{
    public static readonly Protocol Http = new("http");
    public static readonly Protocol WebSocket = new("websocket");
    public static readonly Protocol ServerSentEvents = new("sse");
    public static readonly Protocol Grpc = new("grpc");
    public static readonly Protocol Mqtt = new("mqtt");
    public static readonly Protocol Tcp = new("tcp");
    public static readonly Protocol Udp = new("udp");
}

public sealed record ProtocolEndpoint(string Name, string? Address = null, string? Channel = null)
{
    public IReadOnlyDictionary<string, string> AsAttributes()
    {
        var attributes = new Dictionary<string, string>
        {
            ["endpoint.name"] = Name
        };

        if (!string.IsNullOrWhiteSpace(Address))
        {
            attributes["endpoint.address"] = Address;
        }

        if (!string.IsNullOrWhiteSpace(Channel))
        {
            attributes["endpoint.channel"] = Channel;
        }

        return attributes;
    }
}

public enum ProtocolMessageDirection
{
    Inbound,
    Outbound
}

public sealed record ProtocolMessage(
    ProtocolMessageDirection Direction,
    string? Operation = null,
    string? Type = null,
    string? CorrelationId = null,
    long? SizeBytes = null,
    IReadOnlyDictionary<string, string>? Attributes = null)
{
    public IReadOnlyDictionary<string, string> AsAttributes()
    {
        var attributes = new Dictionary<string, string>
        {
            ["message.direction"] = Direction == ProtocolMessageDirection.Inbound ? "inbound" : "outbound"
        };

        if (!string.IsNullOrWhiteSpace(Operation))
        {
            attributes["message.operation"] = Operation;
        }

        if (!string.IsNullOrWhiteSpace(Type))
        {
            attributes["message.type"] = Type;
        }

        if (!string.IsNullOrWhiteSpace(CorrelationId))
        {
            attributes["message.correlation_id"] = CorrelationId;
        }

        if (SizeBytes is not null)
        {
            attributes["message.size_bytes"] = SizeBytes.Value.ToString();
        }

        if (Attributes is not null)
        {
            foreach (var item in Attributes)
            {
                attributes[item.Key] = item.Value;
            }
        }

        return attributes;
    }
}

public sealed record ProtocolFailure(
    string? Code = null,
    string? Type = null,
    string? Message = null,
    bool? Retryable = null,
    IReadOnlyDictionary<string, string>? Attributes = null)
{
    public IReadOnlyDictionary<string, string> AsAttributes()
    {
        var attributes = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(Code))
        {
            attributes["error.code"] = Code;
        }

        if (!string.IsNullOrWhiteSpace(Type))
        {
            attributes["error.type"] = Type;
        }

        if (!string.IsNullOrWhiteSpace(Message))
        {
            attributes["error.message"] = Message;
        }

        if (Retryable is not null)
        {
            attributes["error.retryable"] = Retryable.Value.ToString().ToLowerInvariant();
        }

        if (Attributes is not null)
        {
            foreach (var item in Attributes)
            {
                attributes[item.Key] = item.Value;
            }
        }

        return attributes;
    }
}

public sealed record ProtocolClose(
    int? Code = null,
    string? Reason = null,
    bool? Graceful = null,
    IReadOnlyDictionary<string, string>? Attributes = null)
{
    public IReadOnlyDictionary<string, string> AsAttributes()
    {
        var attributes = new Dictionary<string, string>();

        if (Code is not null)
        {
            attributes["close.code"] = Code.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(Reason))
        {
            attributes["close.reason"] = Reason;
        }

        if (Graceful is not null)
        {
            attributes["close.graceful"] = Graceful.Value.ToString().ToLowerInvariant();
        }

        if (Attributes is not null)
        {
            foreach (var item in Attributes)
            {
                attributes[item.Key] = item.Value;
            }
        }

        return attributes;
    }
}

public sealed class EventFactory
{
    private readonly IReadOnlyList<IEventEnricher> _enrichers;

    public EventFactory(CommonMetadata? metadata = null, IReadOnlyList<IEventEnricher>? customEnrichers = null)
    {
        var enrichers = new List<IEventEnricher>();

        if (metadata is not null)
        {
            enrichers.Add(new CommonMetadataEnricher(metadata));
        }

        if (customEnrichers is not null)
        {
            enrichers.AddRange(customEnrichers);
        }

        _enrichers = enrichers;
    }

    public Event ScreenViewed(string screenName) =>
        Enrich(new Event("screen_viewed", new Dictionary<string, string> { ["screen.name"] = screenName }));

    public Event Tap(string screenName, string action) =>
        Enrich(new Event("tap", new Dictionary<string, string>
        {
            ["screen.name"] = screenName,
            ["event.action"] = action
        }));

    public Event HttpError(string screenName, HttpError error)
    {
        var attributes = new Dictionary<string, string>
        {
            ["screen.name"] = screenName,
            ["error.code"] = error.Code.ToString()
        };

        if (!string.IsNullOrWhiteSpace(error.Type))
        {
            attributes["error.type"] = error.Type!;
        }

        if (!string.IsNullOrWhiteSpace(error.Message))
        {
            attributes["error.message"] = error.Message!;
        }

        return Enrich(new Event("http_error", attributes));
    }

    public Event Custom(string name, IReadOnlyDictionary<string, string>? attributes = null) =>
        Enrich(new Event(name, attributes));

    public Event ProtocolConnectionStarted(Protocol protocol, ProtocolEndpoint endpoint, string? sessionId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        EnrichProtocolEvent("protocol_connection_started", protocol, endpoint, sessionId, attributes);

    public Event ProtocolConnectionOpened(Protocol protocol, ProtocolEndpoint endpoint, string? sessionId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        EnrichProtocolEvent("protocol_connection_opened", protocol, endpoint, sessionId, attributes);

    public Event ProtocolMessage(Protocol protocol, ProtocolEndpoint endpoint, ProtocolMessage message, string? sessionId = null) =>
        EnrichProtocolEvent("protocol_message", protocol, endpoint, sessionId, message.AsAttributes());

    public Event ProtocolConnectionClosed(Protocol protocol, ProtocolEndpoint endpoint, ProtocolClose? close = null, string? sessionId = null) =>
        EnrichProtocolEvent("protocol_connection_closed", protocol, endpoint, sessionId, (close ?? new ProtocolClose()).AsAttributes());

    public Event ProtocolFailure(Protocol protocol, ProtocolEndpoint endpoint, ProtocolFailure failure, string? sessionId = null) =>
        EnrichProtocolEvent("protocol_failure", protocol, endpoint, sessionId, failure.AsAttributes());

    public Event ProtocolReconnectScheduled(
        Protocol protocol,
        ProtocolEndpoint endpoint,
        int attempt,
        long delayMillis,
        string? reason = null,
        string? sessionId = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var mapped = new Dictionary<string, string>
        {
            ["reconnect.attempt"] = attempt.ToString(),
            ["reconnect.delay_ms"] = delayMillis.ToString()
        };

        if (!string.IsNullOrWhiteSpace(reason))
        {
            mapped["reconnect.reason"] = reason!;
        }

        if (attributes is not null)
        {
            foreach (var item in attributes)
            {
                mapped[item.Key] = item.Value;
            }
        }

        return EnrichProtocolEvent("protocol_reconnect_scheduled", protocol, endpoint, sessionId, mapped);
    }

    public Event WebSocketConnectionStarted(ProtocolEndpoint endpoint, string? sessionId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        ProtocolConnectionStarted(Protocol.WebSocket, endpoint, sessionId, attributes);

    public Event WebSocketConnectionOpened(ProtocolEndpoint endpoint, string? sessionId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        ProtocolConnectionOpened(Protocol.WebSocket, endpoint, sessionId, attributes);

    public Event WebSocketMessageSent(
        ProtocolEndpoint endpoint,
        string? operation = null,
        string? type = null,
        string? correlationId = null,
        long? sizeBytes = null,
        string? sessionId = null,
        IReadOnlyDictionary<string, string>? attributes = null) =>
        ProtocolMessage(
            Protocol.WebSocket,
            endpoint,
            new ProtocolMessage(ProtocolMessageDirection.Outbound, operation, type, correlationId, sizeBytes, attributes),
            sessionId);

    public Event WebSocketMessageReceived(
        ProtocolEndpoint endpoint,
        string? operation = null,
        string? type = null,
        string? correlationId = null,
        long? sizeBytes = null,
        string? sessionId = null,
        IReadOnlyDictionary<string, string>? attributes = null) =>
        ProtocolMessage(
            Protocol.WebSocket,
            endpoint,
            new ProtocolMessage(ProtocolMessageDirection.Inbound, operation, type, correlationId, sizeBytes, attributes),
            sessionId);

    public Event WebSocketConnectionClosed(ProtocolEndpoint endpoint, ProtocolClose? close = null, string? sessionId = null) =>
        ProtocolConnectionClosed(Protocol.WebSocket, endpoint, close, sessionId);

    public Event WebSocketFailure(ProtocolEndpoint endpoint, ProtocolFailure failure, string? sessionId = null) =>
        ProtocolFailure(Protocol.WebSocket, endpoint, failure, sessionId);

    public Event WebSocketReconnectScheduled(ProtocolEndpoint endpoint, int attempt, long delayMillis, string? reason = null, string? sessionId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        ProtocolReconnectScheduled(Protocol.WebSocket, endpoint, attempt, delayMillis, reason, sessionId, attributes);

    private Event Enrich(Event @event) =>
        _enrichers.Aggregate(@event, (current, enricher) => enricher.Enrich(current));

    private Event EnrichProtocolEvent(
        string name,
        Protocol protocol,
        ProtocolEndpoint endpoint,
        string? sessionId,
        IReadOnlyDictionary<string, string>? attributes)
    {
        var mapped = new Dictionary<string, string>
        {
            ["protocol.name"] = protocol.Name
        };

        foreach (var item in endpoint.AsAttributes())
        {
            mapped[item.Key] = item.Value;
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            mapped["session.id"] = sessionId!;
        }

        if (attributes is not null)
        {
            foreach (var item in attributes)
            {
                mapped[item.Key] = item.Value;
            }
        }

        return Enrich(new Event(name, mapped));
    }
}

public sealed class DrManhattan
{
    private readonly IEventBus _bus;
    private readonly EventFactory _factory;

    public DrManhattan(IEventBus bus, EventFactory factory)
    {
        _bus = bus;
        _factory = factory;
    }

    public void Publish(Event @event) => _bus.Publish(@event);

    public void ScreenViewed(string screenName) => Publish(_factory.ScreenViewed(screenName));

    public void Tap(string screenName, string action) => Publish(_factory.Tap(screenName, action));

    public void HttpError(string screenName, HttpError error) => Publish(_factory.HttpError(screenName, error));

    public void Custom(string name, IReadOnlyDictionary<string, string>? attributes = null) => Publish(_factory.Custom(name, attributes));

    public void ProtocolConnectionStarted(Protocol protocol, ProtocolEndpoint endpoint, string? sessionId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        Publish(_factory.ProtocolConnectionStarted(protocol, endpoint, sessionId, attributes));

    public void ProtocolConnectionOpened(Protocol protocol, ProtocolEndpoint endpoint, string? sessionId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        Publish(_factory.ProtocolConnectionOpened(protocol, endpoint, sessionId, attributes));

    public void ProtocolMessage(Protocol protocol, ProtocolEndpoint endpoint, ProtocolMessage message, string? sessionId = null) =>
        Publish(_factory.ProtocolMessage(protocol, endpoint, message, sessionId));

    public void ProtocolConnectionClosed(Protocol protocol, ProtocolEndpoint endpoint, ProtocolClose? close = null, string? sessionId = null) =>
        Publish(_factory.ProtocolConnectionClosed(protocol, endpoint, close, sessionId));

    public void ProtocolFailure(Protocol protocol, ProtocolEndpoint endpoint, ProtocolFailure failure, string? sessionId = null) =>
        Publish(_factory.ProtocolFailure(protocol, endpoint, failure, sessionId));

    public void ProtocolReconnectScheduled(Protocol protocol, ProtocolEndpoint endpoint, int attempt, long delayMillis, string? reason = null, string? sessionId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        Publish(_factory.ProtocolReconnectScheduled(protocol, endpoint, attempt, delayMillis, reason, sessionId, attributes));

    public ProtocolSessionTracker ProtocolSession(Protocol protocol, ProtocolEndpoint endpoint, string? sessionId = null) =>
        new(this, protocol, endpoint, sessionId);

    public WebSocketSessionTracker WebSocketSession(ProtocolEndpoint endpoint, string? sessionId = null) =>
        new(this, endpoint, sessionId);
}

public class ProtocolSessionTracker
{
    private readonly DrManhattan _drManhattan;
    private readonly Protocol _protocol;

    public ProtocolEndpoint Endpoint { get; }
    public string? SessionId { get; }

    public ProtocolSessionTracker(DrManhattan drManhattan, Protocol protocol, ProtocolEndpoint endpoint, string? sessionId = null)
    {
        _drManhattan = drManhattan;
        _protocol = protocol;
        Endpoint = endpoint;
        SessionId = sessionId;
    }

    public void ConnectionStarted(IReadOnlyDictionary<string, string>? attributes = null) =>
        _drManhattan.ProtocolConnectionStarted(_protocol, Endpoint, SessionId, attributes);

    public void ConnectionOpened(IReadOnlyDictionary<string, string>? attributes = null) =>
        _drManhattan.ProtocolConnectionOpened(_protocol, Endpoint, SessionId, attributes);

    public void Message(ProtocolMessage message) =>
        _drManhattan.ProtocolMessage(_protocol, Endpoint, message, SessionId);

    public void InboundMessage(string? operation = null, string? type = null, string? correlationId = null, long? sizeBytes = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        Message(new ProtocolMessage(ProtocolMessageDirection.Inbound, operation, type, correlationId, sizeBytes, attributes));

    public void OutboundMessage(string? operation = null, string? type = null, string? correlationId = null, long? sizeBytes = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        Message(new ProtocolMessage(ProtocolMessageDirection.Outbound, operation, type, correlationId, sizeBytes, attributes));

    public void HeartbeatSent(string? correlationId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        OutboundMessage("heartbeat", "heartbeat", correlationId, null, attributes);

    public void HeartbeatReceived(string? correlationId = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        InboundMessage("heartbeat", "heartbeat", correlationId, null, attributes);

    public void ReconnectScheduled(int attempt, long delayMillis, string? reason = null, IReadOnlyDictionary<string, string>? attributes = null) =>
        _drManhattan.ProtocolReconnectScheduled(_protocol, Endpoint, attempt, delayMillis, reason, SessionId, attributes);

    public void Failure(ProtocolFailure failure) =>
        _drManhattan.ProtocolFailure(_protocol, Endpoint, failure, SessionId);

    public void Closed(ProtocolClose? close = null) =>
        _drManhattan.ProtocolConnectionClosed(_protocol, Endpoint, close, SessionId);
}

public sealed class WebSocketSessionTracker : ProtocolSessionTracker
{
    public WebSocketSessionTracker(DrManhattan drManhattan, ProtocolEndpoint endpoint, string? sessionId = null)
        : base(drManhattan, Protocol.WebSocket, endpoint, sessionId)
    {
    }
}
