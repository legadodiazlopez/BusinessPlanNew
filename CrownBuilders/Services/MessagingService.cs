using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using CrownBuilders;

namespace CrownBuilders.Services;

public sealed class MessagingService : IAsyncDisposable
{
    private const int MaxPendingAttempts = 5;
    private readonly ApiService _apiService;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _hubConnection;
    private int? _conversationId;

    public MessagingService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public event Func<MensajeApiResponse, Task>? MessageReceived;

    public async Task StartAsync(int conversationId, CancellationToken cancellationToken = default)
    {
        if (conversationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conversationId));
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _hubConnection ??= CreateConnection();

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            _conversationId = conversationId;
            await _hubConnection.InvokeAsync(
                "JoinConversation",
                conversationId,
                cancellationToken).ConfigureAwait(false);
            await SyncPendingAsync(conversationId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<MensajeApiResponse> SendAsync(
        int conversationId,
        Guid clientMessageId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("SignalR no está conectado.");
        }

        return await _hubConnection.InvokeAsync<MensajeApiResponse>(
            "SendMessage",
            new
            {
                ConversationId = conversationId,
                ClientMessageId = clientMessageId,
                Text = text
            },
            cancellationToken).ConfigureAwait(false);
    }

    private HubConnection CreateConnection()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        var connection = new HubConnectionBuilder()
            .WithUrl(ApiEndpoints.SignalRHubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_apiService.AuthToken);
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.WebSockets;
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(20)
            })
            .WithServerTimeout(TimeSpan.FromSeconds(30))
            .Build();

        connection.On<MensajeApiResponse>("MessageReceived", HandleMessageReceivedAsync);
        connection.Reconnected += async _ =>
        {
            if (_conversationId is int conversationId)
            {
                await connection.InvokeAsync("JoinConversation", conversationId).ConfigureAwait(false);
                await SyncPendingAsync(conversationId).ConfigureAwait(false);
            }
        };

        return connection;
    }

    private async Task SyncPendingAsync(
        int conversationId,
        CancellationToken cancellationToken = default)
    {
        var pendingMessages = await DatabaseHelper
            .ObtenerMensajesPendientesAsync(conversationId)
            .ConfigureAwait(false);

        foreach (var pending in pendingMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pending.Intentos >= MaxPendingAttempts ||
                !Guid.TryParse(pending.ClientMessageId, out var clientMessageId))
            {
                continue;
            }

            try
            {
                var response = await SendAsync(
                    conversationId,
                    clientMessageId,
                    pending.Texto,
                    cancellationToken).ConfigureAwait(false);
                await DatabaseHelper.MarcarMensajeSincronizadoAsync(
                    pending.ClientMessageId,
                    response.Id,
                    response.ConversationId).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                !cancellationToken.IsCancellationRequested &&
                exception is HubException or HttpRequestException or TimeoutException or InvalidOperationException)
            {
                await DatabaseHelper.RegistrarFalloMensajeAsync(pending.ClientMessageId)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task HandleMessageReceivedAsync(MensajeApiResponse message)
    {
        var handlers = MessageReceived;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<MensajeApiResponse, Task>>())
        {
            await handler(message).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync().ConfigureAwait(false);
        }

        _connectionLock.Dispose();
    }
}
