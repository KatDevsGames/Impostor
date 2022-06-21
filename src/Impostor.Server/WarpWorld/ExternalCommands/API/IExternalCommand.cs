using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Server.ExternalCommands.API
{
    /// <summary>
    /// An interface representing a command triggered by some kind of external process
    /// </summary>
    /// <typeparam name="T">The payload type that the command requires</typeparam>
    /// <typeparam name="TResult">The return type that the command requires</typeparam>
    internal interface IExternalCommand<T, TResult> : IExternalCommand
         where T : class
         where TResult : class
    {
        Task<IExternalCommandResponse<TResult>> PerformCommand(T payload);
    }

    /// <summary>
    /// Non-generic interface used to help with generics. You should use <see cref="IExternalCommand{T, TResult}"/>
    /// for all implementations
    /// </summary>
    internal interface IExternalCommand
    {

    }
}
