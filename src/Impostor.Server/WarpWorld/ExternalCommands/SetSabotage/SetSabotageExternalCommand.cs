using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Net.Messages;
using Impostor.Hazel;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.Net.Inner;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Serilog;

namespace Impostor.Server.ExternalCommands.SetSabotage
{
    /// <summary>
    /// An implementation of <see cref="IExternalCommand{T, TResult}"/> for fixing or triggering variable sabotage events
    /// Takes in a <see cref="SetSabotagePayload"/> and returns a wrapped <see cref="Game"/>
    /// </summary>
    internal class SetSabotageExternalCommand : IExternalCommand<SetSabotagePayload, Game>
    {
        private readonly GameManager gameManager;
        private readonly ILogger logger;

        /// <summary>
        /// Constructor for creating a <see cref="SetSabotageExternalCommand"/>
        /// </summary>
        internal SetSabotageExternalCommand(GameManager gameManager) : this(gameManager, Log.ForContext<SetSabotageExternalCommand>())
        {

        }

        /// <summary>
        /// Constructor for creating a <see cref="SetSabotageExternalCommand"/> which allows you to specify a logger
        /// </summary>
        internal SetSabotageExternalCommand(GameManager gameManager, ILogger logger)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Triggers or fixes the appropriate sabotage event on the specified game
        /// </summary>
        public virtual async Task<IExternalCommandResponse<Game>> PerformCommand(SetSabotagePayload payload)
        {
            Game game = gameManager.Find(payload.Code);

            if (game != null)
            {
                try
                {
                    // We want to create a message that we send to the rest of the clients
                    // Depending on the data provided (it differs slightly for different sabotage types)
                    // we format the message differently
                    using (var writer = MessageWriter.Get(Api.Net.Messages.MessageType.Reliable))
                    {
                        // Write the GameData and DataFlag messages with our code and Net ID
                        writer.StartMessage(MessageFlags.GameData);
                        writer.Write(game.Code);

                        writer.StartMessage(GameDataTag.DataFlag);
                        writer.WritePacked(game.GameNet.ShipStatus.NetId);

                        // Write the 'System Type' which represents the sabotage type we care about
                        writer.StartMessage((byte)payload.SystemType);
                        //writer.WritePacked(1 << (int)payload.SystemType);

                        // If a bool was provided for this sabotage system, write that
                        if (payload.BooleanSystemState.HasValue)
                        {
                            writer.Write(payload.BooleanSystemState.Value);
                        }

                        // If a countdown was provided for this sabotage system, write that
                        if (payload.CountdownSystemState.HasValue)
                        {
                            writer.Write(payload.CountdownSystemState.Value);
                        }

                        // If a 'Number of Players holding buttons' was provided for this sabotage system,
                        // write that
                        if (payload.PlayersHoldingButtons.HasValue)
                        {
                            writer.WritePacked(payload.PlayersHoldingButtons.Value);
                        }

                        // If a Timer was provided for this sabotage system, write that
                        if (payload.TimerSystemState.HasValue)
                        {
                            writer.Write(payload.TimerSystemState.Value);
                        }

                        // If a count of active consoles in the system states was provided for this sabotage system, write that
                        if (payload.ActiveConsolesCountSystemState.HasValue)
                        {
                            writer.WritePacked(payload.ActiveConsolesCountSystemState.Value);

                        }

                        // If the state of the active consoles is provided, write it
                        if (payload.ActiveConsolesPayloadSystemState != null)
                        {
                            for (int i = 0; i < payload.ActiveConsolesPayloadSystemState.Count; i++)
                            {
                                writer.Write(payload.ActiveConsolesPayloadSystemState[i].Item1);
                                writer.Write(payload.ActiveConsolesPayloadSystemState[i].Item2);
                            }
                        }

                        // if a count of completed consoles in the completed system states was provided for this sabotage system, write that
                        if (payload.CompletedConsolesCountSystemState.HasValue)
                        {
                            writer.WritePacked(payload.CompletedConsolesCountSystemState.Value);
                        }

                        // If the state of the completed consoles is provided, write it
                        if (payload.CompletedConsolesPayloadSystemState != null)
                        {
                            for (int i = 0; i < payload.CompletedConsolesPayloadSystemState.Count; i++)
                            {
                                writer.Write(payload.CompletedConsolesPayloadSystemState[i]);
                            }
                        }

                        // Magic
                        //writer.Write(new byte[] { 0x04, 0x00, 0x11, 0x00, 0x00, 0xf0, 0x41 });

                        writer.EndMessage();
                        // End the messages and send them to everyone
                        // We end twice due to the two StartMessage calls at the top
                        writer.EndMessage();

                        writer.EndMessage();

                        await game.SendToAllAsync(writer);
                    }

                    // Return the new state of the game 
                    return new ExternalCommandResponse<Game>(game);
                }
                catch (Exception e)
                {
                    logger.Error($"Encountered exception whilst setting sabotage state: {e}");
                    return new ExternalCommandResponse<Game>("An exception was encountered whilst setting sabotage state", (int)ExternalCommandErrorCodes.UnexpectedException);
                }
            }
            else
            {
                return new ExternalCommandResponse<Game>("Cannot find the requested game.", (int)ExternalCommandErrorCodes.UnableToFindRequestedGame);
            }
        }
    }
}
