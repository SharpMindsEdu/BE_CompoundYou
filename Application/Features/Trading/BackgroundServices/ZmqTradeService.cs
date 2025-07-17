using System.Collections.Concurrent;
using Application.Shared.Services.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace Application.Features.Trading.BackgroundServices
{
    public enum CommandType
    {
        Open,
        Close,
        Info
    }

    public class ZmqTradeService(IServiceScopeFactory scopeFactory, ILogger<ZmqTradeService> logger) : BackgroundService
    {
        private readonly string _accountName = "510071179";
        private const string PubAddress  = "tcp://127.0.0.1:1985"; // EA inbound
        private const string SubAddress  = "tcp://127.0.0.1:1986"; // EA outbound

        private DateTime? LatestTradeTime;

        private PublisherSocket _pubSocket = null!;
        private SubscriberSocket _subSocket = null!;

        // Warteschlange für eingehende statische Kommandos
        private readonly ConcurrentQueue<(CommandType type, string body, TaskCompletionSource<string> tcs, TimeSpan timeout)> 
            _commandQueue = new();

        // Zuordnung von UID auf TaskCompletionSource für Antworten
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> Pending 
            = new();

        // Singleton-Referenz, damit static AddCommand Zugriff hat
        private static ZmqTradeService _instance = null!;

        /// <summary>
        /// Ermöglicht von außen, über das Enum ein Kommando zu senden und auf die Antwort zu warten.
        /// </summary>
        public static void AddCommand(CommandType type, string body, TimeSpan timeout = default)
        {
            if (_instance == null)
                throw new InvalidOperationException("ZmqTradeService läuft noch nicht.");
            
            if(timeout == TimeSpan.Zero)
                timeout = TimeSpan.FromSeconds(15);

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _instance._commandQueue.Enqueue((type, body, tcs, timeout));
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(2000);
            // Singleton setzen
            _instance = this;

            // Publisher zum Senden von Kommandos
            _pubSocket = new PublisherSocket();
            _pubSocket.SendReady += (sender, args) => logger.LogInformation("Sending to PubSocket is ready now!");
            _pubSocket.Connect(PubAddress);
            


            // Subscriber zum Empfang von Antworten
            _subSocket = new SubscriberSocket();
            _subSocket.Connect(SubAddress);
            _subSocket.Subscribe("");

            logger.LogInformation("ZmqTradeService gestartet.");
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ZmqTradeService ExecuteAsync beginnt.");
            while (!stoppingToken.IsCancellationRequested)
            {
                await RetrieveTradingOpportunitiesSuccessful();
                // 1) Neue Commands aus der Queue senden
                while (_commandQueue.TryDequeue(out var item))
                {
                    var (type, body, tcs, timeout) = item;
                    var id = Guid.NewGuid().ToString();
                    var cmdString = $"{_accountName}|{id} {type.ToString().ToLower()} {body}";

                    Pending[id] = tcs;
                    logger.LogInformation("Sende ZMQ-Kommando: {Cmd}", cmdString);
                    _pubSocket.SendFrame(cmdString);

                    // Timeout überwachen
                    // var cancel = new CancellationTokenSource(timeout);
                    // cancel.Token.Register(() =>
                    // {
                    //     if (Pending.TryRemove(id, out var removed))
                    //         removed.TrySetException(new TimeoutException($"Keine Antwort für UID {id} in {timeout.TotalSeconds}s"));
                    // });
                }

                // 2) Antworten verarbeiten
                if (_subSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out var msg))
                {
                    // Format: response|account|uid payload
                    var split = msg.Split(' ', 2);
                    if (split.Length == 2)
                    {
                        var header = split[0];  // "response|account|uid"
                        var payload = split[1];
                        var uid = header[(header.LastIndexOf('|') + 1)..];

                        if (Pending.TryRemove(uid, out var tcs))
                        {
                            tcs.TrySetResult(payload);
                            logger.LogInformation("Antwort für UID {Uid}: {Payload}", uid, payload);
                        }
                    }
                }
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task RetrieveTradingOpportunitiesSuccessful()
        {
            try
            {
                if (!LatestTradeTime.HasValue
                    || (LatestTradeTime.Value.Date != DateTime.UtcNow.Date
                        && DateTime.UtcNow.DayOfWeek != DayOfWeek.Saturday
                        && DateTime.UtcNow.DayOfWeek != DayOfWeek.Sunday))
                {
                    using var scope = scopeFactory.CreateScope();
                    var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
                    var result = await aiService.GetDailySignalAsync("USDCAD");

                    if (result == null) return;
                    LatestTradeTime = DateTime.UtcNow.Date;
                    
                    if(result.Confidence > 60) 
                        AddCommand(CommandType.Open, result.ToCommand());
                }
            }
            catch (Exception ex)
            {
                    logger.LogError(ex, "Error while retrieving trading opportunities");
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _subSocket?.Dispose();
            _pubSocket?.Dispose();
            logger.LogInformation("ZmqTradeService gestoppt.");
            return base.StopAsync(cancellationToken);
        }
    }
}
