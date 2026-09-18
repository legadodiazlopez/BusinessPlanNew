using Microsoft.AspNetCore.Mvc;
using CrownBuilders.API.Contracts;
using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PerfilController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PerfilController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- REGISTRO COMPAÑÍA ---
        [HttpPost("guardar-compania")]
        public async Task<IActionResult> GuardarCompania([FromBody] Compania datosCompania)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[API] Recibiendo registro compañía: {datosCompania.Correo}");
                
                var existe = await _context.Companias
                    .AnyAsync(c => c.Correo == datosCompania.Correo);

                if (existe)
                {
                    System.Diagnostics.Debug.WriteLine($"[API] Correo ya existe: {datosCompania.Correo}");
                    return Conflict(new { exitoso = false, mensaje = "El correo ya está registrado" });
                }

                _context.Companias.Add(datosCompania);
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"[API] Compañía registrada exitosamente, ID: {datosCompania.Id}");

                return Ok(new
                {
                    exitoso = true,
                    mensaje = "Registro exitoso",
                    id = datosCompania.Id,
                    correo = datosCompania.Correo
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error en GuardarCompania: {ex.Message}");
                return Ok(new { exitoso = false, mensaje = "Error en el servidor: " + ex.Message });
            }
        }

        // --- REGISTRO TRABAJADOR ---
        [HttpPost("guardar-trabajador")]
        public async Task<IActionResult> GuardarTrabajador([FromBody] Trabajador datosTrabajador)
        {
            try
            {
                var existe = await _context.Trabajadores
                    .AnyAsync(t => t.Correo == datosTrabajador.Correo);

                if (existe)
                {
                    return Conflict(new { exitoso = false, mensaje = "El correo ya está registrado" });
                }

                _context.Trabajadores.Add(datosTrabajador);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    exitoso = true,
                    mensaje = "Registro exitoso",
                    id = datosTrabajador.Id,
                    correo = datosTrabajador.Correo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor", error = ex.Message });
            }
        }

        // --- LISTAR TRABAJADORES ---
        [HttpGet("listar-trabajadores")]
        public async Task<IActionResult> GetTrabajadores()
        {
            try
            {
                var trabajadores = await _context.Trabajadores.ToListAsync();
                return Ok(trabajadores);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor", error = ex.Message });
            }
        }

        // --- LISTAR COMPAÑÍAS ---
        [HttpGet("listar-companias")]
        public async Task<IActionResult> GetCompanias(CancellationToken cancellationToken = default)
        {
            try
            {
                var companias = await _context.Companias
                    .AsNoTracking()
                    .Select(compania => new CompanyDirectoryItemResponse
                    {
                        Id = compania.Id,
                        NombreEmpresa = compania.NombreEmpresa,
                        Especialidad = compania.Especialidad,
                        Ciudad = compania.Ciudad,
                        FormaPago = compania.FormaPago,
                        MontoPago = compania.MontoPago
                    })
                    .ToListAsync(cancellationToken);
                return Ok(companias);
            }
            catch (Exception)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor" });
            }
        }

        // --- OBTENER TRABAJADOR POR ID ---
        [HttpGet("trabajador/{id}")]
        public async Task<IActionResult> GetTrabajador(int id)
        {
            try
            {
                var trabajador = await _context.Trabajadores.FindAsync(id);
                if (trabajador == null)
                {
                    return NotFound(new { exitoso = false, mensaje = "Trabajador no encontrado" });
                }
                return Ok(trabajador);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor", error = ex.Message });
            }
        }

        // --- OBTENER COMPAÑÍA POR ID ---
        [HttpGet("compania/{id}")]
        public async Task<IActionResult> GetCompania(int id)
        {
            try
            {
                var compania = await _context.Companias.FindAsync(id);
                if (compania == null)
                {
                    return NotFound(new { exitoso = false, mensaje = "Compañía no encontrada" });
                }
                return Ok(compania);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor", error = ex.Message });
            }
        }

        // --- ELIMINAR TRABAJADOR ---
        [HttpDelete("trabajador/{id}")]
        public async Task<IActionResult> DeleteTrabajador(int id)
        {
            try
            {
                var trabajador = await _context.Trabajadores.FindAsync(id);
                if (trabajador == null)
                {
                    return NotFound(new { exitoso = false, mensaje = "Trabajador no encontrado" });
                }

                _context.Trabajadores.Remove(trabajador);
                await _context.SaveChangesAsync();

                return Ok(new { exitoso = true, mensaje = "Trabajador eliminado exitosamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor", error = ex.Message });
            }
        }

        // --- ELIMINAR COMPAÑÍA ---
        [HttpDelete("compania/{id}")]
        public async Task<IActionResult> DeleteCompania(int id)
        {
            try
            {
                var compania = await _context.Companias.FindAsync(id);
                if (compania == null)
                {
                    return NotFound(new { exitoso = false, mensaje = "Compañía no encontrada" });
                }

                _context.Companias.Remove(compania);
                await _context.SaveChangesAsync();

                return Ok(new { exitoso = true, mensaje = "Compañía eliminada exitosamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor", error = ex.Message });
            }
        }

        // --- ACTUALIZAR FCM TOKEN TRABAJADOR ---
        [HttpPut("trabajador/{id}/token")]
        public async Task<IActionResult> UpdateTrabajadorFcmToken(int id, [FromBody] FcmTokenRequest request)
        {
            try
            {
                var trabajador = await _context.Trabajadores.FindAsync(id);
                if (trabajador == null)
                {
                    return NotFound(new { exitoso = false, mensaje = "Trabajador no encontrado" });
                }

                trabajador.FcmToken = request.FcmToken;
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"[API] FCM Token actualizado para trabajador ID: {id}");
                return Ok(new { exitoso = true, mensaje = "FCM Token actualizado exitosamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor", error = ex.Message });
            }
        }

        // --- ACTUALIZAR FCM TOKEN COMPAÑÍA ---
        [HttpPut("compania/{id}/token")]
        public async Task<IActionResult> UpdateCompaniaFcmToken(int id, [FromBody] FcmTokenRequest request)
        {
            try
            {
                var compania = await _context.Companias.FindAsync(id);
                if (compania == null)
                {
                    return NotFound(new { exitoso = false, mensaje = "Compañía no encontrada" });
                }

                compania.FcmToken = request.FcmToken;
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"[API] FCM Token actualizado para compañía ID: {id}");
                return Ok(new { exitoso = true, mensaje = "FCM Token actualizado exitosamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { exitoso = false, mensaje = "Error en el servidor", error = ex.Message });
            }
        }
    }

    // --- MODELO DE SOLICITUD PARA FCM TOKEN ---
    public class FcmTokenRequest
    {
        public string FcmToken { get; set; }
    }
}
