namespace middle_office_backend.Rmis.Domain.Entities.MiddleOffice
{
    public enum ReportType
    {
        KajianRisiko = 1,
        ResumeHo = 2,
        ProfilKln = 3
    }

    public enum MaturityTipe
    {
        Contractual = 1,
        Behavioral = 2
    }

    public enum BatchStatus
    {
        Draft = 1,
        Pending = 2,
        Approved = 3,
        Rejected = 4
    }
}
