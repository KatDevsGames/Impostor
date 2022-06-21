using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Server.ExternalCommands.API;

namespace Impostor.Server.WarpWorld.ExternalCommands.API
{
    /// <summary>
    /// An interface representing a factory which knows how to make External Commands
    /// </summary>
    internal interface IExternalCommandFactory
    {
        T GetExternalCommand<T>() where T : IExternalCommand;
    }
}
