using DotNetDrManhattan;

var failures = new List<string>();

void Check(bool condition, string message)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

{
    var factory = new EventFactory(new CommonMetadata("1.0.0", "dotnet", "prod"), null);
    var @event = factory.Custom("custom_event", new Dictionary<string, string> { ["feature"] = "chat" });
    Check(@event.Name == "custom_event", "custom event name mismatch");
    Check(@event.Attributes["feature"] == "chat", "custom event feature mismatch");
    Check(@event.Attributes["app.version"] == "1.0.0", "metadata enrichment mismatch");
}

{
    var deliveries = new List<string>();
    var errors = 0;
    var bus = new DefaultEventBus(
        onObserverError: (_, _, _) => errors++
    );
    bus.Subscribe(new DelegateEventObserver(@event =>
    {
        deliveries.Add("first");
        throw new InvalidOperationException(@event.Name);
    }));
    bus.Subscribe(new DelegateEventObserver(_ => deliveries.Add("second")));
    bus.Publish(new Event("ordered"));
    Check(deliveries.SequenceEqual(new[] { "first", "second" }), "event bus order or isolation mismatch");
    Check(errors == 1, "event bus error callback mismatch");
}

{
    var recorded = new List<Event>();
    var bus = new DefaultEventBus();
    bus.Subscribe(new DelegateEventObserver(recorded.Add));

    var tracker = new DrManhattan(bus, new EventFactory(new CommonMetadata("1.0.0"), null));
    var endpoint = new ProtocolEndpoint("chat", "wss://socket.example.com", "rooms/general");

    tracker.Publish(tracker.ProtocolSession(Protocol.WebSocket, endpoint, "ws-42") is { } session
        ? new EventFactory().WebSocketConnectionStarted(endpoint, "ws-42")
        : new Event("invalid"));

    tracker.ProtocolMessage(
        Protocol.WebSocket,
        endpoint,
        new ProtocolMessage(ProtocolMessageDirection.Outbound, "join_room", "json", "corr-1"),
        "ws-42");

    tracker.ProtocolFailure(
        Protocol.WebSocket,
        endpoint,
        new ProtocolFailure("WS_TIMEOUT", "transport", "heartbeat timeout", true),
        "ws-42");

    Check(recorded.Count == 3, "drmanhattan publish count mismatch");
    Check(recorded[1].Attributes["message.direction"] == "outbound", "outbound protocol message mismatch");
    Check(recorded[2].Attributes["error.retryable"] == "true", "protocol failure retryable mismatch");
}

{
    var recorded = new List<Event>();
    var bus = new DefaultEventBus();
    bus.Subscribe(new DelegateEventObserver(recorded.Add));
    var tracker = new DrManhattan(bus, new EventFactory());
    var session = tracker.ProtocolSession(Protocol.Mqtt, new ProtocolEndpoint("broker"), "mqtt-9");

    session.HeartbeatSent("hb-out-1");
    session.HeartbeatReceived("hb-in-1");
    session.ReconnectScheduled(2, 1500, "network_lost");
    session.Closed(new ProtocolClose(1001, "going away", false));

    Check(recorded.Count == 4, "protocol session event count mismatch");
    Check(recorded[0].Attributes["message.operation"] == "heartbeat", "heartbeat operation mismatch");
    Check(recorded[2].Attributes["reconnect.attempt"] == "2", "reconnect attempt mismatch");
    Check(recorded[3].Attributes["close.code"] == "1001", "close code mismatch");
}

{
    var @event = new EventFactory().HttpError("Checkout", new HttpError(503, "maintenance", "temporarily unavailable"));
    Check(@event.Name == "http_error", "http error event name mismatch");
    Check(@event.Attributes["error.code"] == "503", "http error code mismatch");
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("Validation failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }
    Environment.Exit(1);
}

Console.WriteLine("dotnet-drmanhattan validation passed");
