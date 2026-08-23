using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace iptv.Services._IptvNotifier;

[AllowAnonymous]
public class IptvHub : Hub
{
    public const string Route = "/hubs/iptv";

    private const string ChannelGroupPrefix = "channel-";

    public async Task SubscribeToChannel(string channelId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, ChannelGroupPrefix + channelId);

    public async Task UnsubscribeFromChannel(string channelId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, ChannelGroupPrefix + channelId);

    public async Task SubscribeToProvider(string providerPublicKey)
        => await Groups.AddToGroupAsync(Context.ConnectionId, providerPublicKey);
}
