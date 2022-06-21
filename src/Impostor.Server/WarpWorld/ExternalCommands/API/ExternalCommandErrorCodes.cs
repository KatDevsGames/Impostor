using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Server.ExternalCommands.API
{
    /// <summary>
    /// An enum representing a non-exhaustive list of possible error codes 
    /// returned by an <see cref="IExternalCommand{T, TResult}"/>
    /// </summary>
    internal enum ExternalCommandErrorCodes
    {
        Success = 0,
        UnableToParsePayload = 1,
        UnableToFindRequestedGame = 2,
        UnexpectedException = 3,
    }
}
