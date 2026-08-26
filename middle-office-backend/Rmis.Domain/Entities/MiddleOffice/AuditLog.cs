namespace middle_office_backend.Rmis.Domain.Entities.MiddleOffice
{
    public class AuditLog
    {
        public long Id { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Upload, Submit, Edit, Approve, Reject, Export, Login
        public string Entity { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
