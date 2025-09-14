using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MonitoringApplication.Hubs
{
    /// <summary>
    /// This hub is the real-time communication endpoint.
    /// The server uses this hub to push live status updates to connected clients.
    /// </summary>
    [Authorize] // بهبود: هاب امن شد تا فقط کاربران احرازهویت شده متصل شوند
    public class StatusHub : Hub
    {
        // No client-callable methods are needed for this implementation,
        // as the communication is one-way from server to client.
    }
}

