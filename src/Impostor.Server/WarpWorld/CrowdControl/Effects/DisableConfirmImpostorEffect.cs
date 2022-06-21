using System;
using ConnectorLib.JSON;
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
    /// An implementation of <see cref="Effect"/> for disabling Confirm Impostor
    /// </summary>
    public class DisableConfirmImpostorEffect : Effect
    {
        public override string Code => "DisableConfirmImpostor";

        public override uint ID => 16;

        public override EffectType Type => EffectType.Instant;

        private readonly IGame game;
        private readonly GameManager gameManager;
        private readonly IGameStateTracker gameStateTracker;
        private readonly IExternalCommandFactory externalCommandFactory;
        private readonly ILogger logger;

        internal DisableConfirmImpostorEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory)
            :this(game, gameManager, gameStateTracker, externalCommandFactory, Serilog.Log.ForContext<DisableConfirmImpostorEffect>())
        {

        }

        /// <summary>
        /// Internal ctor for creating a <see cref="DisableConfirmImpostorEffect"/>
        /// </summary>
        /// <param name="game">The <see cref="Game"/> this effect will run against</param>
        /// <param name="gameManager">A <see cref="GameManager"/> instance</param>
        /// <param name="gameStateTracker">An implementation of <see cref="IGameStateTracker"/> used to allow us to assess the current game state</param>
        internal DisableConfirmImpostorEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory, ILogger logger)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.gameStateTracker = gameStateTracker ?? throw new ArgumentNullException(nameof(gameStateTracker));
            this.externalCommandFactory = externalCommandFactory ?? throw new ArgumentNullException(nameof(externalCommandFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Identify whether this effect is ready to be ran.
        /// We can run this effect if we have infomration about this game, and it is currently in a valid state
        /// We also cannot run this effect if Confirm Impostor is already disabled
        /// </summary>
        /// <returns><c>true</c> if the effect can be ran, <c>false</c> if not</returns>
        public override bool IsReady()
        {
            if (gameStateTracker.HasGameState(game.Code) && gameStateTracker.HasStartingGameOptionsData(game.Code))
            {
                if (gameStateTracker.TryGetGameState(game.Code.Value, out CrowdControlGameState? gameState) && gameState.HasValue)
                {
                    bool validTrackedState = gameState.Value == CrowdControlGameState.InGame || gameState.Value == CrowdControlGameState.InMeeting;
                    bool validGameState = game.GameState == GameStates.Started || game.GameState == GameStates.Starting;
                    bool result = validTrackedState && validGameState && game.Options.ConfirmImpostor;
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

            if (!game.Options.ConfirmImpostor)
            {
                // If confirm impostor is already disabled, we can't enable it again. 
                // This should get caught by IsReady but can't hurt to check twice
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned False, ConfirmImpostor is already disabled!");
                return false;
            }

            // Get the current options for the game
            GameOptionsData currentOptions = game.Options;

            // Update the options to disable confirm impostor
            var updateCommand = externalCommandFactory.GetExternalCommand<UpdateGameExternalCommand>();
            var updatePayload = new UpdateGamePayload()
            {
                Code = game.Code,
                GameOptions = currentOptions,
                Game = game,
            };
            updatePayload.GameOptions.ConfirmImpostor = false;
            IExternalCommandResponse<Game> updateResponse = updateCommand.PerformCommand(updatePayload).Result;

            // Return whether it succeeded
            bool success = updateResponse.ErrorCode == 0 && updateResponse.ResponseData != null;
            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned {success}!");
            return success;
        }
    }
}
