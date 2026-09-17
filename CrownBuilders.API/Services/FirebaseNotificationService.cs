using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace CrownBuilders.API.Services
{
    public class FirebaseNotificationService
    {
        private readonly IConfiguration _configuration;

        public FirebaseNotificationService(IConfiguration configuration)
        {
            _configuration = configuration;
            InicializarFirebase();
        }

        private void InicializarFirebase()
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                try
                {
                    var credential = FirebaseCredentialResolver.Resolve(
                        _configuration,
                        AppContext.BaseDirectory);
                    if (credential is null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[Firebase] ATENCIÓN: No se configuró una credencial administrativa.");
                        return;
                    }

                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = credential
                    });

                    System.Diagnostics.Debug.WriteLine("[Firebase] Inicializado correctamente.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Error al inicializar FirebaseApp: {ex.Message}");
                }
            }
        }

        public async Task<bool> SendNotificationAsync(string fcmToken, string title, string body)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fcmToken))
                {
                    System.Diagnostics.Debug.WriteLine("[Firebase] Token FCM no proporcionado.");
                    return false;
                }

                if (FirebaseApp.DefaultInstance == null)
                {
                    InicializarFirebase();
                    if (FirebaseApp.DefaultInstance == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[Firebase] No se pudo enviar la notificación: Servicio de Firebase no inicializado.");
                        return false;
                    }
                }

                // Construcción del mensaje para Firebase Cloud Messaging (FCM)
                var message = new Message()
                {
                    Token = fcmToken,
                    Notification = new Notification()
                    {
                        Title = title,
                        Body = body
                    }
                };

                // Envío en vivo a través de la infraestructura de Google
                string responseId = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                System.Diagnostics.Debug.WriteLine($"[Firebase] Notificación enviada exitosamente. ID: {responseId}");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Firebase] Error enviando notificación push: {ex.Message}");
                return false;
            }
        }
    }
}
