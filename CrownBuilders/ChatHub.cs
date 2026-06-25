using Microsoft.AspNetCore.SignalR;

namespace CrownBuilders.Hubs
{
    public class ChatHub : Hub
    {
        // Este método será llamado desde la interfaz del chat
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}