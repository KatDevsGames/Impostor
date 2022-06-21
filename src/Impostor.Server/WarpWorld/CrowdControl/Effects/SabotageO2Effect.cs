using System;
using ConnectorLib.JSON;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.ExternalCommands.SetSabotage;
using Impostor.Server.Net.Inner.Objects.ShipStatus;
using Impostor.Server.Net.Inner.Objects.Systems;
using Impostor.Server.Net.Inner.Objects.Systems.ShipStatus;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Impostor.Server.WarpWorld.CrowdControl.GameState.API;
using Impostor.Server.WarpWorld.ExternalCommands.API;
using Serilog;

namespace Impostor.Server.WarpWorld.CrowdControl.Effects
{
    /// <summary>
    /// An implementation <see cref="Effect"/> which sabotages the O2 System
    /// </summary>
    public class SabotageO2Effect : Effect
    {
        public override string Code => "SabotageO2";

        public override uint ID => EffectID;

        public override EffectType Type => EffectType.Instant;

        private readonly IGame game;
        private readonly GameManager gameManager;
        private readonly IGameStateTracker gameStateTracker;
        private readonly IExternalCommandFactory externalCommandFactory;
        private readonly ILogger logger;

        public const uint EffectID = 26;

        internal SabotageO2Effect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory)
            : this(game, gameManager, gameStateTracker, externalCommandFactory, Serilog.Log.ForContext<SabotageO2Effect>())
        {

        }

        /// <summary>
        /// Internal ctor for creating a <see cref="SabotageO2Effect"/>
        /// </summary>
        /// <param name="game">The <see cref="Game"/> this effect will run against</param>
        /// <param name="gameManager">A <see cref="GameManager"/> instance</param>
        /// <param name="gameStateTracker">An implementation of <see cref="IGameStateTracker"/> used to allow us to assess the current game state</param>
        internal SabotageO2Effect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory, ILogger logger)
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
        /// We can't run this game if the O2 system is already sabotaged
        /// </summary>
        /// <returns><c>true</c> if the effect can be ran, <c>false</c> if not</returns>
        public override bool IsReady()
        {
            // If the Map is Polus or Airship - it doesn't have an O2...
            if (game.Options.Map == MapTypes.Polus ||
                game.Options.Map == MapTypes.Airship)
            {
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} IsReady returned False, We are on Polus or Airship, which don't have O2 Sabotage Systems!");
                return false;
            }

            // If we know about this game
            if (gameStateTracker.HasGameState(game.Code) && gameStateTracker.HasStartingGameOptionsData(game.Code))
            {
                // We can get the game state
                if (gameStateTracker.TryGetGameState(game.Code.Value, out CrowdControlGameState? gameState) && gameState.HasValue)
                {
                    // The game state is considered valid
                    if (gameState.Value == CrowdControlGameState.InGame && game.GameState == GameStates.Started)
                    {
                        // And the system isn't already fixed
                        var t = game.GameNet.ShipStatus as InnerShipStatus;
                        if (t != null && t.TryGetSabotageSystem(SystemTypes.LifeSupp, out IActivatable act))
                        {
                            bool result = !act.IsActive;
                            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} IsReady returned {result}!");
                            return result;
                        }
                    }
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

            // The Sabotage event has already been triggered (such as by a player)
            var t = game.GameNet.ShipStatus as InnerShipStatus;
            if (t == null || !t.TryGetSabotageSystem(SystemTypes.LifeSupp, out IActivatable act) || act.IsActive)
            {
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned False, O2 is already sabotaged!");
                return false;
            }

            // Trigger the sabotage command
            var setSabotageCommand = externalCommandFactory.GetExternalCommand<SetSabotageExternalCommand>();
            var setSabotagePayload = new SetSabotagePayload()
            {
                Code = game.Code,
                SystemType = SystemTypes.LifeSupp,
            };

            // Timer for reactor changes on map apparently
            float countdownVal = 0;
            switch (game.Options.Map)
            {
                case MapTypes.Skeld:
                    countdownVal = 30;
                    break;
                case MapTypes.MiraHQ:
                    countdownVal = 45;
                    break;
                case MapTypes.Polus:
                    countdownVal = 60;
                    break;
                default:
                    // We don't support this map!
                    return false;
            }
            setSabotagePayload.CountdownSystemState = countdownVal;

            // Run the Command
            IExternalCommandResponse<Game> updateResponse = setSabotageCommand.PerformCommand(setSabotagePayload).Result;

            // Return whether it succeeded
            bool success = updateResponse.ErrorCode == 0 && updateResponse.ResponseData != null;

            // Update the server internal data because this doesn't actually send a message to the server, just the clients
            LifeSuppSystemType? lifeSuppAct = act as LifeSuppSystemType;
            if (success && lifeSuppAct != null)
            {
                lifeSuppAct.Countdown = countdownVal;
            }

            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned {success}!");
            return success;
        }
    }
}
