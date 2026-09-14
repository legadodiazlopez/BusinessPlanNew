using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
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
                    // 1. Intentar obtener la ruta desde appsettings.json si está especificada
                    string rutaCredencial = _configuration["Firebase:CredentialPath"];

                    // 2. Si no está especificada en appsettings.json, buscar automáticamente cualquier archivo .json dentro de FirebaseConfig
                    if (string.IsNullOrEmpty(rutaCredencial) || !File.Exists(rutaCredencial))
                    {
                        string carpetaConfig = Path.Combine(AppContext.BaseDirectory, "FirebaseConfig");
                        if (Directory.Exists(carpetaConfig))
                        {
                            var archivosJson = Directory.GetFiles(carpetaConfig, "*.json");
                            if (archivosJson.Length > 0)
                            {
                                rutaCredencial = archivosJson[0]; // Selecciona el JSON de la clave
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(rutaCredencial) || !File.Exists(rutaCredencial))
                    {
                        System.Diagnostics.Debug.WriteLine("[Firebase] ATENCIÓN: No se encontró el archivo de credencial JSON en la carpeta FirebaseConfig.");
                        return;
                    }

                    // Inicialización oficial usando el SDK de FirebaseAdmin
                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = GoogleCredential.FromFile(rutaCredencial)
                    });

                    System.Diagnostics.Debug.WriteLine($"[Firebase] Inicializado con éxito usando: {Path.GetFileName(rutaCredencial)}");
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