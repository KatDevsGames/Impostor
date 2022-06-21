using System;
using ConnectorLib.JSON;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.ExternalCommands.UpdateGame;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Impostor.Server.WarpWorld.CrowdControl.GameState.API;
using Impostor.Server.WarpWorld.ExternalCommands.API;
using Serilog;

namespace Impostor.Server.WarpWorld.CrowdControl.Effects
{
    /// <summary>
    /// An implementation of <see cref="Effect"/> which decrements the voting time by <see cref="VotingTimeDecrement"/>
    /// </summary>
    public class DecrementVotingTimeEffect : Effect
    {
        public override string Code => "DecrementVotingTime";

        public override uint ID => 18;

        public override EffectType Type => EffectType.Instant;

        private readonly IGame game;
        private readonly GameManager gameManager;
        private readonly IGameStateTracker gameStateTracker;
        private readonly IExternalCommandFactory externalCommandFactory;
        private readonly ILogger logger;

        internal const int VotingTimeDecrement = 10; // TODO - Config

        internal DecrementVotingTimeEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory)
            : this(game, gameManager, gameStateTracker, externalCommandFactory, Serilog.Log.ForContext<DecrementVotingTimeEffect>())
        {

        }

        /// <summary>
        /// Internal ctor for creating a <see cref="DecrementVotingTimeEffect"/>
        /// </summary>
        /// <param name="game">The <see cref="Game"/> this effect will run against</param>
        /// <param name="gameManager">A <see cref="GameManager"/> instance</param>
        /// <param name="gameStateTracker">An implementation of <see cref="IGameStateTracker"/> used to allow us to assess the current game state</param>
        internal DecrementVotingTimeEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory, ILogger logger)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.gameStateTracker = gameStateTracker ?? throw new ArgumentNullException(nameof(gameStateTracker));
            this.externalCommandFactory = externalCommandFactory ?? throw new ArgumentNullException(nameof(externalCommandFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called by the scheduler to load this effect, called once
        /// </summary>
        public override void Load()
        {
            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Load is called!");
            gameStateTracker.OnMeetingEndedEvent += ResetVotingTime;
        }

        /// <summary>
        /// Called by the scheduler to unload this effect, called once
        /// </summary>
        public override void Unload()
        {
            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Unload is called!");
            gameStateTracker.OnMeetingEndedEvent -= ResetVotingTime;
        }

        /// <summary>
        /// Identify whether this effect is ready to be ran.
        /// We can run this effect if we have infomration about this game, and it is currently in a valid state
        /// We can only run this game within a Meeting
        /// </summary>
        /// <returns><c>true</c> if the effect can be ran, <c>false</c> if not</returns>
        /// <remarks>
        /// We're fine with this running in the lobby, as it's not timed
        /// </remarks>
        public override bool IsReady()
        {
            if (gameStateTracker.HasGameState(game.Code) && gameStateTracker.HasStartingGameOptionsData(game.Code))
            {
                if (gameStateTracker.TryGetGameState(game.Code.Value, out CrowdControlGameState? gameState) && gameState.HasValue)
                {
                    bool validTrackedState = gameState.Value == CrowdControlGameState.InMeeting;
                    bool validGameState = game.GameState == GameStates.Started;
                    bool result = validTrackedState && validGameState;
                    logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} IsReady returned {result}!");
                    return result;
                }
            }
            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} IsReady returned false!");
            return false;
        }

        /// <summary>
        /// Called by the Scheduler to start the current effect
        /// </summary>
        /// <returns><c>true</c> if the effect was able to start, <c>false</c> if it wasn't</returns>
        public override bool Start(Request context)
        {
            if (game.GameState == GameStates.Destroyed || game.GameState == GameStates.Ended)
            {
                // The game has ended, so we can't do anything
                // We shouldn't get here, but on the off chance our tracked game state differs from the actual game state, this will
                // catch any weirdness
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned False, the game is destroyed or has ended");
                return false;
            }

            if (!gameStateTracker.TryGetGameState(game.Code.Value, out CrowdControlGameState? gameState) || !gameState.HasValue ||
                gameState != CrowdControlGameState.InMeeting)
            {
                // The game isn't currently in a meeting
                // We shouldn't get here, but just to cover all of our possible race conditions
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned False, unable to get the Starting Game Data, or the game is in a meeting");
                return false;
            }

            // Get the current options for the game
            GameOptionsData currentOptions = game.Options;

            // Update the options to include the decremented voting time
            var updateCommand = externalCommandFactory.GetExternalCommand<UpdateGameExternalCommand>();
            var updatePayload = new UpdateGamePayload()
            {
                Code = game.Code,
                GameOptions = currentOptions,
                Game = game,
            };
            updatePayload.GameOptions.VotingTime -= VotingTimeDecrement;
            IExternalCommandResponse<Game> updateResponse = updateCommand.PerformCommand(updatePayload).Result;

            // Return whether it succeeded
            bool success = updateResponse.ErrorCode == 0 && updateResponse.ResponseData != null;
            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned {success}!");
            return success;
        }

        /// <summary>
        /// Resets the voting time to what it was at the start
        /// </summary>
        public void ResetVotingTime(IMeetingEndedEvent e)
        {
            if (gameStateTracker.TryGetStartingGameOptionsData(game.Code.Value, out GameOptionsData? startingOptions) && startingOptions != null)
            {
                var updateCommand = externalCommandFactory.GetExternalCommand<UpdateGameExternalCommand>();
                var updatePayload = new UpdateGamePayload()
                {
                    Code = game.Code,
                    GameOptions = startingOptions,
                    Game = game,
                };

                IExternalCommandResponse<Game> updateResponse = updateCommand.PerformCommand(updatePayload).Result;
            }
        }
    }
}
