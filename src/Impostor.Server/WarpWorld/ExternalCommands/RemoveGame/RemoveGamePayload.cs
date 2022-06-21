using Impostor.Api.Innersloth;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Server.ExternalCommands.RemoveGame
{
    /// <summary>
    /// An aggregate class containing the payload for a Remove Request
    /// </summary>
    public class RemoveGamePayload
    {
        public int Code { get; set; }
        public string CodeStr { get { return GameCodeParser.IntToGameName(Code); } }

        public static RemoveGamePayload DeserializeFromJson(string json, ILogger logger)
        {
            // Deserialize the Game Data
            List<string> deserializerErrors = new List<string>();
            var deserializeSettings = new JsonSerializerSettings()
            {
                Error = ((o, e) =>
                {
                    deserializerErrors.Add(e.ErrorContext.Error.Message);
                    e.ErrorContext.Handled = true;
                }),
            };
            RemoveGamePayload removePayload = JsonConvert.DeserializeObject<RemoveGamePayload>(json, deserializeSettings);

            // Report any errors
            if (deserializerErrors.Count > 0)
            {
                var errorText = $"Encountered the following error when trying to Deserialise {typeof(RemoveGamePayload).Name}\n";
                for (int i = 0; i < deserializerErrors.Count; i++)
                {
                    errorText += $"{deserializerErrors[i]}\n";
                }
                logger.Error(errorText);
            }

            return removePayload;
        }
    }
}
