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
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        ConversationService conversationService,
        MessagePersistenceService messagePersistenceService,
        MessageDeliveryService messageDeliveryService,
        ActiveConnectionRegistry connectionRegistry,
        ILogger<ChatHub> logger)
    {
        _conversationService = conversationService;
        _messagePersistenceService = messagePersistenceService;
        _messageDeliveryService = messageDeliveryService;
        _connectionRegistry = connectionRegistry;
        _logger = logger;
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

    public async Task JoinConversation(int conversationId)
    {
        try
        {
            var user = AuthenticatedUserAccessor.FromPrincipal(Context.User);
            var connectionAborted = Context.ConnectionAborted;
            _logger.LogInformation(
                "Solicitud de ingreso al chat. ConversationId={ConversationId}, UserType={UserType}, UserId={UserId}",
                conversationId,
                user.UserType,
                user.UserId);

            await _conversationService.GetAuthorizedAsync(
                conversationId,
                user,
                connectionAborted);
            _logger.LogInformation(
                "Conversación autorizada. ConversationId={ConversationId}, UserType={UserType}, UserId={UserId}",
                conversationId,
                user.UserType,
                user.UserId);

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GetConversationGroup(conversationId),
                connectionAborted);

            _logger.LogInformation(
                "Conexión unida al grupo. ConversationId={ConversationId}, ConnectionId={ConnectionId}",
                conversationId,
                Context.ConnectionId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Error al ingresar a la conversación. ConversationId={ConversationId}, ConnectionId={ConnectionId}",
                conversationId,
                Context.ConnectionId);
            throw;
        }
    }

    public async Task<MessageResponse> SendMessage(
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = AuthenticatedUserAccessor.FromPrincipal(Context.User);
        _logger.LogInformation(
            "Mensaje recibido para difusión. ConversationId={ConversationId}, UserType={UserType}, UserId={UserId}",
            request.ConversationId,
            user.UserType,
            user.UserId);
        var result = await _messagePersistenceService.SendAsync(request, user, cancellationToken);
        var response = MessageResponseMapper.FromEntity(result.Message);

        if (result.Outcome == MessageWriteOutcome.Created)
        {
            await _messageDeliveryService.DeliverAsync(response);
            _logger.LogInformation(
                "Mensaje difundido. MessageId={MessageId}, ConversationId={ConversationId}",
                response.Id,
                response.ConversationId);
        }

        return response;
    }

    public async Task Typing(int conversationId, bool isTyping)
    {
        var user = AuthenticatedUserAccessor.FromPrincipal(Context.User);
        await _conversationService.GetAuthorizedAsync(
            conversationId,
            user,
            Context.ConnectionAborted);

        await Clients.OthersInGroup(GetConversationGroup(conversationId)).SendAsync(
            "TypingChanged",
            conversationId,
            user.UserType,
            isTyping,
            Context.ConnectionAborted);
    }
}
