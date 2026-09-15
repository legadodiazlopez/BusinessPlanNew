using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Services;

public sealed record AuthenticatedUser(string UserType, int UserId);

public sealed record SendMessageRequest(int ConversationId, Guid ClientMessageId, string Text);

public enum MessageWriteOutcome
{
    Created,
    AlreadyExists
}

public sealed record MessageWriteResult(Mensaje Message, MessageWriteOutcome Outcome);

public sealed class ConversationNotFoundException : Exception
{
    public ConversationNotFoundException(int conversationId)
        : base($"No existe la conversación {conversationId}.")
    {
    }
}

public sealed class MessageConflictException : Exception
{
    public MessageConflictException(Guid clientMessageId)
        : base($"La clave de cliente {clientMessageId} ya fue usada con otro mensaje.")
    {
    }
}

public sealed class MessagePersistenceService
{
    private readonly ApplicationDbContext _context;

    public MessagePersistenceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MessageWriteResult> SendAsync(
        SendMessageRequest request,
        AuthenticatedUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);

        var validation = MessageContentValidator.Validate(request.Text);
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error, nameof(request));
        }

        if (request.ConversationId <= 0)
        {
            throw new ArgumentException("La conversación es obligatoria.", nameof(request));
        }

        if (request.ClientMessageId == Guid.Empty)
        {
            throw new ArgumentException("La clave de cliente es obligatoria.", nameof(request));
        }

        var conversation = await _context.Conversaciones
            .SingleOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            throw new ConversationNotFoundException(request.ConversationId);
        }

        if (!ConversationAccessPolicy.CanAccess(
                conversation.TrabajadorId,
                conversation.CompaniaId,
                user.UserType,
                user.UserId))
        {
            throw new UnauthorizedAccessException("El usuario no pertenece a la conversación.");
        }

        var text = request.Text.Trim();
        var senderType = user.UserType.Trim().ToLowerInvariant();
        var existing = await _context.Mensajes
            .SingleOrDefaultAsync(
                message => message.ConversacionId == conversation.Id &&
                           message.ClientMessageId == request.ClientMessageId,
                cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.Texto, text, StringComparison.Ordinal) ||
                !string.Equals(existing.Remitente, senderType, StringComparison.Ordinal))
            {
                throw new MessageConflictException(request.ClientMessageId);
            }

            return new MessageWriteResult(existing, MessageWriteOutcome.AlreadyExists);
        }

        var message = new Mensaje
        {
            ConversacionId = conversation.Id,
            TrabajadorId = conversation.TrabajadorId,
            CompaniaId = conversation.CompaniaId,
            ClientMessageId = request.ClientMessageId,
            Remitente = senderType,
            Texto = text,
            FechaEnvio = DateTime.UtcNow
        };

        _context.Mensajes.Add(message);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            var concurrentMessage = await _context.Mensajes
                .SingleOrDefaultAsync(
                    item => item.ConversacionId == conversation.Id &&
                            item.ClientMessageId == request.ClientMessageId,
                    cancellationToken);

            if (concurrentMessage is null)
            {
                throw;
            }

            if (!string.Equals(concurrentMessage.Texto, text, StringComparison.Ordinal) ||
                !string.Equals(concurrentMessage.Remitente, senderType, StringComparison.Ordinal))
            {
                throw new MessageConflictException(request.ClientMessageId);
            }

            return new MessageWriteResult(concurrentMessage, MessageWriteOutcome.AlreadyExists);
        }

        return new MessageWriteResult(message, MessageWriteOutcome.Created);
    }
}
