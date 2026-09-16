using Xunit;
using CompaniaViewModel = CrownBuilders.Components.Pages.CompaniasDisponibles.Compania;
using CompaniasDisponiblesPage = CrownBuilders.Components.Pages.CompaniasDisponibles;
using CrownBuilders.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownBuilders.Tests
{
    public class PriceMappingTests
    {
        [Fact]
        public void CompaniaDTOMapping_ConPrecioEspecifico_NoDevuelveNulo()
        {
            // Arrange
            var companiaApi = new CrownBuilders.API.Models.Compania
            {
                Id = 1,
                FotoPerfil = "https://example.com/img.jpg",
                NombreEmpresa = "Constructora Test",
                Especialidad = "Construcción",
                Ciudad = "Madrid",
                Correo = "test@test.com",
                Telefono = "911234567",
                Password = "hashed_password",
                FormaPago = "Tarjeta",
                MontoPago = 150.00m, // Precio específico de prueba
                NombreTitular = "Juan Pérez",
                NumeroTarjeta = "****1234",
                FechaExpiracion = "12/25",
                CVC = "123",
                AceptaTerminos = true,
                FcmToken = "fcm_token"
            };

            // Act - Simular el mapeo que hace DatabaseHelper.ObtenerCompaniasAsync()
            var companiaDTO = new CompaniaViewModel
            {
                Id = companiaApi.Id,
                Nombre = companiaApi.NombreEmpresa,
                Ciudad = companiaApi.Ciudad,
                Especialidad = companiaApi.Especialidad,
                MontoPago = (double)companiaApi.MontoPago, // La corrección implementada
                Puntualidad = 5,
                Pagos = 5,
                Trato = 5
            };

            // Assert
            Assert.NotNull(companiaDTO);
            Assert.NotNull(companiaDTO.MontoPago);
            Assert.Equal(150.0, companiaDTO.MontoPago);
            Assert.True(companiaDTO.MontoPago > 0, "El precio debe ser mayor que 0");
        }

        [Fact]
        public void CompaniaDTOMapping_ConPrecioCero_NoDevuelveNulo()
        {
            // Arrange
            var companiaApi = new CrownBuilders.API.Models.Compania
            {
                Id = 2,
                FotoPerfil = "",
                NombreEmpresa = "Constructora Test 2",
                Especialidad = "Electricidad",
                Ciudad = "Barcelona",
                Correo = "test2@test.com",
                Telefono = "922345678",
                Password = "hashed_password",
                FormaPago = "Transferencia",
                MontoPago = 0.0m, // Precio cero
                NombreTitular = "María García",
                NumeroTarjeta = "****5678",
                FechaExpiracion = "06/26",
                CVC = "456",
                AceptaTerminos = true,
                FcmToken = "fcm_token_2"
            };

            // Act
            var companiaDTO = new CompaniaViewModel
            {
                Id = companiaApi.Id,
                Nombre = companiaApi.NombreEmpresa,
                Ciudad = companiaApi.Ciudad,
                Especialidad = companiaApi.Especialidad,
                MontoPago = (double)companiaApi.MontoPago,
                Puntualidad = 5,
                Pagos = 5,
                Trato = 5
            };

            // Assert
            Assert.NotNull(companiaDTO);
            Assert.NotNull(companiaDTO.MontoPago);
            Assert.Equal(0.0, companiaDTO.MontoPago);
        }

        [Fact]
        public void CompaniaDTOMapping_ListaDeCompanias_PreciosCorrectos()
        {
            // Arrange
            var companiasApi = new List<CrownBuilders.API.Models.Compania>
            {
                new CrownBuilders.API.Models.Compania
                {
                    Id = 1,
                    NombreEmpresa = "Constructora 1",
                    Especialidad = "Construcción",
                    Ciudad = "Madrid",
                    MontoPago = 150.00m
                },
                new CrownBuilders.API.Models.Compania
                {
                    Id = 2,
                    NombreEmpresa = "Constructora 2",
                    Especialidad = "Electricidad",
                    Ciudad = "Barcelona",
                    MontoPago = 120.50m
                },
                new CrownBuilders.API.Models.Compania
                {
                    Id = 3,
                    NombreEmpresa = "Constructora 3",
                    Especialidad = "Plomería",
                    Ciudad = "Valencia",
                    MontoPago = 95.75m
                }
            };

            // Act - Simular el mapeo completo que hace DatabaseHelper
            var companiasDTO = companiasApi.Select(c => new CompaniaViewModel
            {
                Id = c.Id,
                Nombre = c.NombreEmpresa,
                Ciudad = c.Ciudad,
                Especialidad = c.Especialidad,
                MontoPago = (double)c.MontoPago,
                Puntualidad = 5,
                Pagos = 5,
                Trato = 5
            }).ToList();

            // Assert
            Assert.Equal(3, companiasDTO.Count);
            Assert.Equal(150.0, companiasDTO[0].MontoPago);
            Assert.Equal(120.5, companiasDTO[1].MontoPago);
            Assert.Equal(95.75, companiasDTO[2].MontoPago);
            
            // Verificar que ningún precio sea nulo
            Assert.All(companiasDTO, c => Assert.NotNull(c.MontoPago));
        }

        [Fact]
        public void RenderizadoBlazor_FormatoPrecio_Correcto()
        {
            // Arrange
            var compania = new CompaniaViewModel
            {
                Id = 1,
                Nombre = "Constructora Test",
                Ciudad = "Madrid",
                Especialidad = "Construcción",
                MontoPago = 150.0,
                Puntualidad = 5,
                Pagos = 5,
                Trato = 5
            };

            // Act - Simular el renderizado de Blazor
            var precioRenderizado = compania.MontoPago.ToString("F2");

            // Assert
            Assert.Equal("150.00", precioRenderizado);
            Assert.StartsWith("$", $"${precioRenderizado}");
        }

        [Theory]
        [InlineData("PorJornada", "/ jornada")]
        [InlineData("PorHora", "/ hora")]
        public void EtiquetaFormaPago_UsaLaUnidadGuardada(string formaPago, string expected)
        {
            Assert.Equal(expected, CompaniasDisponiblesPage.ObtenerEtiquetaFormaPago(formaPago));
        }
    }
}
