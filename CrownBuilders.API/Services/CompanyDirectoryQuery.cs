using CrownBuilders.API.Contracts;
using CrownBuilders.API.Data;
using Microsoft.EntityFrameworkCore;

namespace CrownBuilders.API.Services;

public static class CompanyDirectoryQuery
{
    public static async Task<List<CompanyDirectoryItemResponse>> LoadAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var companies = await context.Companias
            .AsNoTracking()
            .Select(company => new
            {
                company.Id,
                company.NombreEmpresa,
                company.Especialidad,
                company.Ciudad,
                company.FormaPago,
                company.MontoPago,
                TieneFotoPerfil = company.FotoPerfil != null && company.FotoPerfil != ""
            })
            .ToListAsync(cancellationToken);

        var ratings = await context.Resenas
            .AsNoTracking()
            .Where(review => review.TipoReceptor == "Compania")
            .GroupBy(review => review.ReceptorId)
            .Select(group => new
            {
                CompanyId = group.Key,
                Total = group.Count(),
                Puntualidad = group.Average(review => (double)review.Calificacion1),
                Pagos = group.Average(review => (double)review.Calificacion2),
                Trato = group.Average(review => (double)review.Calificacion3)
            })
            .ToDictionaryAsync(rating => rating.CompanyId, cancellationToken);

        return companies.Select(company =>
        {
            ratings.TryGetValue(company.Id, out var rating);
            return new CompanyDirectoryItemResponse
            {
                Id = company.Id,
                NombreEmpresa = company.NombreEmpresa,
                Especialidad = company.Especialidad,
                Ciudad = company.Ciudad,
                FormaPago = company.FormaPago,
                MontoPago = company.MontoPago,
                TieneFotoPerfil = company.TieneFotoPerfil,
                TotalResenas = rating?.Total ?? 0,
                Puntualidad = rating?.Puntualidad ?? 0,
                Pagos = rating?.Pagos ?? 0,
                Trato = rating?.Trato ?? 0
            };
        }).ToList();
    }
}
