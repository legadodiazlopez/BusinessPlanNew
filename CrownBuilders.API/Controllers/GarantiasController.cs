using Microsoft.AspNetCore.Mvc;

namespace CrownBuilders.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GarantiasController : ControllerBase
    {
        [HttpPost("reclamar")]
        public IActionResult Reclamar([FromBody] object garantia)
        {
            // Aquí recibirá los datos de tu SyncService
            Console.WriteLine("=== ¡API RECIBIÓ UNA GARANTÍA POR INASISTENCIA! ===");

            // Retornamos OK para que el SyncService sepa que todo salió bien
            return Ok(new { mensaje = "Garantía procesada correctamente." });
        }
    }
}