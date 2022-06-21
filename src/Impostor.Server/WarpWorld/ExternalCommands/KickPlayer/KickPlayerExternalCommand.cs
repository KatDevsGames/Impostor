using System;
using System.Threading.Tasks;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Impostor.Server.ExternalCommands.API;
using Serilog;

namespace Impostor.Server.ExternalCommands.KickPlayer
{
    /// <summary>
    /// An implementation of <see cref="IExternalCommand{T, TResult}"/> for kicking a player from a specific game
    /// Takes in a <see cref="KickPlayerPayload"/> and returns a wrapped <see cref="Game"/>
    /// </summary>
    internal class KickPlayerExternalCommand : IExternalCommand<KickPlayerPayload, Game>
    {
        private readonly GameManager gameManager;
        private readonly ILogger logger;

        /// <summary>
        /// Constructor for creating a <see cref="KickPlayerExternalCommand"/>
        /// </summary>
        internal KickPlayerExternalCommand(GameManager gameManager) : this(gameManager, Log.ForContext<KickPlayerExternalCommand>())
        {
        }

        /// <summary>
        /// Constructor for creating a <see cref="CloseAllDoorsExternalCommand"/> allowing additional setting of a logger
        /// </summary>
        internal KickPlayerExternalCommand(GameManager gameManager, ILogger logger)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Kicks the selected player from the game, optionally banning them
        /// </summary>
        public async Task<IExternalCommandResponse<Game>> PerformCommand(KickPlayerPayload payload)
        {
            Game game = gameManager.Find(payload.Code);

            if (game != null)
            {
                try
                {
                    // Kick the player from the game
                    await game.HandleKickPlayer(payload.PlayerID, payload.IsBan);

                    // Return the new state of the game
                    return new ExternalCommandResponse<Game>(game);
                }
                catch (Exception e)
                {
                    logger.Error($"Encountered exception whilst kicking player from the game: {e}");
                    return new ExternalCommandResponse<Game>("An exception was encountered when kicking the player", (int)ExternalCommandErrorCodes.UnexpectedException);
                }
            }
            else
            {
                return new ExternalCommandResponse<Game>("Cannot find the requested game.", (int)ExternalCommandErrorCodes.UnableToFindRequestedGame);
            }
        }
    }
}
