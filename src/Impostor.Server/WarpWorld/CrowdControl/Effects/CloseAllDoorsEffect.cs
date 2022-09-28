using System;
using ConnectorLib.JSON;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.ExternalCommands.CloseAllDoors;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Impostor.Server.WarpWorld.CrowdControl.GameState.API;
using Impostor.Server.WarpWorld.ExternalCommands.API;
using Serilog;

namespace Impostor.Server.WarpWorld.CrowdControl.Effects
{
    /// <summary>
    /// An implementation of <see cref="Effect"/> which closes all the doors
    /// We count this as a Timed effect despite it being instant in order to implicitly provide a 
    /// 'cooldown' on how long it takes the doors to then open again
    /// </summary>
    public class CloseAllDoorsEffect : Effect
    {
        public override string Code => "CloseAllDoors";

        public override uint ID => EffectID;

        public override EffectType Type => EffectType.Timed;

        /// <summary>
        /// The doors stay closed for 10 seconds
        /// </summary>
        public override TimeSpan Duration { get; } = TimeSpan.FromSeconds(10);

        public override string[] Mutex => new string[] { "CloseAllDoors" };

        private readonly IGame game;
        private readonly GameManager gameManager;
        private readonly IGameStateTracker gameStateTracker;
        private readonly IExternalCommandFactory externalCommandFactory;
        private readonly ILogger logger;

        public const uint EffectID = 19;

        /// <summary>
        /// Internal ctor for creating a <see cref="CloseAllDoorsEffect"/>
        /// </summary>
        /// <param name="game">The <see cref="Game"/> this effect will run against</param>
        /// <param name="gameManager">A <see cref="GameManager"/> instance</param>
        /// <param name="gameStateTracker">An implementation of <see cref="IGameStateTracker"/> used to allow us to assess the current game state</param>
        /// <param name="externalCommandFactory">An implementation of <see cref="IExternalCommandFactory"/> for making external commands to execute</param>
        internal CloseAllDoorsEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory) 
            : this(game, gameManager, gameStateTracker, externalCommandFactory, Serilog.Log.ForContext<CloseAllDoorsEffect>())
        {

        }

        /// <summary>
        /// Internal ctor for creating a <see cref="CloseAllDoorsEffect"/> which allows specifying of a logger
        /// </summary>
        /// <param name="game">The <see cref="Game"/> this effect will run against</param>
        /// <param name="gameManager">A <see cref="GameManager"/> instance</param>
        /// <param name="gameStateTracker">An implementation of <see cref="IGameStateTracker"/> used to allow us to assess the current game state</param>
        /// <param name="externalCommandFactory">An implementation of <see cref="IExternalCommandFactory"/> for making external commands to execute</param>
        /// <param name="logger">An implementation of <see cref="ILogger"/> for logging</param>
        internal CloseAllDoorsEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory, ILogger logger)
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
        public override bool IsReady()
        {
            // No Doors on Mira HQ....
            if (game.Options.Map == MapTypes.MiraHQ)
            {
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} IsReady returned False, we are on MiraHQ and don't support this effect here!");
                return false;
            }

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

            // Trigger the close all doors command
            var closeAllDoorsCommand = externalCommandFactory.GetExternalCommand<CloseAllDoorsExternalCommand>();
            var closeAllDoorsPayload = new CloseAllDoorsPayload()
            {
                Code = game.Code,
            };
            IExternalCommandResponse<Game> updateResponse = closeAllDoorsCommand.PerformCommand(closeAllDoorsPayload).Result;

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
            // Do Nothing
            // We only count this as a timed effect in order to get an implicit 'cooldown'
            bool result = base.Stop();
            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Stop returned {result}");
            return result;
        }
    }
}
