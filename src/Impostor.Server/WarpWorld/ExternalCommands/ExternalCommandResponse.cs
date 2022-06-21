using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Server.ExternalCommands.API;

namespace Impostor.Server.ExternalCommands
{
    /// <summary>
    /// A default implementation of <see cref="IExternalCommandResponse{T}"/> which lets a user provide the ResponseData and appropriate error
    /// wherein the <see cref="T"/> type is a nullable class
    /// </summary>
    public class ExternalCommandResponse<T> : IExternalCommandResponse<T>
        where T : class
    {
        /// <summary>
        /// The response data for the External Command
        /// </summary>
        public T ResponseData { get; private set; }

        /// <summary>
        /// An optional error from the External Command
        /// </summary>
        public string Error { get; private set; }

        /// <summary>
        /// An optional error code from the External Command
        /// </summary>
        public int ErrorCode { get; private set; }

        /// <summary>
        /// Ctor for creating a <see cref="ExternalCommandResponse{T}"/> which takes in the response data of a successful command
        /// </summary>
        public ExternalCommandResponse(T responseData) : this(responseData, string.Empty, 0)
        {
        }

        /// <summary>
        /// Ctor for creating a <see cref="ExternalCommandResponse{T}"/> which takes in the error and error code of an unsuccessful command
        /// </summary>
        public ExternalCommandResponse(string error, int errorCode) : this(null, error, errorCode)
        {
        }

        /// <summary>
        /// Ctor for creating a <see cref="ExternalCommandResponse{T}"/> which takes in the response data and error info of an External Command
        /// </summary>
        public ExternalCommandResponse(T responseData, string error, int errorCode)
        {
            this.ResponseData = responseData;
            this.Error = error ?? throw new ArgumentNullException(nameof(error));
            this.ErrorCode = errorCode;
        }
    }
}
