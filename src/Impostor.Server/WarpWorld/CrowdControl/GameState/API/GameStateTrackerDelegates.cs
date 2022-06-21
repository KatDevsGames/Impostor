using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;

namespace Impostor.Server.WarpWorld.CrowdControl.GameState.API
{
    public delegate void GameCreatedDelegate(IGameCreatedEvent e);
    public delegate void GameDestroyedDelegate(IGameDestroyedEvent e);
    public delegate void MeetingStartedDelegate(IMeetingStartedEvent e);
    public delegate void MeetingEndedDelegate(IMeetingEndedEvent e);
    public delegate void GameStartingDelegate(IGameStartingEvent e);
    public delegate void GameEnteredLobbyDelegate(IGameEvent e);
    public delegate void GameExitedLobbyDelegate(IGameEvent e);
}
