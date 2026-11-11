using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServiceDefaults.Auth;

namespace NotificationService.Hubs;

/// <summary>
/// Real-time hub. Connections are authenticated with the JWT and placed in groups,
/// so the server decides who receives which event:
///   - "it-support"   : every IT Support connection (all incidents + monitoring)
///   - "user:{id}"    : one employee's connections (only their own incidents)
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string ItSupportGroup = "it-support";
    public static string UserGroup(string userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var group = Context.User!.IsItSupport()
            ? ItSupportGroup
            : UserGroup(Context.User!.GetUserId());

        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        await base.OnConnectedAsync();
    }
}
