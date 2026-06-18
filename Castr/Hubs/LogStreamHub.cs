using Microsoft.AspNetCore.SignalR;

namespace Castr.Hubs;

/// <summary>
/// SignalR hub used to stream live log lines to the dashboard Logs panel.
/// The server pushes entries via <c>IHubContext&lt;LogStreamHub&gt;</c> on the
/// "LogEntry" event (see <c>LogBufferService</c>).
/// </summary>
public class LogStreamHub : Hub
{
}
