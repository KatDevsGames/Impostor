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
    /// An implementation of <see cref="Effect"/> which fixes the electrical sabotage
    /// </summary>
    public class FixElectricEffect : Effect
    {
        public override string Code => "FixElectric";

        public override uint ID => 25;

        public override EffectType Type => EffectType.Instant;

        private readonly IGame game;
        private readonly GameManager gameManager;
        private readonly IGameStateTracker gameStateTracker;
        private readonly IExternalCommandFactory externalCommandFactory;
        private readonly ILogger logger;

        internal FixElectricEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory)
            : this (game, gameManager, gameStateTracker, externalCommandFactory, Serilog.Log.ForContext<FixElectricEffect>())
        {

        }

        /// <summary>
        /// Internal ctor for creating a <see cref="FixElectricEffect"/>
        /// </summary>
        /// <param name="game">The <see cref="Game"/> this effect will run against</param>
        /// <param name="gameManager">A <see cref="GameManager"/> instance</param>
        /// <param name="gameStateTracker">An implementation of <see cref="IGameStateTracker"/> used to allow us to assess the current game state</param>
        internal FixElectricEffect(IGame game, GameManager gameManager, IGameStateTracker gameStateTracker, IExternalCommandFactory externalCommandFactory, ILogger logger)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.gameStateTracker = gameStateTracker ?? throw new ArgumentNullException(nameof(gameStateTracker));
            this.externalCommandFactory = externalCommandFactory ?? throw new ArgumentNullException(nameof(externalCommandFactory));
            this.logger = logger;
        }

        /// <summary>
        /// Identify whether this effect is ready to be ran.
        /// We can run this effect if we have infomration about this game, and it is currently in the game (ie, not in a Lobby or a Meeting)
        /// We can't run this game if the Electric system is already fixed
        /// </summary>
        /// <returns><c>true</c> if the effect can be ran, <c>false</c> if not</returns>
        public override bool IsReady()
        {
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
                        if (t != null && t.TryGetSabotageSystem(SystemTypes.Electrical, out IActivatable act))
                        {
                            SwitchSystem? elecAct = act as SwitchSystem;
                            if (elecAct != null)
                            {
                                bool result = elecAct.IsActive;
                                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} IsReady returned {result}!");
                                return result;
                            }
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

            // The Sabotage event has already been fixed (such as by a player)
            var t = game.GameNet.ShipStatus as InnerShipStatus;
            if (t == null || !t.TryGetSabotageSystem(SystemTypes.Electrical, out IActivatable act) || act == null)
            {
                logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned False, Unable to find 'Electrical' as a Sabotage System!");
                return false;
            }
            else
            {
                SwitchSystem? elec = act as SwitchSystem;
                if (elec != null && !elec.IsActive)
                {
                    logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned False, Electrical is already fixed!");
                    return false;
                }
            }

            // Trigger the sabotage command
            var setSabotageCommand = externalCommandFactory.GetExternalCommand<SetSabotageExternalCommand>();
            var setSabotagePayload = new SetSabotagePayload()
            {
                Code = game.Code,
                SystemType = SystemTypes.Electrical,
                BooleanSystemState = false
            };

            // Run the Command
            IExternalCommandResponse<Game> updateResponse = setSabotageCommand.PerformCommand(setSabotagePayload).Result;

            // Return whether it succeeded
            bool success = updateResponse.ErrorCode == 0 && updateResponse.ResponseData != null;

            // Update the server internal data because this doesn't actually send a message to the server, just the clients
            SwitchSystem? elecAct = act as SwitchSystem;
            if (success && elecAct != null)
            {
                elecAct.ExpectedSwitches = 0;
                elecAct.ActualSwitches = 0;
                elecAct.Value = byte.MaxValue;
            }

            logger.Write(Serilog.Events.LogEventLevel.Debug, $"{this.GetType().Name} Start returned {success}!");
            return success;
        }
    }
}
