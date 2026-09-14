using Microsoft.AspNetCore.SignalR;

namespace CrownBuilders.API.Hubs
{
    public class ChatHub : Hub
    {
        // Método para registrar o unir al cliente al grupo usando directamente el ID
        public async Task UnirseAConversacion(string usuarioId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, usuarioId);
        }

        // Enviar mensaje dirigido exclusivamente al grupo de ese ID (evita rebotes globales)
        public async Task EnviarMensajePrivado(string usuarioId, string usuario, string mensaje)
        {
            await Clients.Group(usuarioId).SendAsync("ReceiveMessage", usuario, mensaje);
        }
    }
}