using System.Collections.Generic;
using System.Linq;
using Impostor.Api.Innersloth;
using Impostor.Server.Net.Inner.Objects.Systems;
using Impostor.Server.Net.Inner.Objects.Systems.ShipStatus;

namespace Impostor.Server.Net.Inner.Objects.ShipStatus
{

    /// <summary>
    /// This file contains some partial classes to help manage the current ship status
    /// </summary>
    internal partial class InnerShipStatus
    {
        internal void SetSystems(Dictionary<SystemTypes, ISystemType> systems)
        {
            _systems.Clear();
            foreach (var kvp in systems)
            {
                _systems.Add(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Attempt to get the <see cref="IActivatable"/> representing the specified Sabotage System
        /// Supported types are <see cref="SystemTypes.Comms"/>, <see cref="SystemTypes.Reactor"/>, <see cref="SystemTypes.LifeSupp"/>
        /// and <see cref="SystemTypes.Electrical"/>
        /// </summary>
        /// <param name="systemType">The <see cref="SystemTypes"/> we want to get the activatable for</param>
        /// <param name="activatable">The <see cref="IActivatable"/> of the system if found, <see langword="null"/> if not</param>
        /// <returns><c>true</c> if one was found, <c>false</c> if one wasn't</returns>
        public bool TryGetSabotageSystem(SystemTypes systemType, out IActivatable activatable)
        {
            // TODO - We need to test this with Reactor in Polus, our notes say this uses Laboratory instead of Reactor
            //        But the code doesn't seem to actually reflect this??

            activatable = null;
            if (systemType != SystemTypes.Comms &&
                systemType != SystemTypes.Reactor &&
                systemType != SystemTypes.LifeSupp &&
                systemType != SystemTypes.Electrical &&
                systemType != SystemTypes.Laboratory)
            {
                // Not a supported Sabotage Type
                return false;
            }

            if (!_systems.ContainsKey(SystemTypes.Sabotage))
            {
                // No Sabotage data available
                return false;
            }

            SabotageSystemType? sabotageSystems = _systems[SystemTypes.Sabotage] as SabotageSystemType;
            if (sabotageSystems == null)
            {
                // Data is not set up correctly
                return false;
            }

            // Get the correct system out of the systems
            IActivatable foundActivatable = null;
            ISystemType foundSystemType = null;
            if (_systems.ContainsKey(systemType))
            {
                foundSystemType = _systems[systemType];

                switch (systemType)
                {
                    case SystemTypes.Comms:
                        foundActivatable = foundSystemType as HudOverrideSystemType;
                        break;
                    case SystemTypes.Laboratory:
                    case SystemTypes.Reactor:
                        // Reactor will either be ReactorSystemType or HeliSabotageSystemType depending on map
                        foundActivatable = foundSystemType as ReactorSystemType;
                        if (foundActivatable == null)
                        {
                            foundActivatable = foundSystemType as HeliSabotageSystemType;
                        }
                        break;
                    case SystemTypes.LifeSupp:
                        foundActivatable = foundSystemType as LifeSuppSystemType;
                        break;
                    case SystemTypes.Electrical:
                        foundActivatable = foundSystemType as SwitchSystem;
                        break;
                    default:
                        // Unsupported type
                        return false;
                }
            }

            // If we did get an activatable, then success
            if (foundActivatable != null)
            {
                activatable = foundActivatable;
                return true;
            }
            return false;
        }
    }
}

namespace Impostor.Server.Net.Inner.Objects.Systems.ShipStatus
{
    public partial class SabotageSystemType
    {
        public IReadOnlyList<IActivatable> Specials
        {
            get
            {
                return _specials.ToList().AsReadOnly();
            }
        }
    }
}
