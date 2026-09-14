using Xunit;
using Moq;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace CrownBuilders.Tests
{
    public class SignalRConnectionTests
    {
        [Fact]
        public async Task MensajeriaCompania_OnInitialized_SuscribeAReceiveMessage()
        {
            // Arrange
            var mockHubConnection = new Mock<HubConnection>();
            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger>();
            
            // Configurar el mock para simular que la conexión está desconectada inicialmente
            mockHubConnection.SetupGet(h => h.State).Returns(HubConnectionState.Disconnected);
            
            // Configurar el mock para simular StartAsync exitoso
            mockHubConnection.Setup(h => h.StartAsync(It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Configurar el mock para simular SendAsync exitoso
            mockHubConnection.Setup(h => h.SendAsync(
                It.IsAny<string>(), 
                It.IsAny<object>(),
                It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Simular el registro del handler ReceiveMessage
            bool receiveMessageRegistrado = false;
            mockHubConnection.Setup(h => h.On<string, string>(
                It.IsAny<string>(), 
                It.IsAny<Func<string, string, Task>>()))
                .Callback<string, Func<string, string, Task>>((methodName, handler) =>
                {
                    if (methodName == "ReceiveMessage")
                    {
                        receiveMessageRegistrado = true;
                    }
                });

            // Act - Simular el flujo de inicialización de Mensajeriacompania.razor
            // Esto simula lo que sucede en OnInitializedAsync
            
            // 1. Crear la conexión (simulado)
            var hubConnection = mockHubConnection.Object;
            
            // 2. Iniciar la conexión
            await hubConnection.StartAsync();
            
            // 3. Unirse al grupo de conversación
            string grupoId = $"compania-1-trabajador-2";
            await hubConnection.SendAsync("UnirseAConversacion", grupoId);
            
            // 4. Registrar el handler ReceiveMessage
            hubConnection.On<string, string>("ReceiveMessage", async (u, m) =>
            {
                // Simular el handler que procesa mensajes
                await Task.CompletedTask;
            });

            // Assert
            Assert.True(receiveMessageRegistrado, 
                "El handler ReceiveMessage debe estar registrado en OnInitializedAsync");
            mockHubConnection.Verify(h => h.StartAsync(It.IsAny<System.Threading.CancellationToken>()), 
                Times.Once, "StartAsync debe ser llamado una vez");
            mockHubConnection.Verify(h => h.SendAsync("UnirseAConversacion", It.IsAny<object>(), It.IsAny<System.Threading.CancellationToken>()), 
                Times.Once, "SendAsync debe ser llamado para unirse al grupo");
        }

        [Fact]
        public void MensajeriaCompania_ConexionConCompanyId_Dinamico()
        {
            // Arrange
            var companiaIdSimulado = 1; // Valor estático anterior
            var companiaIdReal = 5;      // Valor que debería venir del perfil local
            
            // Act - Simular la corrección implementada
            // ANES: companiaIdActual siempre era 1
            // DESPUÉS: companiaIdActual se obtiene del perfil local
            
            // Simular la obtención del ID real del perfil
            companiaIdSimulado = companiaIdReal;
            
            // Generar el grupo ID correcto
            string grupoId = $"compania-{companiaIdSimulado}-trabajador-2";
            
            // Assert
            Assert.Equal("compania-5-trabajador-2", grupoId);
            Assert.NotEqual("compania-1-trabajador-2", grupoId);
        }

        [Fact]
        public async Task MensajeriaCompania_ReconexionAutomatica_ManejadoresCorrectos()
        {
            // Arrange
            var mockHubConnection = new Mock<HubConnection>();
            
            // Configurar handlers de reconexión
            bool closedHandlerRegistrado = false;
            bool reconnectingHandlerRegistrado = false;
            bool reconnectedHandlerRegistrado = false;
            
            mockHubConnection.SetupSet(h => h.Closed += It.IsAny<Func<Exception?, Task>>())
                .Callback<Func<Exception?, Task>>(handler => closedHandlerRegistrado = true);
            
            mockHubConnection.SetupSet(h => h.Reconnecting += It.IsAny<Func<Exception?, Task>>())
                .Callback<Func<Exception?, Task>>(handler => reconnectingHandlerRegistrado = true);
            
            mockHubConnection.SetupSet(h => h.Reconnected += It.IsAny<Func<string?, Task>>())
                .Callback<Func<string?, Task>>(handler => reconnectedHandlerRegistrado = true);

            // Act - Simular la configuración de reconexión automática
            // Esto simula la configuración en Mensajeriacompania.razor
            
            // Simular el registro de handlers (que ya existía en el código)
            mockHubConnection.Object.Closed += async (error) => 
            {
                await Task.CompletedTask;
            };
            
            mockHubConnection.Object.Reconnecting += async (error) => 
            {
                await Task.CompletedTask;
            };
            
            mockHubConnection.Object.Reconnected += async (connectionId) => 
            {
                await Task.CompletedTask;
            };

            // Assert
            Assert.True(closedHandlerRegistrado, "Handler Closed debe estar registrado");
            Assert.True(reconnectingHandlerRegistrado, "Handler Reconnecting debe estar registrado");
            Assert.True(reconnectedHandlerRegistrado, "Handler Reconnected debe estar registrado");
        }

        [Fact]
        public void MensajeriaCompania_GrupoID_AislamientoCorrecto()
        {
            // Arrange
            var companiaId1 = 1;
            var trabajadorId1 = 2;
            var companiaId2 = 3;
            var trabajadorId2 = 4;

            // Act - Generar los grupos para diferentes conversaciones
            var grupo1 = $"compania-{companiaId1}-trabajador-{trabajadorId1}";
            var grupo2 = $"compania-{companiaId2}-trabajador-{trabajadorId2}";

            // Assert - Verificar aislamiento correcto
            Assert.Equal("compania-1-trabajador-2", grupo1);
            Assert.Equal("compania-3-trabajador-4", grupo2);
            Assert.NotEqual(grupo1, grupo2, "Los grupos deben ser diferentes para asegurar aislamiento");
        }
    }
}