using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Serilog;

namespace Impostor.Server.ExternalCommands.NewGame
{
    /// <summary>
    /// An implementation of <see cref="IExternalCommand{T, TResult}"/> for creating a new game
    /// Takes in a <see cref="GameOptionsData"/> and returns a wrapped <see cref="Game"/>
    /// </summary>
    internal class NewGameExternalCommand : IExternalCommand<GameOptionsData, Game>
    {
        private readonly GameManager gameManager;
        private readonly ILogger logger;

        /// <summary>
        /// Constructor for creating a <see cref="NewGameExternalCommand"/>
        /// </summary>
        internal NewGameExternalCommand(GameManager gameManager) :
            this(gameManager, Log.ForContext<NewGameExternalCommand>())
        {
        }

        /// <summary>
        /// Constructor for creating a <see cref="NewGameExternalCommand"/> allowing additional setting of a logger
        /// </summary>
        internal NewGameExternalCommand(GameManager gameManager, ILogger logger)
        {
            this.gameManager = gameManager;
            this.logger = logger;
        }

        public async Task<IExternalCommandResponse<Game>> PerformCommand(GameOptionsData payload)
        {
            // Create the Game
            try
            {
                Game game = (await gameManager.CreateAsync(payload)) as Game;
                if (game != null)
                {
                    // Return the Response with the Game object
                    return new ExternalCommandResponse<Game>(game);
                }
                else
                {
                    return new ExternalCommandResponse<Game>($"Unable to create requested Game", (int)ExternalCommandErrorCodes.UnableToFindRequestedGame);
                }
            }
            catch (Exception e)
            {
                return new ExternalCommandResponse<Game>($"Encountered exception when attempting to create the game", (int)ExternalCommandErrorCodes.UnexpectedException);
            }
        }
    }
}
