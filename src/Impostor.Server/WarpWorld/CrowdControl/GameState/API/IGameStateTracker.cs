using Impostor.Api.Innersloth;

namespace Impostor.Server.WarpWorld.CrowdControl.GameState.API
{
    /// <summary>
    /// An interface representing an object able to keep track of the game's state, allowing other objects to query it
    /// </summary>
    public interface IGameStateTracker
    {
        event GameCreatedDelegate OnGameCreatedEvent;
        event GameDestroyedDelegate OnGameDestroyedEvent;
        event MeetingStartedDelegate OnMeetingStartedEvent;
        event MeetingEndedDelegate OnMeetingEndedEvent;

        /// <summary>
        /// Queries the <see cref="GameOptionsData"/> for the provided game when the round started
        /// </summary>
        /// <param name="gameCode">The integer game code to query</param>
        /// <param name="data">The <see cref="GameOptionsData"/> for that game when it started, <see langword="null"/> if it couldnt be found</param>
        /// <returns><see langword="true"/> if the game could be found, and a <see cref="GameOptionsData"/> was provided, <see langword="false"/> if not</returns>
        bool TryGetStartingGameOptionsData(int gameCode, out GameOptionsData? data);

        /// <summary>
        /// Queries the current state of the game, represented via <see cref="CrowdControlGameState"/>
        /// </summary>
        /// <param name="gameCode">The integer game code to query</param>
        /// <param name="state">The <see cref="CrowdControlGameState"/> for that game currently, <see langword="null"/> if it couldnt be found</param>
        /// <returns><see langword="true"/> if the game could be found, and a <see cref="CrowdControlGameState"/> was provided, <see langword="false"/> if not</returns>
        bool TryGetGameState(int gameCode, out CrowdControlGameState? state);

        /// <summary>
        /// Checks if we have any Starting <see cref="GameOptionsData"/> for the provided game code
        /// </summary>
        bool HasStartingGameOptionsData(int gameCode);

        /// <summary>
        /// Checks if we have any <see cref="CrowdControlGameState"/> information for the provided game code
        /// </summary>
        bool HasGameState(int gamecode);
    }
}
