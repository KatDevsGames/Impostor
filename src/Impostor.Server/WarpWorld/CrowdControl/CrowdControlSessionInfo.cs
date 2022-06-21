using System;
using System.Collections.Concurrent;

namespace Impostor.Server.WarpWorld.CrowdControl
{
    /// <summary>
    /// An aggregate class of objects related to the Crowd Control Session
    /// </summary>
    public class CrowdControlSessionInfo
    {
        public CrowdControlClientInfo HostClientInfo { get; set; }

        /// <summary>
        /// A collection of attached twitch users, maps the twitch user name to their twitch ID
        /// </summary>
        public ConcurrentDictionary<string, string> AttachedUsersTwitchNameIDLookup { get; set; }

        public CrowdControlSessionInfo(CrowdControlClientInfo hostClient)
        {
            HostClientInfo = hostClient;
            AttachedUsersTwitchNameIDLookup = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
