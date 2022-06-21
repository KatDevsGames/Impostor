using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Net;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Impostor.Server.WarpWorld.CrowdControl;
using Serilog;

namespace Impostor.Server.ExternalCommands.RemoveGame
{
    /// <summary>
    /// An implementation of <see cref="IExternalCommand{T, TResult}"/> which removes a game and all the people in it
    /// Takes in a <see cref="RemoveGamePayload"/> and returns a wrapped <see cref="Game"/> object
    /// </summary>
    internal class RemoveGameExternalCommand : IExternalCommand<RemoveGamePayload, Game>
    {
        private readonly GameManager gameManager;
        private readonly ILogger logger;

        private readonly string endGameString;

        private const string EndGameStringSettingsKey = "EndGamePlayerMessage";
        private const string DefaultEndGameString = "The game has been ended by the host";

        /// <summary>
        /// Constructor for creating a <see cref="RemoveGameExternalCommand"/>
        /// </summary>
        internal RemoveGameExternalCommand(GameManager gameManager) : this(gameManager, Log.ForContext<RemoveGameExternalCommand>())
        { 
        }

        /// <summary>
        /// Constructor for creating a <see cref="RemoveGameExternalCommand"/> allowing additional setting of a logger
        /// </summary>
        internal RemoveGameExternalCommand(GameManager gameManager, ILogger logger)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            //endGameString = webApiSettings.GetSettingOrDefault(EndGameStringSettingsKey, DefaultEndGameString);
        }

        /// <summary>
        /// Removes a game from the game manager and kicks everyone from it
        /// </summary>
        public async Task<IExternalCommandResponse<Game>> PerformCommand(RemoveGamePayload payload)
        {
            Game game = gameManager.Find(payload.Code);

            if (game != null)
            {
                try
                {
                    // Prevent more people joining
                    game.FlagGameAsDestroyed();

                    // Get all the Player IDs
                    IReadOnlyList<IClientPlayer> clientPlayers = game.Players.ToList();
                    for (int i = 0; i < clientPlayers.Count; i++)
                    {
                        // Remove each player, once the last one is gone
                        // the server will close itself down
                        ValueTask? discTask = clientPlayers[i]?.Client?.DisconnectAsync(Api.Innersloth.DisconnectReason.Custom, endGameString);
                        if (discTask.HasValue)
                        {
                            await discTask.Value;
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error($"Encountered exception whilst removing game: {e.Message}");
                    return new ExternalCommandResponse<Game>($"Encountered exception whilst removing game", (int)ExternalCommandErrorCodes.UnexpectedException);
                }
                finally
                {
                    // In case it hasn't closed yet, do it ourselves
                    await gameManager.RemoveAsync(game.Code);
                }

                // Return it
                return new ExternalCommandResponse<Game>(game);
            }
            else
            {
                return new ExternalCommandResponse<Game>("Cannot find the requested game", (int)ExternalCommandErrorCodes.UnableToFindRequestedGame);
            }
        }
    }
}
