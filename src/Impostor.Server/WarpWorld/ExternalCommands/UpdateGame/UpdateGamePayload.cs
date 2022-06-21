using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.Net.State;
using Newtonsoft.Json;
using Serilog;

namespace Impostor.Server.ExternalCommands.UpdateGame
{
    /// <summary>
    /// An aggregate class representing the payload used for the <see cref="UpdateGameRequestHandler"/>
    /// </summary>
    internal class UpdateGamePayload
    {
        public int Code { get; set; }
        public string CodeStr { get { return GameCodeParser.IntToGameName(Code); } }
        public GameOptionsData GameOptions { get; set; }
        public IGame? Game { get; set; }

        public static UpdateGamePayload DeserializeFromJson(string json, ILogger logger)
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
            UpdateGamePayload updatePayload = JsonConvert.DeserializeObject<UpdateGamePayload>(json, deserializeSettings);

            // Report any errors
            if (deserializerErrors.Count > 0)
            {
                var errorText = $"Encountered the following error when trying to Deserialise {typeof(UpdateGamePayload).Name}\n";
                for (int i = 0; i < deserializerErrors.Count; i++)
                {
                    errorText += $"{deserializerErrors[i]}\n";
                }
                logger.Error(errorText);
            }

            return updatePayload;
        }
    }
}
