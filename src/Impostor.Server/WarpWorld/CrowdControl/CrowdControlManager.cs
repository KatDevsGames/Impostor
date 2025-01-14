using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConnectorLib;
using ConnectorLib.JSON;
using ConnectorLib.SimpleTCP;
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
using ClientContext = ConnectorLib.SimpleTCP.SimpleTCPServerConnector<ConnectorLib.JSON.Response, ConnectorLib.JSON.Request>.ClientContext;
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
        private readonly ConcurrentDictionary<ISimpleTCPContext, ManagerContext> contexts = new();
        private readonly ConcurrentDictionary<string, GameCode> codes = new();
        private readonly ConcurrentDictionary<GameCode, IntRef> connectionCounts = new();

        private readonly GameStateTracker gameStateTracker;
        private readonly GameManager gameManager;
        private readonly IEventManager eventManager;

        private readonly ILogger logger;

        private readonly Random rng = new();

        private class IntRef
        {
            public int value;
            public int Incr() => Interlocked.Increment(ref value);
            public bool IncrIfZero() => Interlocked.CompareExchange(ref value, 1, 0) == 0;
            public int Decr() => Interlocked.Decrement(ref value);
        }

        public CrowdControlManager(GameStateTracker gameStateTracker, GameManager gameManager, IEventManager eventManager)
        {
            this.gameStateTracker = gameStateTracker ?? throw new ArgumentNullException(nameof(gameStateTracker));
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));

            logger = Log.ForContext<CrowdControlManager>();

            ConnectorLib.Log.OnMessage += (s, _) => logger.Debug(s);
            //var gameCodeCrowdControlSessionLookup = new ConcurrentDictionary<int, CrowdControlSessionInfo>();

            tcpServer = new();

            tcpServer.ClientConnected += OnClientConnected;
            tcpServer.ClientDisconnected += OnClientDisconnected;
            tcpServer.MessageParsed += OnMessageParsed;

            gameStateTracker.OnGameCreatedEvent += OnGameCreated;
            gameStateTracker.OnGameDestroyedEvent += OnGameDestroyed;

            logger.Debug("The Crowd Control service is starting.");
            Start();
        }

        private void OnClientConnected(object? sender, ClientContext context)
        {
            contexts[context] = new ManagerContext(context);
            logger.Debug($"Context object {context.GetHashCode()} was added. Current contexts: {string.Join(", ", contexts.Values.Select(cx => cx.GetHashCode()))}");
            tcpServer.Send(new Response { type = ResponseType.Login }, context);
        }

        private void OnClientDisconnected(object? sender, ClientContext context)
        {
            if (!contexts.TryRemove(context, out var c)) return;
            logger.Debug($"Context object {c.GetHashCode()} was removed. Remaining contexts: {string.Join(", ", contexts.Values.Select(cx => cx.GetHashCode()))}");
            if (!c.IsLoggedIn) return;
            if (c.Game == null) return;
            if (!connectionCounts.TryGetValue(c.Game.Code, out var r)) return;

            r.Decr();
        }

        private void OnGameCreated(IGameCreatedEvent e)
        {
            GameCode gameCode = e.Game.Code;
            if (gameManager.TryFind(gameCode, out Game? game)) GetEffects(game);
            connectionCounts.TryAdd(gameCode, new IntRef());
        }

        private void OnGameDestroyed(IGameDestroyedEvent e)
        {
            GameCode gameCode = e.Game.Code;
            if (gameEffectCache.TryGetValue(gameCode, out var effects))
            {
                foreach (Effect ef in effects)
                {
                    try { ef.Unload(); }
                    catch (Exception ex) { Log.Error(ex, "An effect postloader threw an exception."); }
                }
            }

            foreach (var g in codes)
            {
                if (g.Value != gameCode) continue;
                codes.TryRemove(g);
            }

            gameEffectCache.Remove(gameCode);
            connectionCounts.Remove(gameCode, out _);
        }



        private async void OnMessageParsed(ISimpleTCPConnector<Response, Request, ISimpleTCPContext> sender, Request message, ISimpleTCPContext context)
        {
            try { await MessageParsed(message, (ClientContext)context); }
            catch (Exception e) { logger.Error(e, e.Message); }
        }

        private async Task MessageParsed(Request request, ClientContext context)
        {
            logger.Debug($"Got a request of type {request.type:G} from client {context.GetHashCode()}.");
            try
            {
                if (!contexts.TryGetValue(context, out var c))
                {
                    logger.Debug($"...but the local context object couldn't be found. Available contexts: {string.Join(", ", contexts.Values.Select(cx => cx.GetHashCode()))}");
                    return;
                }

                Response response = new() { id = request.id };
                switch (request.type)
                {
                    case RequestType.Test:
                    {
                        if (!c.IsLoggedIn)
                        {
                            logger.Debug("...but the player was not logged in.");
                            response.status = EffectResult.NotReady;
                            break;
                        }

                        if (!c.Effects.TryGetValue(request.code, out Effect e))
                        {
                            logger.Debug("...but the effect type was not found.");
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
                            logger.Debug("...but the player was not logged in.");
                            response.status = EffectResult.NotReady;
                            break;
                        }

                        if (!c.Effects.TryGetValue(request.code, out Effect e))
                        {
                            logger.Debug("...but the effect type was not found.");
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
                                ScheduleStop(e, request.duration.HasValue ? TimeSpan.FromSeconds(request.duration.Value) : e.Duration).Forget();
                            }
                            else
                            {
                                logger.Debug("...but the effect couldn't start.");
                                response.status = EffectResult.Retry;
                                ReleaseAll(e);
                            }
                        }
                        else
                        {
                            logger.Debug("...but the required mutexes could not be acquired."); 
                            response.status = EffectResult.Retry;
                        }
                        break;
                    }
                    case RequestType.Stop:
                    {
                        if (!c.IsLoggedIn)
                        {
                            logger.Debug("...but the player was not logged in.");
                            response.status = EffectResult.NotReady;
                            break;
                        }

                        if (!c.Effects.TryGetValue(request.code, out Effect e))
                        {
                            logger.Debug("...but the effect type was not found.");
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
                        logger.Debug($"Got a login request with password {password}...");
                        if (codes.TryGetValue(password, out GameCode gc) && gameManager.TryFind(gc, out Game? game))
                        {
                            if (!connectionCounts[game.Code].IncrIfZero())
                            {
                                logger.Debug($"Login rejected for password {password} with #{connectionCounts[game.Code].value} ongoing connections.");
                                Close(c, "Host has already connected. If you are the host, close the lobby and start again.");
                                return;
                            }
                            logger.Debug($"Attaching Impostor game {game.Code} to Crowd Control session {context.GetHashCode()}.");
                            c.Game = game;
                            c.Password = password;
                            c.Effects = GetEffects(game).ToDictionary(e => e.Code);
                            response.type = ResponseType.LoginSuccess;
                            c.IsLoggedIn = true;
                            logger.Debug($"{context.GetHashCode()} is now logged in.");
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
            tcpServer.Host = "0.0.0.0";
            tcpServer.Port = 38984;
            tcpServer.Init();
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private class ManagerContext: IEquatable<ManagerContext>
        {
            public readonly ClientContext ClientContext;

            public volatile bool IsLoggedIn;

            public Game? Game;

            public string? Password;

            public Dictionary<string, Effect>? Effects;

            public ManagerContext(ClientContext clientContext) => ClientContext = clientContext;

            public bool Equals(ManagerContext? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return ClientContext.Equals(other.ClientContext);
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ManagerContext)obj);
            }

            public override int GetHashCode() => ClientContext.GetHashCode();
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
            if (mutex == null) return;
            foreach (string? m in mutex.Where(m => m != null)) _effectMutexes.TryRemove(m!, out _);
        }

        private bool TryGetMutexes(Effect effect)
        {
            IEnumerable<string?>? mutex = effect.Mutex;
            if (mutex == null) return true;

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

        private readonly Dictionary<GameCode, List<Effect>> gameEffectCache = new();
        private readonly object gameEffectCacheLock = new();

        internal List<Effect> GetEffects(Game game)
        {
            GameCode gameCode = game.Code;
            if (gameEffectCache.TryGetValue(gameCode, out var c)) return c;
            lock (gameEffectCacheLock)
            {
                if (gameEffectCache.TryGetValue(gameCode, out var c2)) return c2;
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

                gameEffectCache.Add(gameCode, effectList);
                return effectList;
            }
        }
    }
}
