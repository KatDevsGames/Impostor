using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Innersloth;
using Newtonsoft.Json;
using Serilog;

namespace Impostor.Server.ExternalCommands.SetSabotage
{
    /// <summary>
    /// An aggregate class containing the info for a set sabotage request
    /// </summary>
    public class SetSabotagePayload
    {
        /// <summary>
        /// The code for the game
        /// </summary>
        public int Code { get; set; }

        public string CodeStr { get { return GameCodeParser.IntToGameName(Code); } }

        /// <summary>
        /// The sabotage type this is referring to
        /// </summary>
        public SystemTypes SystemType { get; set; }

        /// <summary>
        /// One of the possible optional parameters for the sabotage states
        /// Defaults to null if not used.
        /// </summary>
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool? BooleanSystemState { get; set; }

        /// <summary>
        /// One of the possible optional parameters for the sabotage states
        /// Defaults to null if not used.
        /// </summary>
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float? CountdownSystemState { get; set; }

        /// <summary>
        /// One of the possible optional parameters for the sabotage states
        /// Defaults to null if not used.
        /// </summary>
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int? PlayersHoldingButtons { get; set; }

        /// <summary>
        ///  One of the possible optional parameters for the sabotage states
        ///  Defaults to null if not used
        /// </summary>
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float? TimerSystemState { get; set; }

        /// <summary>
        ///  One of the possible optional parameters for the sabotage states
        ///  Defaults to null if not used
        /// </summary>
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int? ActiveConsolesCountSystemState { get; set; }

        /// <summary>
        ///  One of the possible optional parameters for the sabotage states
        ///  Defaults to null if not used
        /// </summary>
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<Tuple<byte, byte>>? ActiveConsolesPayloadSystemState { get; set; }

        /// <summary>
        ///  One of the possible optional parameters for the sabotage states
        ///  Defaults to null if not used
        /// </summary>
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int? CompletedConsolesCountSystemState { get; set; }

        /// <summary>
        ///  One of the possible optional parameters for the sabotage states
        ///  Defaults to null if not used
        /// </summary>
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public List<byte>? CompletedConsolesPayloadSystemState { get; set; }


        public static SetSabotagePayload DeserializeFromJson(string json, ILogger logger)
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
            SetSabotagePayload sabotagePayload = JsonConvert.DeserializeObject<SetSabotagePayload>(json, deserializeSettings);

            // Report any errors
            if (deserializerErrors.Count > 0)
            {
                var errorText = $"Encountered the following error when trying to Deserialise {typeof(SetSabotagePayload).Name}\n";
                for (int i = 0; i < deserializerErrors.Count; i++)
                {
                    errorText += $"{deserializerErrors[i]}\n";
                }
                logger.Error(errorText);
            }

            return sabotagePayload;
        }
    }
}
