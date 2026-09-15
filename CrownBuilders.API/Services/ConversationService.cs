using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Services;

public sealed class ConversationService
{
    private readonly ApplicationDbContext _context;

    public ConversationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Conversacion> GetOrCreateAsync(
        int workerId,
        int companyId,
        CancellationToken cancellationToken = default)
    {
        ValidateParticipantIds(workerId, companyId);

        var existing = await _context.Conversaciones
            .SingleOrDefaultAsync(
                conversation => conversation.TrabajadorId == workerId &&
                                conversation.CompaniaId == companyId,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var conversation = new Conversacion
        {
            TrabajadorId = workerId,
            CompaniaId = companyId,
            FechaCreacionUtc = DateTime.UtcNow
        };

        _context.Conversaciones.Add(conversation);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return conversation;
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            var concurrentConversation = await _context.Conversaciones
                .SingleOrDefaultAsync(
                    item => item.TrabajadorId == workerId && item.CompaniaId == companyId,
                    cancellationToken);

            if (concurrentConversation is not null)
            {
                return concurrentConversation;
            }

            throw;
        }
    }

    public async Task<Conversacion> GetAuthorizedAsync(
        int conversationId,
        AuthenticatedUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var conversation = await _context.Conversaciones
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            throw new ConversationNotFoundException(conversationId);
        }

        if (!ConversationAccessPolicy.CanAccess(
                conversation.TrabajadorId,
                conversation.CompaniaId,
                user.UserType,
                user.UserId))
        {
            throw new UnauthorizedAccessException("El usuario no participa en la conversación.");
        }

        return conversation;
    }

    private static void ValidateParticipantIds(int workerId, int companyId)
    {
        if (workerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workerId));
        }

        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId));
        }
    }
}
