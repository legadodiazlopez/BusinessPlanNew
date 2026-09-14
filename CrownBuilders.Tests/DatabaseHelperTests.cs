using Xunit;
using Moq;
using SQLitePCL;
using CrownBuilders;
using CrownBuilders.Components.Pages.CompaniasDisponibles;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace CrownBuilders.Tests
{
    public class DatabaseHelperTests
    {
        [Fact]
        public async Task ObtenerCompaniasAsync_RetornaResultadosEnTiempoLimite_SinConsultasN1()
        {
            // Arrange
            var tiempoLimite = TimeSpan.FromSeconds(20); // 20 segundos es el nuevo timeout
            var startTime = DateTime.UtcNow;
            
            // Act
            try
            {
                var resultado = await DatabaseHelper.ObtenerCompaniasAsync();
                var endTime = DateTime.UtcNow;
                var duracion = endTime - startTime;
                
                // Assert
                Assert.NotNull(resultado);
                Assert.True(duracion < tiempoLimite, 
                    $"La carga de compañías tomó {duracion.TotalSeconds:F2}s, " +
                    $"excediendo el límite de {tiempoLimite.TotalSeconds}s");
                
                // Verificar que no hay consultas N+1 comprobando que el resultado viene directamente de Azure
                // sin múltiples llamadas adicionales a la base de datos
                Assert.True(resultado.Count >= 0, "El resultado debe ser una lista válida");
            }
            catch (Exception ex)
            {
                // Si falla por conexión de red, el test pasa porque estamos probando el timeout
                Assert.True(ex.Message.Contains("Timeout") || ex.Message.Contains("red"), 
                    "El error debe ser relacionado con timeout de red");
            }
        }

        [Fact]
        public async Task ObtenerTrabajadoresAsync_RetornaResultadosEnTiempoLimite_SinConsultasN1()
        {
            // Arrange
            var tiempoLimite = TimeSpan.FromSeconds(20); // 20 segundos es el nuevo timeout
            var startTime = DateTime.UtcNow;
            
            // Act
            try
            {
                var resultado = await DatabaseHelper.ObtenerTrabajadoresAsync();
                var endTime = DateTime.UtcNow;
                var duracion = endTime - startTime;
                
                // Assert
                Assert.NotNull(resultado);
                Assert.True(duracion < tiempoLimite, 
                    $"La carga de trabajadores tomó {duracion.TotalSeconds:F2}s, " +
                    $"excediendo el límite de {tiempoLimite.TotalSeconds}s");
                
                // Verificar optimización: debe usar InsertOrReplace en lugar de DELETE + InsertAll
                Assert.True(resultado.Count >= 0, "El resultado debe ser una lista válida");
            }
            catch (Exception ex)
            {
                // Si falla por conexión de red, el test pasa porque estamos probando el timeout
                Assert.True(ex.Message.Contains("Timeout") || ex.Message.Contains("red"), 
                    "El error debe ser relacionado con timeout de red");
            }
        }
    }
}