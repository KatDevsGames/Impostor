using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Innersloth;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Serilog;

namespace Impostor.Server.ExternalCommands.GetGameOptions
{
    /// <summary>
    /// An implementation of <see cref="IExternalCommand{T, TResult}"/> which gets the current <see cref="GameOptionsData"/> of the selected game
    /// Takes in a <see cref="GetGameOptionsPayload"/> and returns a wrapped <see cref="GameOptionsData"/>
    /// </summary>
    internal class GetGameOptionsExternalCommand : IExternalCommand<GetGameOptionsPayload, GameOptionsData>
    {
        private readonly GameManager gameManager;
        private readonly ILogger logger;

        /// <summary>
        /// Constructor for creating a <see cref="GetGameOptionsExternalCommand"/>
        /// </summary>
        internal GetGameOptionsExternalCommand(GameManager gameManager) : this(gameManager, Log.ForContext<GetGameOptionsExternalCommand>())
        {
        }

        /// <summary>
        /// Constructor for creating a <see cref="GetGameOptionsExternalCommand"/> allowing additional setting of a logger
        /// </summary>
        internal GetGameOptionsExternalCommand(GameManager gameManager, ILogger logger)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the <see cref="GameOptionsData"/> of the specified game
        /// </summary>
        public Task<IExternalCommandResponse<GameOptionsData>> PerformCommand(GetGameOptionsPayload payload)
        {
            Game game = gameManager.Find(payload.Code);

            if (game != null)
            {
                try
                {
                    // Get the options from the found game
                    GameOptionsData gameOptions = game.Options;

                    // Return in the response
                    var response = new ExternalCommandResponse<GameOptionsData>(gameOptions);
                    return Task.FromResult<IExternalCommandResponse<GameOptionsData>>(response);
                }
                catch (Exception e)
                {
                    logger.Error($"Encountered exception whilst closing all doors: {e}");
                    var response = new ExternalCommandResponse<GameOptionsData>("An exception was encountered whilst getting game options", (int)ExternalCommandErrorCodes.UnexpectedException);
                    return Task.FromResult<IExternalCommandResponse<GameOptionsData>>(response);
                }
            }
            else
            {
                var response = new ExternalCommandResponse<GameOptionsData>("Cannot find the requested game.", (int)ExternalCommandErrorCodes.UnableToFindRequestedGame);
                return Task.FromResult<IExternalCommandResponse<GameOptionsData>>(response);
            }
        }
    }
}
