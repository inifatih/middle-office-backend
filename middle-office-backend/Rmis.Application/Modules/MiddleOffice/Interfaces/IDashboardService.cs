using Microsoft.AspNetCore.Http;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Dtos;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace middle_office_backend.Rmis.Application.Modules.MiddleOffice.Interfaces
{
    public interface IDashboardService
    {
        // One file per period the caller wants plotted (e.g. one "upload" workbook per day).
        Task<DashboardResponseDto> BuildKajianRisikoDashboardAsync(IReadOnlyList<IFormFile> files, CancellationToken ct);

        // sheetTipe: "Kontraktual" or "Behavioral" — selects which sheet to read from each file.
        Task<DashboardResponseDto> BuildResumeHoDashboardAsync(IReadOnlyList<IFormFile> files, string sheetTipe, CancellationToken ct);

        Task<DashboardResponseDto> BuildProfilKlnDashboardAsync(IReadOnlyList<IFormFile> files, CancellationToken ct);
    }
}
