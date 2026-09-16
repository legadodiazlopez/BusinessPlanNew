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
    public event Func<TypingNotification, Task>? TypingChanged;

    public async Task StartAsync(int conversationId, CancellationToken cancellationToken = default)
    {
        if (conversationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conversationId));
        }

        System.Diagnostics.Debug.WriteLine($"[CHAT] StartAsync solicitado para conversación {conversationId}");
        await _apiService.RestoreAuthTokenAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_apiService.AuthToken))
        {
            throw new InvalidOperationException("La sesión no está autenticada. Inicia sesión nuevamente.");
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _hubConnection ??= CreateConnection();

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync(cancellationToken).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[CHAT] Hub conectado: {_hubConnection.State}");
            }

            _conversationId = conversationId;
            try
            {
                await _hubConnection.InvokeAsync(
                    "JoinConversation",
                    conversationId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT] Error al unirse a la conversación {conversationId}: {exception}");
                throw;
            }

            System.Diagnostics.Debug.WriteLine($"[CHAT] Unido a conversación {conversationId}");
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

        System.Diagnostics.Debug.WriteLine(
            $"[CHAT] Enviando mensaje remoto. ConversationId={conversationId}, ClientMessageId={clientMessageId}");

        try
        {
#if ANDROID
            // En Android el endpoint HTTP envía el mensaje y SignalR queda como canal de recepción en tiempo real.
            return await _apiService.EnviarMensajeAsync(
                conversationId,
                clientMessageId,
                text,
                cancellationToken).ConfigureAwait(false);
#else
            var response = await _hubConnection.InvokeAsync<MensajeApiResponse>(
                "SendMessage",
                new
                {
                    ConversationId = conversationId,
                    ClientMessageId = clientMessageId,
                    Text = text
                },
                cancellationToken).ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine(
                $"[CHAT] Mensaje remoto confirmado. MessageId={response.Id}, ConversationId={response.ConversationId}");
            return response;
#endif
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CHAT] Error enviando mensaje remoto: {exception.GetType().Name}: {exception.Message}");
            throw;
        }
    }

    public async Task SetTypingAsync(
        int conversationId,
        bool isTyping,
        CancellationToken cancellationToken = default)
    {
        if (_hubConnection?.State != HubConnectionState.Connected || conversationId <= 0)
        {
            return;
        }

        try
        {
            await _hubConnection.InvokeAsync(
                "Typing",
                new object?[] { conversationId, isTyping },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is HubException or HttpRequestException or TimeoutException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CHAT] No se pudo actualizar el indicador de escritura: {exception.Message}");
        }
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
#if ANDROID
                // En el emulador Android el handshake WebSocket completa, pero las invocaciones no reciben respuesta.
                options.Transports = HttpTransportType.LongPolling;
#else
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
#endif
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
        connection.On<int, string, bool>("TypingChanged", HandleTypingChangedAsync);
        connection.Reconnecting += exception =>
        {
            System.Diagnostics.Debug.WriteLine($"[CHAT] SignalR reconectando: {exception?.Message ?? "sin detalle"}");
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            if (_conversationId is int conversationId)
            {
                await connection.InvokeAsync("JoinConversation", conversationId).ConfigureAwait(false);
                await SyncPendingAsync(conversationId).ConfigureAwait(false);
            }
        };
        connection.Closed += exception =>
        {
            System.Diagnostics.Debug.WriteLine($"[CHAT] SignalR cerrado: {exception?.Message ?? "sin detalle"}");
            return Task.CompletedTask;
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

    private async Task HandleTypingChangedAsync(
        int conversationId,
        string sender,
        bool isTyping)
    {
        if (_conversationId != conversationId)
        {
            return;
        }

        var handlers = TypingChanged;
        if (handlers is null)
        {
            return;
        }

        var notification = new TypingNotification(conversationId, sender, isTyping);
        foreach (var handler in handlers.GetInvocationList().Cast<Func<TypingNotification, Task>>())
        {
            await handler(notification).ConfigureAwait(false);
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

public sealed record TypingNotification(int ConversationId, string Sender, bool IsTyping);
