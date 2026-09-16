using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using Microsoft.EntityFrameworkCore;
using CrownBuilders.API.Services;
using System.Security.Claims;

namespace CrownBuilders.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(ApplicationDbContext context, JwtTokenService jwtTokenService)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
        }

        // --- OBTENER TODOS LOS TRABAJADORES ---
        [HttpGet("trabajadores")]
        public async Task<IActionResult> ObtenerTrabajadores()
        {
            try
            {
                var trabajadores = await _context.Trabajadores.ToListAsync();
                return Ok(trabajadores);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTH] Error al obtener trabajadores: {ex.Message}");
                return StatusCode(500, new { error = "Error al obtener trabajadores" });
            }
        }

        // --- OBTENER TODAS LAS COMPAÑÍAS ---
        [HttpGet("companias")]
        public async Task<IActionResult> ObtenerCompanias()
        {
            try
            {
                var companias = await _context.Companias.ToListAsync();
                return Ok(companias);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTH] Error al obtener compañías: {ex.Message}");
                return StatusCode(500, new { error = "Error al obtener compañías" });
            }
        }

        // --- LOGIN TRABAJADOR ---
        [HttpPost("login-trabajador")]
        public async Task<IActionResult> LoginTrabajador([FromBody] LoginRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[AUTH] Login trabajador iniciado: {request.Correo}");
                
                if (string.IsNullOrWhiteSpace(request.Correo) || string.IsNullOrWhiteSpace(request.Password))
                {
                    System.Diagnostics.Debug.WriteLine($"[AUTH] Credenciales vacías proporcionadas");
                    return BadRequest(new { error = "Credenciales inválidas" });
                }

                var trabajador = await _context.Trabajadores
                    .FirstOrDefaultAsync(t => t.Correo == request.Correo && t.Password == request.Password);

                if (trabajador == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AUTH] Trabajador no encontrado para: {request.Correo}");
                    return Ok(new AuthResponse { 
                        Exitoso = false, 
                        Mensaje = "Credenciales incorrectas" 
                    });
                }

                System.Diagnostics.Debug.WriteLine($"[AUTH] Login exitoso para trabajador ID: {trabajador.Id}");
                
                var token = _jwtTokenService.CreateToken(
                    trabajador.Id,
                    "trabajador",
                    trabajador.Nombre);
                
                return Ok(new AuthResponse { 
                    Exitoso = true,
                    Mensaje = "Login exitoso", 
                    Id = trabajador.Id,
                    Nombre = trabajador.Nombre,
                    Correo = trabajador.Correo,
                    Tipo = "trabajador",
                    Token = token
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTH] Error en login trabajador: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AUTH] Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        // --- LOGIN COMPAÑÍA ---
        [HttpPost("login-compania")]
        public async Task<IActionResult> LoginCompania([FromBody] LoginRequest request)
        {
            try
            {
                var compania = await _context.Companias
                    .FirstOrDefaultAsync(c => c.Correo == request.Correo && c.Password == request.Password);

                if (compania == null)
                {
                    return Ok(new AuthResponse { 
                        Exitoso = false, 
                        Mensaje = "Credenciales incorrectas" 
                    });
                }

                var token = _jwtTokenService.CreateToken(
                    compania.Id,
                    "compania",
                    compania.NombreEmpresa);

                return Ok(new AuthResponse { 
                    Exitoso = true,
                    Mensaje = "Login exitoso", 
                    Id = compania.Id,
                    Nombre = compania.NombreEmpresa,
                    Correo = compania.Correo,
                    Tipo = "compania",
                    Token = token
                });
            }
            catch (Exception ex)
            {
                return Ok(new AuthResponse { 
                    Exitoso = false, 
                    Mensaje = "Error en el servidor: " + ex.Message 
                });
            }
        }

        // --- VERIFICAR CORREO EXISTENTE ---
        [HttpPost("verificar-correo")]
        public async Task<IActionResult> VerificarCorreo([FromBody] VerificarCorreoRequest request)
        {
            try
            {
                bool existeTrabajador = await _context.Trabajadores
                    .AnyAsync(t => t.Correo == request.Correo);
                
                bool existeCompania = await _context.Companias
                    .AnyAsync(c => c.Correo == request.Correo);

                return Ok(new VerificarCorreoResponse { 
                    Existe = existeTrabajador || existeCompania,
                    Tipo = existeTrabajador ? "trabajador" : (existeCompania ? "compania" : null)
                });
            }
            catch (Exception ex)
            {
                return Ok(new VerificarCorreoResponse { 
                    Existe = false, 
                    Tipo = null,
                    Error = ex.Message
                });
            }
        }

        // --- OBTENER PERFIL TRABAJADOR ---
        [HttpGet("perfil-trabajador/{id}")]
        public async Task<IActionResult> ObtenerPerfilTrabajador(int id)
        {
            try
            {
                var trabajador = await _context.Trabajadores.FindAsync(id);
                if (trabajador == null)
                {
                    return Ok(new { exitoso = false, mensaje = "Trabajador no encontrado" });
                }

                return Ok(trabajador);
            }
            catch (Exception ex)
            {
                return Ok(new { exitoso = false, mensaje = "Error en el servidor: " + ex.Message });
            }
        }

        // --- OBTENER PERFIL COMPAÑÍA ---
        [HttpGet("perfil-compania/{id}")]
        public async Task<IActionResult> ObtenerPerfilCompania(int id)
        {
            try
            {
                var compania = await _context.Companias.FindAsync(id);
                if (compania == null)
                {
                    return Ok(new { exitoso = false, mensaje = "Compañía no encontrada" });
                }

                return Ok(compania);
            }
            catch (Exception ex)
            {
                return Ok(new { exitoso = false, mensaje = "Error en el servidor: " + ex.Message });
            }
        }

        // --- ACTUALIZAR PERFIL TRABAJADOR ---
        [Authorize(Roles = "trabajador")]
        [HttpPut("actualizar-trabajador/{id}")]
        public async Task<IActionResult> ActualizarTrabajador(int id, [FromBody] Trabajador trabajador)
        {
            try
            {
                var usuarioAutenticado = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(usuarioAutenticado, out var trabajadorAutenticadoId) ||
                    trabajadorAutenticadoId != id)
                {
                    return Forbid();
                }

                if (id != trabajador.Id)
                {
                    return Ok(new { exitoso = false, mensaje = "ID mismatch" });
                }

                _context.Entry(trabajador).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(new { exitoso = true, mensaje = "Perfil actualizado exitosamente" });
            }
            catch (Exception ex)
            {
                return Ok(new { exitoso = false, mensaje = "Error en el servidor: " + ex.Message });
            }
        }

        // --- ACTUALIZAR PERFIL COMPAÑÍA ---
        [HttpPut("actualizar-compania/{id}")]
        public async Task<IActionResult> ActualizarCompania(int id, [FromBody] Compania compania)
        {
            try
            {
                if (id != compania.Id)
                {
                    return Ok(new { exitoso = false, mensaje = "ID mismatch" });
                }

                _context.Entry(compania).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(new { exitoso = true, mensaje = "Perfil actualizado exitosamente" });
            }
            catch (Exception ex)
            {
                return Ok(new { exitoso = false, mensaje = "Error en el servidor: " + ex.Message });
            }
        }
    }

    // --- MODELOS DE REQUEST Y RESPONSE ---
    public class LoginRequest
    {
        public string Correo { get; set; }
        public string Password { get; set; }
    }

    public class VerificarCorreoRequest
    {
        public string Correo { get; set; }
    }

    public class AuthResponse
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public string Tipo { get; set; }
        public string? Token { get; set; }
    }

    public class VerificarCorreoResponse
    {
        public bool Existe { get; set; }
        public string Tipo { get; set; }
        public string Error { get; set; }
    }
}
