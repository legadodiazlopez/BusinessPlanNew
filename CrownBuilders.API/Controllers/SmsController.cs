using Microsoft.AspNetCore.Mvc;

namespace CrownBuilders.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SmsController : ControllerBase
    {
        // --- ENVIAR SMS (MOCK PARA PRUEBAS) ---
        [HttpPost("enviar")]
        public IActionResult EnviarSms([FromBody] SmsRequest request)
        {
            try
            {
                // En un entorno real, aquí se integraría con Azure Communication Services
                // Por ahora, retornamos un código de verificación mock para pruebas
                string codigoMock = "123456";
                
                System.Diagnostics.Debug.WriteLine($"[SMS MOCK] Enviando SMS a {request.Telefono}: {request.Mensaje}");
                System.Diagnostics.Debug.WriteLine($"[SMS MOCK] Código de verificación: {codigoMock}");
                
                return Ok(new { 
                    exitoso = true, 
                    codigo = codigoMock,
                    mensaje = "SMS enviado exitosamente (modo mock)"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { 
                    exitoso = false, 
                    codigo = "",
                    mensaje = "Error: " + ex.Message 
                });
            }
        }
    }

    // --- MODELO DE REQUEST ---
    public class SmsRequest
    {
        public string Telefono { get; set; }
        public string Mensaje { get; set; }
    }
}