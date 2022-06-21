using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Server.ExternalCommands.API;
using Impostor.Server.ExternalCommands.CloseAllDoors;
using Impostor.Server.ExternalCommands.GetGameOptions;
using Impostor.Server.ExternalCommands.KickPlayer;
using Impostor.Server.ExternalCommands.NewGame;
using Impostor.Server.ExternalCommands.RemoveGame;
using Impostor.Server.ExternalCommands.SetSabotage;
using Impostor.Server.ExternalCommands.UpdateGame;
using Impostor.Server.Net.Manager;
using Impostor.Server.WarpWorld.ExternalCommands.API;

namespace Impostor.Server.WarpWorld.ExternalCommands
{
    /// <summary>
    /// An implementation of <see cref="IExternalCommandFactory"/> which knows how to make
    /// <see cref="IExternalCommand{T, TResult}"/> implementations
    /// </summary>
    internal class ExternalCommandFactory : IExternalCommandFactory
    {
        private readonly GameManager gameManager;
        private readonly ConcurrentDictionary<Type, IExternalCommand> externalCommandCache;

        /// <summary>
        /// Constructor for creating an <see cref="ExternalCommandFactory"/>
        /// </summary>
        public ExternalCommandFactory(GameManager gameManager)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            externalCommandCache = new ConcurrentDictionary<Type, IExternalCommand>();
        }

        /// <summary>
        /// Gets either a cached <see cref="IExternalCommand{T, TResult}"/> or creates a new one
        /// </summary>
        /// <remarks>Throws a <see cref="NotImplementedException"/> if the type is not something it knows about</remarks>
        public T GetExternalCommand<T>() where T : IExternalCommand
        {
            IExternalCommand externalCommand = null;
            if (externalCommandCache.ContainsKey(typeof(T)))
            {
                externalCommand = externalCommandCache[typeof(T)];
            }
            else
            {
                externalCommand = CreateAndCacheExternalCommand(typeof(T));
            }
            return (T)Convert.ChangeType(externalCommand, typeof(T));
        }

        /// <summary>
        /// Creates the appropriate <see cref="IExternalCommand{T, TResult}"/> implementation, adds it to the cache
        /// and returns it
        /// </summary>
        private IExternalCommand CreateAndCacheExternalCommand(Type t)
        {
            IExternalCommand externalCommand = null;

            // Depending on the type, make the External Command
            if (t == typeof(UpdateGameExternalCommand))
            {
                externalCommand = new UpdateGameExternalCommand(gameManager);
            }
            else if (t == typeof(SetSabotageExternalCommand))
            {
                externalCommand = new SetSabotageExternalCommand(gameManager);
            }
            else if (t == typeof(RemoveGameExternalCommand))
            {
                externalCommand = new RemoveGameExternalCommand(gameManager);
            }
            else if (t == typeof(NewGameExternalCommand))
            {
                externalCommand = new NewGameExternalCommand(gameManager);
            }
            else if (t == typeof(KickPlayerExternalCommand))
            {
                externalCommand = new KickPlayerExternalCommand(gameManager);
            }
            else if (t == typeof(GetGameOptionsExternalCommand))
            {
                externalCommand = new GetGameOptionsExternalCommand(gameManager);
            }
            else if (t == typeof(CloseAllDoorsExternalCommand))
            {
                externalCommand = new CloseAllDoorsExternalCommand(gameManager);
            }

            // If no command was made, we don't know how to make that, be upset
            if (externalCommand == null)
            {
                throw new NotImplementedException($"Unable to create a {t.Name}");
            }

            // Add it to the cache
            externalCommandCache.TryAdd(t, externalCommand);

            return externalCommand;
        }
    }
}
