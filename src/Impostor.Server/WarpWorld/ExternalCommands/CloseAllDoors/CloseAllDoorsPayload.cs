using System.Collections.Generic;
using Impostor.Api.Innersloth;
using Newtonsoft.Json;
using Serilog;

namespace Impostor.Server.ExternalCommands.CloseAllDoors
{
    /// <summary>
    /// An aggregate class containing the info needed to close all the doors in a game
    /// </summary>
    public class CloseAllDoorsPayload
    {
        public int Code { get; set; }

        public string CodeStr { get { return GameCodeParser.IntToGameName(Code); } }

        public static CloseAllDoorsPayload DeserializeFromJson(string json, ILogger logger)
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
            CloseAllDoorsPayload closePayload = JsonConvert.DeserializeObject<CloseAllDoorsPayload>(json, deserializeSettings);

            // Report any errors
            if (deserializerErrors.Count > 0)
            {
                var errorText = $"Encountered the following error when trying to Deserialise {typeof(CloseAllDoorsPayload).Name}\n";
                for (int i = 0; i < deserializerErrors.Count; i++)
                {
                    errorText += $"{deserializerErrors[i]}\n";
                }
                logger.Error(errorText);
            }

            return closePayload;
        }
    }
}
