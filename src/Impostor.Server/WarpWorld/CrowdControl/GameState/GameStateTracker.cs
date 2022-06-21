using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Managers;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Innersloth;
using Impostor.Server.WarpWorld.CrowdControl.GameState.API;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Impostor.Server.WarpWorld.CrowdControl.GameState
{
    /// <summary>
    /// An implementation of <see cref="IGameStateTracker"/> able to keep track of the states of currently running games.
    /// It does this by implementing <see cref="IEventListener"/> and registering itself to the <see cref="IEventManager"/>
    /// </summary>
    public class GameStateTracker : IGameStateTracker, IEventListener, IHostedService, IDisposable
    {
        private readonly IEventManager eventManager;
        private readonly IDisposable listenerDisposable;
        private readonly Dictionary<int, CrowdControlGameState> gameStates;
        private readonly Dictionary<int, GameOptionsData> startingGameOptions;
        private readonly ILogger logger;

        public event GameCreatedDelegate OnGameCreatedEvent;
        public event GameDestroyedDelegate OnGameDestroyedEvent;
        public event MeetingStartedDelegate OnMeetingStartedEvent;
        public event MeetingEndedDelegate OnMeetingEndedEvent;
        public event GameStartingDelegate OnGameStartingEvent;
        public event GameEnteredLobbyDelegate OnGameEnteredLobbyEvent;
        public event GameExitedLobbyDelegate OnGameExitedLobbyEvent;

        /// <summary>
        /// Constructor for creating a <see cref="GameStateTracker"/>
        /// </summary>
        /// <param name="eventManager">An <see cref="IEventManager"/> implementation to register to</param>
        public GameStateTracker(IEventManager eventManager)
        {
            this.eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));

            gameStates = new Dictionary<int, CrowdControlGameState>();
            startingGameOptions = new Dictionary<int, GameOptionsData>();
            logger = Log.ForContext<GameStateTracker>();

            listenerDisposable = eventManager.RegisterListener(this);
        }

        /// <summary>
        /// Implementation of <see cref="IDisposable.Dispose"/> used to clean up the event listener
        /// </summary>
        public void Dispose()
        {
            try
            {
                listenerDisposable?.Dispose();
            }
            catch (Exception e)
            {

            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Do Nothing
            logger.Warning("GST - Game State Tracker Started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Do Nothing
            logger.Warning("GST - Game State Tracker Stopped");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Queries the <see cref="GameOptionsData"/> for the provided game when the round started
        /// </summary>
        /// <param name="gameCode">The integer game code to query</param>
        /// <param name="data">The <see cref="GameOptionsData"/> for that game when it started, <see langword="null"/> if it couldnt be found</param>
        /// <returns><see langword="true"/> if the game could be found, and a <see cref="GameOptionsData"/> was provided, <see langword="false"/> if not</returns>
        public bool TryGetStartingGameOptionsData(int gameCode, out GameOptionsData? data)
        {
            data = null;
            if (startingGameOptions.ContainsKey(gameCode))
            {
                data = startingGameOptions[gameCode];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Queries the current state of the game, represented via <see cref="CrowdControlGameState"/>
        /// </summary>
        /// <param name="gameCode">The integer game code to query</param>
        /// <param name="state">The <see cref="CrowdControlGameState"/> for that game currently, <see langword="null"/> if it couldnt be found</param>
        /// <returns><see langword="true"/> if the game could be found, and a <see cref="CrowdControlGameState"/> was provided, <see langword="false"/> if not</returns>
        public bool TryGetGameState(int gameCode, out CrowdControlGameState? state)
        {
            state = null;
            if (gameStates.ContainsKey(gameCode))
            {
                state = gameStates[gameCode];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if we have any Starting <see cref="GameOptionsData"/> for the provided game code
        /// </summary>
        public bool HasStartingGameOptionsData(int gameCode)
        {
            return startingGameOptions.ContainsKey(gameCode);
        }

        /// <summary>
        /// Checks if we have any <see cref="CrowdControlGameState"/> information for the provided game code
        /// </summary>
        public bool HasGameState(int gameCode)
        {
            return gameStates.ContainsKey(gameCode);
        }

        #region Events
        [EventListener]
        public void OnGameCreated(IGameCreatedEvent e)
        {
            gameStates[e.Game.Code.Value] = CrowdControlGameState.InLobby;
            startingGameOptions[e.Game.Code.Value] = e.Game.Options.Clone();
            logger.Warning($"GST - Game State of {e.Game.Code.Value} changed to {gameStates[e.Game.Code.Value]}");
            OnGameCreatedEvent?.Invoke(e);
            OnGameEnteredLobbyEvent?.Invoke(e);
        }

        [EventListener]
        public void OnGameStarting(IGameStartingEvent e)
        {
            gameStates[e.Game.Code.Value] = CrowdControlGameState.Starting;
            startingGameOptions[e.Game.Code.Value] = e.Game.Options.Clone();
            logger.Warning($"GST - Game State of {e.Game.Code.Value} changed to {gameStates[e.Game.Code.Value]}");
            OnGameExitedLobbyEvent?.Invoke(e);
            OnGameStartingEvent?.Invoke(e);
        }

        [EventListener]
        public void OnGameStarted(IGameStartedEvent e)
        {
            gameStates[e.Game.Code.Value] = CrowdControlGameState.InGame;
            logger.Warning($"GST - Game State of {e.Game.Code.Value} changed to {gameStates[e.Game.Code.Value]}");
        }

        [EventListener]
        public void OnGameEnded(IGameEndedEvent e)
        {
            gameStates[e.Game.Code.Value] = CrowdControlGameState.Ended;
            logger.Warning($"GST - Game State of {e.Game.Code.Value} changed to {gameStates[e.Game.Code.Value]}");
        }

        [EventListener]
        public void OnGameDestroyed(IGameDestroyedEvent e)
        {
            gameStates[e.Game.Code.Value] = CrowdControlGameState.Destroyed;
            logger.Warning($"GST - Game State of {e.Game.Code.Value} changed to {gameStates[e.Game.Code.Value]}");
            gameStates.Remove(e.Game.Code.Value);
            OnGameDestroyedEvent?.Invoke(e);
        }

        [EventListener]
        public void OnMeetingStarted(IMeetingStartedEvent e)
        {
            gameStates[e.Game.Code.Value] = CrowdControlGameState.InMeeting;
            logger.Warning($"GST - Game State of {e.Game.Code.Value} changed to {gameStates[e.Game.Code.Value]}");
            OnMeetingStartedEvent?.Invoke(e);
        }

        [EventListener]
        public void OnMeetingEnded(IMeetingEndedEvent e)
        {
            gameStates[e.Game.Code.Value] = CrowdControlGameState.InGame;
            logger.Warning($"GST - Game State of {e.Game.Code.Value} changed to {gameStates[e.Game.Code.Value]}");
            OnMeetingEndedEvent?.Invoke(e);
        }

        [EventListener]
        public void OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            // If the game's state was previously 'Ended' and a player has been spawned, we're back in the lobby
            if (gameStates.ContainsKey(e.Game.Code.Value) && gameStates[e.Game.Code.Value] == CrowdControlGameState.Ended)
            {
                gameStates[e.Game.Code.Value] = CrowdControlGameState.InLobby;
                OnGameEnteredLobbyEvent?.Invoke(e);
                logger.Warning($"GST - Game State of {e.Game.Code.Value} changed to {gameStates[e.Game.Code.Value]}");
            }
        }
        #endregion
    }
}
