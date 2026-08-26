namespace middle_office_backend.Rmis.Domain.Entities.MiddleOffice
{
    // PRD §9 — thresholds/formulas must be admin-editable without redeploy, never hardcoded.
    public class ConfigParameter
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty; // e.g. "TrafficLight.KasRupiah"
        public string Key { get; set; } = string.Empty;      // e.g. "DarkGreenMin"
        public string Value { get; set; } = string.Empty;    // stored as string, parsed by consumer
        public string Description { get; set; } = string.Empty;
    }
}
