using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Server.ExternalCommands.API
{
    /// <summary>
    /// An interface representing a possible response from an <see cref="IExternalCommand{T, TResult}"/>
    /// </summary>
    internal interface IExternalCommandResponse<T>
    {
        T ResponseData { get; }
        string Error { get; }
        int ErrorCode { get; }
    }
}
