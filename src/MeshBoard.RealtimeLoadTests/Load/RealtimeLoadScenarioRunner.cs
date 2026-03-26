using System.Collections.Concurrent;
using System.Diagnostics;
using MeshBoard.Contracts.Realtime;
using MeshBoard.RealtimeLoadTests.Api;
using MeshBoard.RealtimeLoadTests.Configuration;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace MeshBoard.RealtimeLoadTests.Load;

internal sealed class RealtimeLoadScenarioRunner
{
    private readonly MeshBoardLoadTestApiClient _apiClient;
    private readonly RealtimeLoadTestOptions _options;
    private readonly TimeProvider _timeProvider;

    public RealtimeLoadScenarioRunner(
        MeshBoardLoadTestApiClient apiClient,
        RealtimeLoadTestOptions options,
        TimeProvider timeProvider)
    {
        _apiClient = apiClient;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<RealtimeLoadRunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        await _apiClient.EnsureAuthenticatedAsync(cancellationToken);

        var scenario = _options.GetScenario();
        var startedAtUtc = _timeProvider.GetUtcNow();

        IReadOnlyCollection<RealtimeLoadSample> samples = scenario switch
        {
            RealtimeLoadScenario.ConnectBurst => await RunConnectBurstAsync(cancellationToken),
            RealtimeLoadScenario.ReconnectStorm => await RunReconnectStormAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException()
        };

        var finishedAtUtc = _timeProvider.GetUtcNow();
        return RealtimeLoadRunSummary.Create(scenario, _options, startedAtUtc, finishedAtUtc, samples);
    }

    private async Task<IReadOnlyCollection<RealtimeLoadSample>> RunConnectBurstAsync(CancellationToken cancellationToken)
    {
        var samples = new ConcurrentBag<RealtimeLoadSample>();
        var connectedClients = new ConcurrentBag<IMqttClient>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, _options.ClientCount),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (_, clientCancellationToken) =>
            {
                var session = await TryCreateSessionAsync(samples, clientCancellationToken);
                if (session is null)
                {
                    return;
                }

                var client = new MqttClientFactory().CreateMqttClient();
                var connectSample = await TryConnectAndSubscribeAsync(client, session, "connect", clientCancellationToken);
                samples.Add(connectSample);

                if (connectSample.IsSuccess)
                {
                    connectedClients.Add(client);
                }
                else
                {
                    client.Dispose();
                }
            });

        if (_options.HoldDurationSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.HoldDurationSeconds), cancellationToken);
        }

        foreach (var client in connectedClients)
        {
            await DisconnectAndDisposeAsync(client);
        }

        return samples.ToArray();
    }

    private async Task<IReadOnlyCollection<RealtimeLoadSample>> RunReconnectStormAsync(CancellationToken cancellationToken)
    {
        var samples = new ConcurrentBag<RealtimeLoadSample>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, _options.ClientCount),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (_, clientCancellationToken) =>
            {
                for (var iteration = 0; iteration < _options.ReconnectIterations; iteration++)
                {
                    var session = await TryCreateSessionAsync(samples, clientCancellationToken);
                    if (session is null)
                    {
                        continue;
                    }

                    var client = new MqttClientFactory().CreateMqttClient();
                    var connectSample = await TryConnectAndSubscribeAsync(client, session, "reconnect", clientCancellationToken);
                    samples.Add(connectSample);

                    if (connectSample.IsSuccess && _options.DelayBetweenReconnectMilliseconds > 0)
                    {
                        await Task.Delay(_options.DelayBetweenReconnectMilliseconds, clientCancellationToken);
                    }

                    await DisconnectAndDisposeAsync(client);
                }
            });

        return samples.ToArray();
    }

    private async Task<RealtimeSessionResponse?> TryCreateSessionAsync(
        ConcurrentBag<RealtimeLoadSample> samples,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var session = await _apiClient.CreateRealtimeSessionAsync(cancellationToken);
            samples.Add(new RealtimeLoadSample("session-request", true, stopwatch.Elapsed.TotalMilliseconds));
            return session;
        }
        catch (Exception exception)
        {
            samples.Add(
                new RealtimeLoadSample(
                    "session-request",
                    false,
                    stopwatch.Elapsed.TotalMilliseconds,
                    exception.Message.Trim()));
            return null;
        }
    }

    private async Task<RealtimeLoadSample> TryConnectAndSubscribeAsync(
        IMqttClient client,
        RealtimeSessionResponse session,
        string operation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

            await client.ConnectAsync(BuildClientOptions(session), timeout.Token);

            var topicFilter = ResolveTopicFilter(session);
            if (!string.IsNullOrWhiteSpace(topicFilter))
            {
                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        topic =>
                        {
                            topic.WithTopic(topicFilter);
                            topic.WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce);
                        })
                    .Build();

                await client.SubscribeAsync(subscribeOptions, timeout.Token);
            }

            return new RealtimeLoadSample(operation, true, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            return new RealtimeLoadSample(
                operation,
                false,
                stopwatch.Elapsed.TotalMilliseconds,
                exception.Message.Trim());
        }
    }

    private MqttClientOptions BuildClientOptions(RealtimeSessionResponse session)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(session.ClientId)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanSession()
            .WithCredentials(session.Token, session.ClientId)
            .WithWebSocketServer(
                webSocketBuilder =>
                {
                    webSocketBuilder.WithUri(session.BrokerUrl);
                });

        return builder.Build();
    }

    private async Task DisconnectAndDisposeAsync(IMqttClient client)
    {
        try
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync();
            }
        }
        catch
        {
        }
        finally
        {
            client.Dispose();
        }
    }

    private string? ResolveTopicFilter(RealtimeSessionResponse session)
    {
        if (!string.IsNullOrWhiteSpace(_options.TopicFilterOverride))
        {
            return _options.TopicFilterOverride.Trim();
        }

        return session.AllowedTopicPatterns
            .FirstOrDefault(topicFilter => !string.IsNullOrWhiteSpace(topicFilter))
            ?.Trim();
    }
}
