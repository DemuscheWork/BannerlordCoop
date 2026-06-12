using Common;
using Common.Logging;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Utils;

/// <summary>
/// Marshals game-state or UI work from network-thread message handlers onto the game's main thread.
/// Network messages are delivered on a background poller thread and <see cref="Common.Messaging.MessageBroker"/>
/// invokes subscribers synchronously on that thread, so handlers that touch game state or UI from
/// there race the main game loop (see <c>ScreenManager</c>'s unsynchronized layer lists).
/// </summary>
/// <remarks>
/// The deferred action runs behind a <see cref="Campaign.Current"/> guard and a try/catch:
/// the campaign can unload between queueing and execution, and
/// <see cref="GameLoopRunner.Update"/> invokes queued actions unguarded, so a throw would
/// escape into the game's main tick and crash it. Anything else the work depends on must be
/// re-validated inside <paramref name="action"/> rather than at message arrival.
/// Do NOT use this as a broker-level subscription wrapper: <c>MessageBroker.Subscribe</c> holds
/// subscribers weakly via <c>WeakDelegate</c>, so a wrapper closure referenced only by the broker
/// is collected and the handler silently stops firing. Call this from inside an instance handler
/// method instead.
/// </remarks>
public class GameThreadDispatcher
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameThreadDispatcher>();

    private GameThreadDispatcher() { }

    /// <summary>
    /// Queues <paramref name="action"/> onto the game's main thread, guarded against a missing
    /// campaign and escaping exceptions.
    /// </summary>
    /// <param name="workName">Name of the work (e.g. the handled message type), used in log output.</param>
    /// <param name="action">The game-state or UI work to run on the main thread.</param>
    /// <param name="blocking">
    /// True blocks the calling thread until the work completed — use it when a network reply may
    /// only be sent after the game work is done. False queues and returns.
    /// </param>
    /// <returns>
    /// For blocking calls, true when the action ran to completion (campaign loaded, no exception).
    /// For non-blocking calls, always true; the action's outcome is not yet known.
    /// </returns>
    public static bool RunOnGameThread(string workName, Action action, bool blocking = false)
    {
        var completed = false;

        GameLoopRunner.RunOnMainThread(
            () => completed = ExecuteGuarded(workName, action, IsCampaignLoaded),
            blocking);

        return !blocking || completed;
    }

    private static bool IsCampaignLoaded() => Campaign.Current != null;

    /// <summary>
    /// Runs <paramref name="action"/> behind the campaign guard and exception barrier.
    /// Factored out of <see cref="RunOnGameThread"/> so the guard behavior is unit-testable
    /// without a live campaign or a pumping game loop.
    /// </summary>
    /// <returns>True when the action ran to completion.</returns>
    internal static bool ExecuteGuarded(string workName, Action action, Func<bool> isCampaignLoaded)
    {
        if (isCampaignLoaded() == false)
        {
            Logger.Warning("Skipping {Work}: the campaign is no longer loaded", workName);
            return false;
        }

        try
        {
            action();
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to run {Work} on the game thread", workName);
            return false;
        }
    }
}
