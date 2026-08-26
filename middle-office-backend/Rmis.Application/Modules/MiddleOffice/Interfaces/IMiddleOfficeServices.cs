using Microsoft.AspNetCore.Http;
using middle_office_backend.Rmis.Application.Modules.MiddleOffice.Dtos;
using System.Threading;
using System.Threading.Tasks;

namespace middle_office_backend.Rmis.Application.Modules.MiddleOffice.Interfaces
{
    public interface IMiddleOfficeServices
    {
        Task<(bool Success, KajianRisikoExtractResponseDto Result)> ExtractKajianRisikoAsync(IFormFile file, CancellationToken ct);
        Task<(bool Success, ExtractResponseDto<ProfilMaturitasKlnDto> Result)> ExtractProfilMaturitasKlnAsync(IFormFile file, CancellationToken ct);
        Task<(bool Success, ResumeMatProfHoExtractResponseDto Result)> ExtractResumeMatProfHoAsync(IFormFile file, CancellationToken ct, string? preferredSheet = null);
    }
}
