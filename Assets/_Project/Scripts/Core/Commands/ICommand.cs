// Assets/_Project/Scripts/Core/Commands/ICommand.cs
namespace NonaRoyale.Core.Commands
{
    /// <summary>
    /// Something a player asks the game to do. The view never changes state
    /// directly — it sends one of these and reacts to the events that come back
    /// (ADR-0004).
    /// </summary>
    /// <remarks>
    /// Commands are plain serializable data on purpose. Whether one arrives from
    /// a local click or a network socket is the view's concern, so online
    /// multiplayer later is a transport swap rather than a re-architecture.
    /// </remarks>
    public interface ICommand
    {
    }
}