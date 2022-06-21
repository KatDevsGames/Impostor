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
    /// An implementation of <see cref="Effect"/> which temporarily decreases the players speed
    /// </summary>
    public class DecreasePlayerSpeedEffect : Effect
    {
        public override string Code => "DecreasePlayerSpeed";

        public override uint ID => 2;

        public override EffectType Type => EffectType.Timed;

        public override TimeSpan Duration { get; } = TimeSpan.FromSeconds(30); // TODO - Config

        public override string[] Mutex => new string[] { "PlayerSpeed" };

        private readonly IGame game;
        private readonly GameManager gameManager;
        private readonly IGameStateTracker gameStateTracker;
        private readonly IExternalCommandFactory externalCommandFactory;
        private readonly ILogger logger;

        internal const float DecrementedPlayerSpeedScale = 0.5f; // TODO - Config

        internal DecreasePlayerSpeedEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory)
            : this(game, gameManager, gameStateTracker, externalCommandFactory, Serilog.Log.ForContext<DecreasePlayerSpeedEffect>())
        {

        }

        /// <summary>
        /// Internal ctor for creating a <see cref="DecreasePlayerSpeedEffect"/>
        /// </summary>
        /// <param name="game">The <see cref="Game"/> this effect will run against</param>
        /// <param name="gameManager">A <see cref="GameManager"/> instance</param>
        /// <param name="gameStateTracker">An implementation of <see cref="IGameStateTracker"/> used to allow us to assess the current game state</param>
        internal DecreasePlayerSpeedEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory, ILogger logger)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.gameStateTracker = gameStateTracker ?? throw new ArgumentNullException(nameof(gameStateTracker));
            this.externalCommandFactory = externalCommandFactory ?? throw new ArgumentNullException(nameof(externalCommandFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Identify whether this effect is ready to be ran.
        /// We can run this effect if we have infomration about this game, and it is currently in the game (ie, not in a Lobby or a Meeting)
        /// </summary>
        /// <returns><c>true</c> if the effect can be ran, <c>false</c> if not</returns>
        /// <remarks>
        /// We could run this in the lobby, but when this is done we need to be able to change the speed back to what it was when we started, settings can be changed anytime during the lobby
        /// so to minimize messiness here the <see cref="IGameStateTracker"/> takes a snapshot of the settings when the game starts, we then use this snapshot to correctly 'end' the effect.
        /// Excluding this effect from being ran during the Lobby ensures we don't need to keep track of settings changes and we don't need to worry about possible race conditions that arise from that.
        /// </remarks>
        public override bool IsReady()
        {
            if (gameStateTracker.HasGameState(game.Code) && gameStateTracker.HasStartingGameOptionsData(game.Code))
            {
                if (gameStateTracker.TryGetGameState(game.Code.Value, out CrowdControlGameState? gameState) && gameState.HasValue)
                {
                    bool result = gameState.Value == CrowdControlGameState.InGame && game.GameState == GameStates.Started;
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
            if (game.GameState != GameStates.Started)
            {
                // The game hasn't started yet, so we definitely can't do anything with it
                // We shouldn't get here, but on the off chance our tracked game state differs from the actual game state, this will
                // catch any weirdness
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned False, the game has not yet started!");
                return false;
            }

            // Get the starting options for the game
            if (!gameStateTracker.TryGetStartingGameOptionsData(game.Code, out GameOptionsData? startingGameData) || startingGameData == null)
            {
                // Something went wrong here, we weren't able to reference any data about this game
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned False, unable to get the Starting Game Data");
                return false;
            }


            // Get the current options for the game
            GameOptionsData currentOptions = game.Options;

            // Update the options to include decreased player speed
            var updateCommand = externalCommandFactory.GetExternalCommand<UpdateGameExternalCommand>();
            var updatePayload = new UpdateGamePayload()
            {
                Code = game.Code,
                GameOptions = currentOptions,
                Game = game,
            };
            updatePayload.GameOptions.PlayerSpeedMod = startingGameData.PlayerSpeedMod * DecrementedPlayerSpeedScale;
            IExternalCommandResponse<Game> updateResponse = updateCommand.PerformCommand(updatePayload).Result;
            logger.Information($"{this.GetType().Name} Start attempted to set player speed mod to {updatePayload.GameOptions.PlayerSpeedMod}");

            // Return whether it succeeded
            bool success = updateResponse.ErrorCode == 0 && updateResponse.ResponseData != null;

            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned {success}!");
            return success;
        }

        /// <summary>
        /// Called by the Scheduler to stop the current effect
        /// </summary>
        /// <returns><c>true</c> if the effect was able to stop correctly, <c>false</c> if it wasn't</returns>
        public override bool Stop()
        {
            if (game.GameState != GameStates.Started)
            {
                // The game hasn't started yet, so we definitely can't do anything with it
                // We shouldn't get here, but on the off chance our tracked game state differs from the actual game state, this will
                // catch any weirdness
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Stop returned False, the game has not yet started!");
                return false;
            }

            // Get the starting options for the game
            if (!gameStateTracker.TryGetStartingGameOptionsData(game.Code, out GameOptionsData? startingGameData) || startingGameData == null)
            {
                // Something went wrong here, we weren't able to reference any data about this game
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Stop returned False, unable to get the Starting Game Data");
                return false;
            }

            // Update the options to be whatever they are currently, but with the player speed set back to what it was when the game started
            var updateCommand = externalCommandFactory.GetExternalCommand<UpdateGameExternalCommand>();
            var updatePayload = new UpdateGamePayload()
            {
                Code = game.Code,
                GameOptions = game.Options,
                Game = game,
            };
            updatePayload.GameOptions.PlayerSpeedMod = startingGameData.PlayerSpeedMod;
            IExternalCommandResponse<Game> updateResponse = updateCommand.PerformCommand(updatePayload).Result;

            // Return whether it succeeded
            bool success = updateResponse.ErrorCode == 0 && updateResponse.ResponseData != null;
            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Stop returned {success}!");
            return success;
        }
    }
}
