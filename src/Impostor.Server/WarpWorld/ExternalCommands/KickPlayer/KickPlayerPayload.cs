using System.Collections.Generic;
using Impostor.Api.Innersloth;
using Newtonsoft.Json;
using Serilog;

namespace Impostor.Server.ExternalCommands.KickPlayer
{
    /// <summary>
    /// An aggregate class representing the payload used for the <see cref="KickPlayerRequestHandler"/>
    /// </summary>
    public class KickPlayerPayload
    {
        public int Code { get; set; }
        public string CodeStr { get { return GameCodeParser.IntToGameName(Code); } }
        public int PlayerID { get; set; }
        public bool IsBan { get; set; }

        public static KickPlayerPayload DeserializeFromJson(string json, ILogger logger)
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
            KickPlayerPayload kickPayload = JsonConvert.DeserializeObject<KickPlayerPayload>(json, deserializeSettings);

            // Report any errors
            if (deserializerErrors.Count > 0)
            {
                var errorText = $"Encountered the following error when trying to Deserialise {typeof(KickPlayerPayload).Name}\n";
                for (int i = 0; i < deserializerErrors.Count; i++)
                {
                    errorText += $"{deserializerErrors[i]}\n";
                }
                logger.Error(errorText);
            }

            return kickPayload;
        }
    }
}
