using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConnectorLib;
using ConnectorLib.JSON;
using Impostor.Api.Events;
using Impostor.Api.Events.Managers;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Impostor.Server.WarpWorld.CrowdControl.Effects;
using Impostor.Server.WarpWorld.CrowdControl.GameState;
using Impostor.Server.WarpWorld.ExternalCommands;
using Serilog;
using ClientContext = ConnectorLib.SimpleTCPServerConnector<ConnectorLib.JSON.Response, ConnectorLib.JSON.Request>.ClientContext;
using Log = Serilog.Log;

namespace Impostor.Server.WarpWorld.CrowdControl
{
    [SuppressMessage("ReSharper", "SuggestVarOrType_Elsewhere")]
    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1503:Braces should not be omitted")]
    [SuppressMessage("Style", "IDE0007:Use implicit type")]
    [SuppressMessage("ReSharper", "SuggestVarOrType_BuiltInTypes")]
    internal class CrowdControlManager : IEventListener
    {
        private readonly SimpleTCPServerConnector<Response, Request> tcpServer;
        private const DigestAlgorithm DIGEST_ALGORITHM = DigestAlgorithm.WHIRLPOOL;
        private readonly ConcurrentDictionary<ClientContext, ManagerContext> contexts = new();
        private readonly ConcurrentDictionary<string, GameCode> codes = new();

        private readonly GameStateTracker gameStateTracker;
        private readonly GameManager gameManager;
        private readonly IEventManager eventManager;

        private readonly ILogger logger;

        private readonly Random rng = new();

        public CrowdControlManager(GameStateTracker gameStateTracker, GameManager gameManager, IEventManager eventManager)
        {
            this.gameStateTracker = gameStateTracker ?? throw new ArgumentNullException(nameof(gameStateTracker));
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));

            logger = Log.ForContext<CrowdControlManager>();
            //var gameCodeCrowdControlSessionLookup = new ConcurrentDictionary<int, CrowdControlSessionInfo>();

            tcpServer = new();

            tcpServer.ClientConnected += OnClientConnected;
            tcpServer.ClientDisconnected += OnClientDisconnected;
            tcpServer.MessageParsed += OnMessageParsed;

            logger.Debug("The Crowd Control service is starting.");
            Start();
        }

        private void OnClientConnected(object? sender, ClientContext e)
        {
            contexts[e] = new ManagerContext { ClientContext = e };
            tcpServer.Send(new Response { type = ResponseType.Login }, e);
        }

        private void OnClientDisconnected(object? sender, ClientContext context)
        {
            if (contexts.TryRemove(context, out var c))
            {
                foreach (Effect e in c?.Effects?.Values ?? Enumerable.Empty<Effect>())
                {
                    try { e.Unload(); }
                    catch (Exception ex) { Log.Error(ex, "An effect postloader threw an exception."); }
                }

                contexts.TryRemove(context, out _);
                if (c.Password != null) codes.TryRemove(c.Password, out _);
            }
        }

        private async void OnMessageParsed(ISimpleTCPConnector<Response, Request, ClientContext> sender, Request request, ClientContext context)
        {
            try
            {
                if (!contexts.TryGetValue(context, out var c)) return;

                Response response = new() { id = request.id };
                switch (request.type)
                {
                    case RequestType.Test:
                    {
                        if (!c.IsLoggedIn)
                        {
                            response.status = EffectResult.NotReady;
                            break;
                        }

                        if (!c.Effects.TryGetValue(request.code, out Effect e))
                        {
                            response.status = EffectResult.Failure;
                            break;
                        }

                        response.status = e.IsReady() ? EffectResult.Success : EffectResult.Retry;
                        break;
                    }
                    case RequestType.Start:
                    {
                        if (!c.IsLoggedIn)
                        {
                            response.status = EffectResult.NotReady;
                            break;
                        }

                        if (!c.Effects.TryGetValue(request.code, out Effect e))
                        {
                            response.status = EffectResult.Failure;
                            break;
                        }

                        if (TryGetMutexes(e))
                        {
                            if (e.TryStart(request))
                            {
                                response.status = EffectResult.Success;
                                if (e.Type == Effect.EffectType.Timed)
                                    response.timeRemaining = e.Duration.Milliseconds;
                                ScheduleStop(e).Forget();
                            }
                            else
                            {
                                response.status = EffectResult.Retry;
                                ReleaseAll(e);
                            }
                        }
                        else response.status = EffectResult.Retry;
                        break;
                    }
                    case RequestType.Stop:
                    {
                        if (!c.IsLoggedIn)
                        {
                            response.status = EffectResult.NotReady;
                            break;
                        }

                        if (!c.Effects.TryGetValue(request.code, out Effect e))
                        {
                            response.status = EffectResult.Failure;
                            break;
                        }
                        response.status = (await ScheduleStop(e, TimeSpan.Zero)) ? EffectResult.Success : EffectResult.Retry;
                        break;
                    }
                    case RequestType.Login:
                    {
                        //if (HashCompare(request.message, "password", DIGEST_ALGORITHM))
                        string password = Encoding.UTF8.GetString(StringToBytes(request.message)).ToLowerInvariant();
                        Log.Debug($"Got a login request with password {password}...");
                        if (codes.TryGetValue(password, out GameCode gc) && gameManager.TryFind(gc, out Game? game))
                        {
                            c.Game = game!;
                            c.Password = password;
                            c.Effects = GetEffects(game, gameManager, gameStateTracker).ToDictionary(e => e.Code);
                            response.type = ResponseType.LoginSuccess;
                            c.IsLoggedIn = true;
                        }
                        else
                        {
                            Close(c, "The supplied password was not valid.");
                            return;
                        }
                        break;
                    }
                    case RequestType.KeepAlive:
                        return;
                }
                tcpServer.Send(response, context);
            }
            catch (Exception e)
            {
                logger.Error(e, e.Message);
            }
        }

        private Task<bool> ScheduleStop(Effect effect) => ScheduleStop(effect, effect.Duration);

        private async Task<bool> ScheduleStop(Effect effect, TimeSpan delay)
        {
            await Task.Delay(delay);
            bool wasRunning = effect.Active;
            bool result = effect.TryStop();
            if (result && wasRunning) ReleaseAll(effect);
            return result;
        }

        private void Close(ManagerContext context, string message)
        {
            try
            {
                Response response = new()
                {
                    type = ResponseType.Disconnect,
                    message = message
                };
                context.IsLoggedIn = false;
                tcpServer.Send(response, context.ClientContext);
            }
            catch (Exception e) { logger.Debug(e, e.Message); }

            try { tcpServer.CloseContext(context.ClientContext); }
            catch (Exception e) { logger.Debug(e, e.Message); }
        }

        [EventListener]
        public void OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            if (e.Game.GameState != GameStates.NotStarted) return;
            if (e.ClientPlayer.IsHost)
            {
                string pw = GetNewPassword();
                codes[pw.ToLowerInvariant()] = e.Game.Code;
                string msg = $"Your Crowd Control password is: {pw}";
                e.PlayerControl.SendChatToPlayerAsync(msg);
            }
        }

        private static readonly string[] WORDS = File.ReadAllLines("words.txt");

        private string GetNewPassword()
        {
            string s1 = FrontCase(WORDS[rng.Next(100, WORDS.Length)]);
            string s2 = FrontCase(WORDS[rng.Next(100, WORDS.Length)]);
            string s3 = FrontCase(WORDS[rng.Next(100, WORDS.Length)]);
            return s1 + s2 + s3;

            string FrontCase(string str)
            {
                if (str.Length == 0) return string.Empty;
                if (str.Length == 1) return char.ToUpper(str[0]).ToString();
                return char.ToUpper(str[0]) + str.Substring(1);
            }
        }

        public static byte[] StringToBytes(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        public void Start()
        {
            logger.Debug("Crowd Control listener is starting...");
            tcpServer.Init("0.0.0.0", 38984, ISimpleTCPConnector.MessageType.JSON, ISimpleTCPConnector.FramingType.Null);
        }

        private class ManagerContext
        {
            public ClientContext ClientContext;

            public volatile bool IsLoggedIn;

            public Game Game;

            public string? Password;

            public Dictionary<string, Effect> Effects;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "<Pending>")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1404:Code analysis suppression should have justification", Justification = "<Pending>")]
        public enum DigestAlgorithm
        {
            NONE,
            SHA_512,
            WHIRLPOOL
        }

        private readonly ConcurrentDictionary<string, Effect> _effectMutexes = new();

        private void ReleaseAll(Effect effect) => ReleaseAll(effect.Mutex);

        private void ReleaseAll(IEnumerable<string?>? mutex)
        {
            if (mutex == null) { return; }
            foreach (string? m in mutex.Where(m => m != null)) _effectMutexes.TryRemove(m!, out _);
        }

        private bool TryGetMutexes(Effect effect)
        {
            IEnumerable<string?>? mutex = effect.Mutex;
            if (mutex == null) { return true; }

            List<string> gathered = new List<string>();
            foreach (string? m in mutex)
            {
                if (m == null) continue;
                if (_effectMutexes.TryAdd(m, effect)) gathered.Add(m);
                else
                {
                    ReleaseAll(gathered);
                    return false;
                }
            }

            return true;
        }

        internal static List<Effect> GetEffects(Game game, GameManager gameManager, GameStateTracker gameStateTracker)
        {
            var externalCommandFactory = new ExternalCommandFactory(gameManager);

            List<Effect> effectList = new List<Effect>
            {
                // Duration Round-based effects
                new IncreasePlayerSpeedEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DecreasePlayerSpeedEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new IncreaseCrewVisionEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DecreaseCrewVisionEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new IncreaseImpostorVisionEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DecreaseImpostorVisionEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new IncreaseKillCooldownEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DecreaseKillCooldownEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new IncreaseKillDistanceEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DecreaseKillDistanceEffect(game, gameManager, gameStateTracker, externalCommandFactory),

                // round-wide settings change effects
                new EnableAnonymousVotingEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DisableAnonymousVotingEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new EnableVisualTasksEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DisableVisualTasksEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new EnableConfirmImpostorEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DisableConfirmImpostorEffect(game, gameManager, gameStateTracker, externalCommandFactory),

                // Meeting change effects
                new IncrementVotingTimeEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new DecrementVotingTimeEffect(game, gameManager, gameStateTracker, externalCommandFactory),

                // Doors
                new CloseAllDoorsEffect(game, gameManager, gameStateTracker, externalCommandFactory),

                // Sabotage
                new SabotageCommsEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new FixCommsEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new SabotageReactorEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new FixReactorEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new SabotageElectricEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new FixElectricEffect(game, gameManager, gameStateTracker, externalCommandFactory),
                new SabotageO2Effect(game, gameManager, gameStateTracker, externalCommandFactory),
                new FixO2Effect(game, gameManager, gameStateTracker, externalCommandFactory),
            };

            foreach (Effect e in effectList)
            {
                try { e.Load(); }
                catch (Exception ex) { Log.Error(ex, "An effect preloader threw an exception."); }
            }

            return effectList;
        }
    }
}
