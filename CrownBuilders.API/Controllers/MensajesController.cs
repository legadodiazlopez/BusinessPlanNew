using Microsoft.AspNetCore.Mvc;
using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using CrownBuilders.API.Services;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MensajesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly FirebaseNotificationService _firebaseService;

        public MensajesController(ApplicationDbContext context, FirebaseNotificationService firebaseService)
        {
            _context = context;
            _firebaseService = firebaseService;
        }

        [HttpPost("guardar")]
        public async Task<IActionResult> GuardarMensaje([FromBody] Mensaje mensaje)
        {
            _context.Mensajes.Add(mensaje);
            await _context.SaveChangesAsync();

            // Enviar notificación push al receptor
            try
            {
                string? recipientToken = null;

                if (mensaje.Remitente == "trabajador")
                {
                    // Notificar a la compañía
                    var compania = await _context.Companias.FindAsync(mensaje.CompaniaId);
                    recipientToken = compania?.FcmToken;
                }
                else if (mensaje.Remitente == "compania")
                {
                    // Notificar al trabajador
                    var trabajador = await _context.Trabajadores.FindAsync(mensaje.TrabajadorId);
                    recipientToken = trabajador?.FcmToken;
                }

                if (!string.IsNullOrEmpty(recipientToken))
                {
                    await _firebaseService.SendNotificationAsync(
                        recipientToken,
                        "Nuevo mensaje",
                        mensaje.Texto ?? "Tienes un nuevo mensaje"
                    );
                    System.Diagnostics.Debug.WriteLine($"[Mensajes] Notificación push enviada al receptor");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Mensajes] No se encontró FCM token para el receptor");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Mensajes] Error enviando notificación: {ex.Message}");
            }

            return Ok(new { mensaje = "Mensaje guardado exitosamente", id = mensaje.Id });
        }

        [HttpGet("historial/{trabajadorId}/{companiaId}")]
        public async Task<IActionResult> ObtenerHistorial(int trabajadorId, int companiaId)
        {
            var mensajes = await _context.Mensajes
                .Where(m => m.TrabajadorId == trabajadorId && m.CompaniaId == companiaId)
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();
            return Ok(mensajes);
        }

        [HttpGet("historial/trabajador/{trabajadorId}")]
        public async Task<IActionResult> ObtenerMensajesPorTrabajador(int trabajadorId)
        {
            var mensajes = await _context.Mensajes
                .Where(m => m.TrabajadorId == trabajadorId)
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();
            return Ok(mensajes);
        }

        [HttpGet("historial/compania/{companiaId}")]
        public async Task<IActionResult> ObtenerMensajesPorCompania(int companiaId)
        {
            var mensajes = await _context.Mensajes
                .Where(m => m.CompaniaId == companiaId)
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();
            return Ok(mensajes);
        }
    }
}