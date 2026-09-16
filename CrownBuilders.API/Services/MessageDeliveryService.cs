using CrownBuilders.API.Contracts;
using CrownBuilders.API.Data;
using CrownBuilders.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrownBuilders.API.Services;

public sealed class MessageDeliveryService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ActiveConnectionRegistry _connectionRegistry;
    private readonly ApplicationDbContext _context;
    private readonly FirebaseNotificationService _firebaseService;
    private readonly ILogger<MessageDeliveryService> _logger;

    public MessageDeliveryService(
        IHubContext<ChatHub> hubContext,
        ActiveConnectionRegistry connectionRegistry,
        ApplicationDbContext context,
        FirebaseNotificationService firebaseService,
        ILogger<MessageDeliveryService> logger)
    {
        _hubContext = hubContext;
        _connectionRegistry = connectionRegistry;
        _context = context;
        _firebaseService = firebaseService;
        _logger = logger;
    }

    public async Task DeliverAsync(
        MessageResponse message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await _hubContext.Clients
                .Group(ChatHub.GetConversationGroup(message.ConversationId))
                .SendAsync("MessageReceived", message, cancellationToken);
            _logger.LogInformation(
                "Mensaje enviado al grupo SignalR. MessageId={MessageId}, ConversationId={ConversationId}",
                message.Id,
                message.ConversationId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudo difundir el mensaje {MessageId} por SignalR.",
                message.Id);
        }

        var recipient = GetRecipient(message);
        if (_connectionRegistry.IsConnected(recipient))
        {
            return;
        }

        string? recipientToken;
        try
        {
            recipientToken = await GetRecipientTokenAsync(recipient, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudo consultar el token push del destinatario del mensaje {MessageId}.",
                message.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(recipientToken))
        {
            return;
        }

        try
        {
            await _firebaseService.SendNotificationAsync(
                recipientToken,
                "Nuevo mensaje",
                message.Text);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudo enviar la notificación del mensaje {MessageId}.",
                message.Id);
        }
    }

    private async Task<string?> GetRecipientTokenAsync(
        AuthenticatedUser recipient,
        CancellationToken cancellationToken)
    {
        return recipient.UserType switch
        {
            "trabajador" => await _context.Trabajadores
                .AsNoTracking()
                .Where(item => item.Id == recipient.UserId)
                .Select(item => item.FcmToken)
                .SingleOrDefaultAsync(cancellationToken),
            "compania" => await _context.Companias
                .AsNoTracking()
                .Where(item => item.Id == recipient.UserId)
                .Select(item => item.FcmToken)
                .SingleOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    private static AuthenticatedUser GetRecipient(MessageResponse message)
    {
        return string.Equals(message.Sender, "trabajador", StringComparison.OrdinalIgnoreCase)
            ? new AuthenticatedUser("compania", message.CompanyId)
            : new AuthenticatedUser("trabajador", message.WorkerId);
    }
}
