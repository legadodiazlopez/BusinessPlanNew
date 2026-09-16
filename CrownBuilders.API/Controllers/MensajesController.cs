using CrownBuilders.API.Contracts;
using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using CrownBuilders.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class MensajesController : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    private readonly ApplicationDbContext _context;
    private readonly ConversationService _conversationService;
    private readonly MessagePersistenceService _messagePersistenceService;
    private readonly MessageDeliveryService _messageDeliveryService;

    public MensajesController(
        ApplicationDbContext context,
        ConversationService conversationService,
        MessagePersistenceService messagePersistenceService,
        MessageDeliveryService messageDeliveryService)
    {
        _context = context;
        _conversationService = conversationService;
        _messagePersistenceService = messagePersistenceService;
        _messageDeliveryService = messageDeliveryService;
    }

    [HttpPost("conversaciones")]
    public async Task<ActionResult<ConversationResponse>> CreateConversation(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var user = AuthenticatedUserAccessor.FromPrincipal(User);
        if (!ConversationAccessPolicy.CanAccess(
                request.WorkerId,
                request.CompanyId,
                user.UserType,
                user.UserId))
        {
            return Forbid();
        }

        if (!await _context.Trabajadores.AnyAsync(
                worker => worker.Id == request.WorkerId,
                cancellationToken) ||
            !await _context.Companias.AnyAsync(
                company => company.Id == request.CompanyId,
                cancellationToken))
        {
            return NotFound(new { error = "No existe uno de los participantes." });
        }

        var conversation = await _conversationService.GetOrCreateAsync(
            request.WorkerId,
            request.CompanyId,
            cancellationToken);

        return Ok(ToConversationResponse(conversation));
    }

    [HttpPost("conversaciones/{conversationId:int}/mensajes")]
    public async Task<ActionResult<MessageResponse>> SendMessage(
        int conversationId,
        [FromBody] SendMessageHttpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = AuthenticatedUserAccessor.FromPrincipal(User);
            var result = await _messagePersistenceService.SendAsync(
                new SendMessageRequest(conversationId, request.ClientMessageId, request.Text),
                user,
                cancellationToken);
            var response = MessageResponseMapper.FromEntity(result.Message);

            if (result.Outcome == MessageWriteOutcome.Created)
            {
                await _messageDeliveryService.DeliverAsync(response);
            }

            return result.Outcome == MessageWriteOutcome.Created
                ? CreatedAtAction(
                    nameof(GetHistory),
                    new { conversationId },
                    response)
                : Ok(response);
        }
        catch (ConversationNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (MessageConflictException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("conversaciones/{conversationId:int}/mensajes")]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> GetHistory(
        int conversationId,
        [FromQuery] int limit = DefaultPageSize,
        [FromQuery] DateTime? beforeSentAtUtc = null,
        [FromQuery] int? beforeMessageId = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxPageSize)
        {
            return BadRequest(new { error = $"El límite debe estar entre 1 y {MaxPageSize}." });
        }

        if (beforeSentAtUtc.HasValue != beforeMessageId.HasValue)
        {
            return BadRequest(new { error = "El cursor requiere fecha e identificador del mensaje." });
        }

        try
        {
            var user = AuthenticatedUserAccessor.FromPrincipal(User);
            await _conversationService.GetAuthorizedAsync(conversationId, user, cancellationToken);

            var query = _context.Mensajes
                .AsNoTracking()
                .Where(message => message.ConversacionId == conversationId);

            if (beforeSentAtUtc.HasValue && beforeMessageId.HasValue)
            {
                query = query.Where(message =>
                    message.FechaEnvio < beforeSentAtUtc.Value ||
                    (message.FechaEnvio == beforeSentAtUtc.Value &&
                     message.Id < beforeMessageId.Value));
            }

            var messages = await query
                .OrderByDescending(message => message.FechaEnvio)
                .ThenByDescending(message => message.Id)
                .Take(limit)
                .Select(message => new MessageResponse(
                    message.Id,
                    message.ConversacionId,
                    message.TrabajadorId,
                    message.CompaniaId,
                    message.ClientMessageId,
                    message.Remitente,
                    message.Texto,
                    message.FechaEnvio))
                .ToListAsync(cancellationToken);

            messages.Reverse();
            return Ok(messages);
        }
        catch (ConversationNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [Obsolete("Usar el endpoint de conversaciones con ClientMessageId.")]
    [HttpPost("guardar")]
    public async Task<ActionResult<MessageResponse>> SaveLegacyMessage(
        [FromBody] LegacyMessageRequest request,
        CancellationToken cancellationToken)
    {
        var user = AuthenticatedUserAccessor.FromPrincipal(User);
        if (!ConversationAccessPolicy.CanAccess(
                request.TrabajadorId,
                request.CompaniaId,
                user.UserType,
                user.UserId))
        {
            return Forbid();
        }

        var conversation = await _conversationService.GetOrCreateAsync(
            request.TrabajadorId,
            request.CompaniaId,
            cancellationToken);

        try
        {
            var result = await _messagePersistenceService.SendAsync(
                new SendMessageRequest(conversation.Id, request.ClientMessageId, request.Texto),
                user,
                cancellationToken);
            var response = MessageResponseMapper.FromEntity(result.Message);
            if (result.Outcome == MessageWriteOutcome.Created)
            {
                await _messageDeliveryService.DeliverAsync(response);
            }

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (MessageConflictException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpGet("historial/{trabajadorId:int}/{companiaId:int}")]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> GetLegacyHistory(
        int trabajadorId,
        int companiaId,
        CancellationToken cancellationToken)
    {
        var conversation = await _context.Conversaciones
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TrabajadorId == trabajadorId && item.CompaniaId == companiaId,
                cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        return await GetHistory(conversation.Id, DefaultPageSize, null, null, cancellationToken);
    }

    private static ConversationResponse ToConversationResponse(Conversacion conversation)
    {
        return new ConversationResponse(
            conversation.Id,
            conversation.TrabajadorId,
            conversation.CompaniaId,
            conversation.FechaCreacionUtc);
    }
}

public sealed record CreateConversationRequest(int WorkerId, int CompanyId);

public sealed record ConversationResponse(
    int Id,
    int WorkerId,
    int CompanyId,
    DateTime CreatedAtUtc);

public sealed record SendMessageHttpRequest(Guid ClientMessageId, string Text);

public sealed record LegacyMessageRequest(
    int TrabajadorId,
    int CompaniaId,
    Guid ClientMessageId,
    string Texto);
