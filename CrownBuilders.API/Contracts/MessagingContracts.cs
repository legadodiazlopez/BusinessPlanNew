namespace CrownBuilders.API.Contracts;

public sealed record MessageResponse(
    int Id,
    int ConversationId,
    int WorkerId,
    int CompanyId,
    Guid ClientMessageId,
    string Sender,
    string Text,
    DateTime SentAtUtc);

public static class MessageResponseMapper
{
    public static MessageResponse FromEntity(Models.Mensaje message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new MessageResponse(
            message.Id,
            message.ConversacionId,
            message.TrabajadorId,
            message.CompaniaId,
            message.ClientMessageId,
            message.Remitente,
            message.Texto,
            message.FechaEnvio);
    }
}
