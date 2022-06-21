using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Serilog;

namespace Impostor.Server.ExternalCommands.UpdateGame
{
    /// <summary>
    /// An implementation of <see cref="IExternalCommand{T, TResult}"/> which updates the game options of a specified game
    /// Takes in an <see cref="UpdateGamePayload"/> and returns a wrapped <see cref="Game"/>
    /// </summary>
    internal class UpdateGameExternalCommand : IExternalCommand<UpdateGamePayload, Game>
    {
        private readonly GameManager gameManager;
        private readonly ILogger logger;

        /// <summary>
        /// Constructor for creating an <see cref="UpdateGameExternalCommand"/>
        /// </summary>
        internal UpdateGameExternalCommand(GameManager gameManager) : this(gameManager, Log.ForContext<UpdateGameExternalCommand>())
        {

        }
        /// <summary>
        /// Constructor for creating an <see cref="UpdateGameExternalCommand"/> which allows you to specify a logger
        /// </summary>
        internal UpdateGameExternalCommand(GameManager gameManager, ILogger logger)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Updates the Game Options of the specified game
        /// </summary>
        public virtual async Task<IExternalCommandResponse<Game>> PerformCommand(UpdateGamePayload payload)
        {
            Game game = payload.Game as Game ?? gameManager.Find(payload.Code);

            if (game != null)
            {
                try
                {
                    // Update the Game Options and trigger a sync
                    game.Options = payload.GameOptions;
                    await game.SyncSettingsAsync();

                    return new ExternalCommandResponse<Game>(game);
                }
                catch (Exception e)
                {
                    logger.Error($"Encountered exception whilst updating game: {e}");
                    return new ExternalCommandResponse<Game>("An exception was encountered when updating the game", (int)ExternalCommandErrorCodes.UnexpectedException);
                }
            }
            else
            {
                return new ExternalCommandResponse<Game>("Cannot find the requested game.", (int)ExternalCommandErrorCodes.UnableToFindRequestedGame);
            }
        }
    }
}
