using CrownBuilders.API.Contracts;
using CrownBuilders.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CrownBuilders.API.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly ConversationService _conversationService;
    private readonly MessagePersistenceService _messagePersistenceService;
    private readonly MessageDeliveryService _messageDeliveryService;
    private readonly ActiveConnectionRegistry _connectionRegistry;

    public ChatHub(
        ConversationService conversationService,
        MessagePersistenceService messagePersistenceService,
        MessageDeliveryService messageDeliveryService,
        ActiveConnectionRegistry connectionRegistry)
    {
        _conversationService = conversationService;
        _messagePersistenceService = messagePersistenceService;
        _messageDeliveryService = messageDeliveryService;
        _connectionRegistry = connectionRegistry;
    }

    public override async Task OnConnectedAsync()
    {
        var user = AuthenticatedUserAccessor.FromPrincipal(Context.User);
        _connectionRegistry.Register(user, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionRegistry.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public static string GetConversationGroup(int conversationId)
    {
        if (conversationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conversationId));
        }

        return $"conversation:{conversationId}";
    }

    public async Task JoinConversation(
        int conversationId,
        CancellationToken cancellationToken = default)
    {
        var user = AuthenticatedUserAccessor.FromPrincipal(Context.User);
        await _conversationService.GetAuthorizedAsync(conversationId, user, cancellationToken);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetConversationGroup(conversationId),
            cancellationToken);
    }

    public async Task<MessageResponse> SendMessage(
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = AuthenticatedUserAccessor.FromPrincipal(Context.User);
        var result = await _messagePersistenceService.SendAsync(request, user, cancellationToken);
        var response = MessageResponseMapper.FromEntity(result.Message);

        if (result.Outcome == MessageWriteOutcome.Created)
        {
            await _messageDeliveryService.DeliverAsync(response, cancellationToken);
        }

        return response;
    }
}
