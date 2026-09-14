using Microsoft.AspNetCore.Mvc;
using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResenasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ResenasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST /api/resenas - Guardar nueva reseña
        [HttpPost]
        public async Task<IActionResult> GuardarResena([FromBody] Resena resena)
        {
            try
            {
                if (resena == null)
                {
                    System.Diagnostics.Debug.WriteLine("[RESEÑAS] Datos de reseña nulos");
                    return BadRequest(new { error = "Datos de reseña inválidos" });
                }

                if (resena.EmisorId <= 0 || resena.ReceptorId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[RESEÑAS] IDs inválidos: EmisorId={resena.EmisorId}, ReceptorId={resena.ReceptorId}");
                    return BadRequest(new { error = "IDs de emisor o receptor inválidos" });
                }

                if (string.IsNullOrWhiteSpace(resena.TipoReceptor))
                {
                    System.Diagnostics.Debug.WriteLine("[RESEÑAS] TipoReceptor no proporcionado");
                    return BadRequest(new { error = "Tipo de receptor no especificado" });
                }

                resena.FechaCreacion = DateTime.UtcNow;
                _context.Resenas.Add(resena);
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"[RESEÑAS] Reseña guardada exitosamente: EmisorId={resena.EmisorId}, ReceptorId={resena.ReceptorId}, Tipo={resena.TipoReceptor}");
                return Ok(new { mensaje = "Reseña guardada exitosamente", id = resena.Id });
            }
            catch (DbUpdateException dbEx)
            {
                System.Diagnostics.Debug.WriteLine($"[RESEÑAS] Error de base de datos: {dbEx.Message}");
                System.Diagnostics.Debug.WriteLine($"[RESEÑAS] Inner exception: {dbEx.InnerException?.Message}");
                return StatusCode(500, new { error = "Error de base de datos al guardar reseña", detalle = dbEx.InnerException?.Message });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RESEÑAS] Error al guardar reseña: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[RESEÑAS] Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Error al guardar reseña", detalle = ex.Message });
            }
        }

        // GET /api/resenas/promedio/{receptorId}/{tipo} - Obtener promedio de calificaciones
        [HttpGet("promedio/{receptorId}/{tipo}")]
        public async Task<IActionResult> ObtenerPromedio(int receptorId, string tipo)
        {
            try
            {
                var resenas = await _context.Resenas
                    .Where(r => r.ReceptorId == receptorId && r.TipoReceptor == tipo)
                    .ToListAsync();

                if (resenas.Count == 0)
                {
                    return Ok(new
                    {
                        totalResenas = 0,
                        promedioGeneral = 0.0,
                        promedioCalificacion1 = 0.0,
                        promedioCalificacion2 = 0.0,
                        promedioCalificacion3 = 0.0
                    });
                }

                double promedioGeneral = resenas.Average(r => (r.Calificacion1 + r.Calificacion2 + r.Calificacion3) / 3.0);
                double promedioCalificacion1 = resenas.Average(r => r.Calificacion1);
                double promedioCalificacion2 = resenas.Average(r => r.Calificacion2);
                double promedioCalificacion3 = resenas.Average(r => r.Calificacion3);

                return Ok(new
                {
                    totalResenas = resenas.Count,
                    promedioGeneral = Math.Round(promedioGeneral, 1),
                    promedioCalificacion1 = Math.Round(promedioCalificacion1, 1),
                    promedioCalificacion2 = Math.Round(promedioCalificacion2, 1),
                    promedioCalificacion3 = Math.Round(promedioCalificacion3, 1)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RESEÑAS] Error al obtener promedio: {ex.Message}");
                return StatusCode(500, new { error = "Error al obtener promedio" });
            }
        }

        // GET /api/resenas/{receptorId}/{tipo} - Obtener todas las reseñas de un receptor
        [HttpGet("{receptorId}/{tipo}")]
        public async Task<IActionResult> ObtenerResenas(int receptorId, string tipo)
        {
            try
            {
                var resenas = await _context.Resenas
                    .Where(r => r.ReceptorId == receptorId && r.TipoReceptor == tipo)
                    .OrderByDescending(r => r.FechaCreacion)
                    .ToListAsync();

                return Ok(resenas);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RESEÑAS] Error al obtener reseñas: {ex.Message}");
                return StatusCode(500, new { error = "Error al obtener reseñas" });
            }
        }
    }
}
