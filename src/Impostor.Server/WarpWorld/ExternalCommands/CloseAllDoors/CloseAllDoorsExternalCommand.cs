using System;
using System.Threading.Tasks;
using Impostor.Api.Innersloth;
using Impostor.Api.Net.Messages;
using Impostor.Hazel;
using Impostor.Server.Net.Inner;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Impostor.Server.ExternalCommands.API;
using Serilog;

namespace Impostor.Server.ExternalCommands.CloseAllDoors
{
    /// <summary>
    /// An implementation of <see cref="IExternalCommand{T, TResult}"/> for closing all the doors in a provided game.
    /// Takes in a <see cref="CloseAllDoorsPayload"/> and returns a wrapped <see cref="Game"/>
    /// </summary>
    internal class CloseAllDoorsExternalCommand : IExternalCommand<CloseAllDoorsPayload, Game>
    {
        private readonly GameManager gameManager;
        private readonly ILogger logger;

        /// <summary>
        /// Constructor for creating a <see cref="CloseAllDoorsExternalCommand"/>
        /// </summary>
        internal CloseAllDoorsExternalCommand(GameManager gameManager) : this(gameManager, Log.ForContext<CloseAllDoorsExternalCommand>())
        {
        }

        /// <summary>
        /// Constructor for creating a <see cref="CloseAllDoorsExternalCommand"/> allowing additional setting of a logger
        /// </summary>
        internal CloseAllDoorsExternalCommand(GameManager gameManager, ILogger logger)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Closes all the doors in the provided game
        /// </summary>
        public virtual async Task<IExternalCommandResponse<Game>> PerformCommand(CloseAllDoorsPayload payload)
        {
            Game game = gameManager.Find(payload.Code);

            if (game != null)
            {
                try
                {
                    using (var writer = MessageWriter.Get(Api.Net.Messages.MessageType.Reliable))
                    {
                        // Open a nested message of GameData and DataFlag with the game code and Net Id
                        writer.StartMessage(MessageFlags.GameData);
                        writer.Write(game.Code);

                        writer.StartMessage(GameDataTag.DataFlag);
                        writer.WritePacked(game.GameNet.ShipStatus.NetId);

                        writer.StartMessage((byte)SystemTypes.Doors);

                        // Identify how many doors we have
                        int numberOfDoors;
                        switch (game.Options.Map)
                        {
                            case MapTypes.Skeld:
                                numberOfDoors = 13;
                                break;
                            case MapTypes.MiraHQ:
                                numberOfDoors = 2;
                                break;
                            case MapTypes.Polus:
                                numberOfDoors = 12;
                                break;
                            case MapTypes.Airship:
                                numberOfDoors = 21;
                                break;
                            default:
                                throw new NotImplementedException($"Map ID of {game.Options.Map} is not supported by this command");
                        }

                        // Use a filter that we generate to affect all doors indiscriminately
                        int filter = (int)Math.Pow(2, numberOfDoors);
                        writer.WritePacked(filter - 1);

                        // Set the state of all doors to 'false' which is closed
                        for (int i = 0; i < numberOfDoors; i++)
                        {
                            writer.Write(false);
                        }

                        // Close the messages and send
                        writer.EndMessage();
                        
                        writer.EndMessage();

                        writer.EndMessage();

                        await game.SendToAllAsync(writer);
                    }

                    // Return the new state of the game 
                    return new ExternalCommandResponse<Game>(game);
                }
                catch (Exception e)
                {
                    logger.Error($"Encountered exception whilst closing all doors: {e}");
                    return new ExternalCommandResponse<Game>("An exception was encountered whilst closing all doors", (int)ExternalCommandErrorCodes.UnexpectedException);
                }
            }
            else
            {
                return new ExternalCommandResponse<Game>("Cannot find the requested game.", (int)ExternalCommandErrorCodes.UnableToFindRequestedGame);
            }
        }
    }
}
