namespace middle_office_backend.Rmis.Domain.Entities.Auth
{
    public static class RoleNames
    {
        public const string Uploader = "Uploader";
        public const string Approver = "Approver";
        public const string Viewer = "Viewer";
        public const string Admin = "Admin";
    }

    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }

    public class UserRole
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
    }
}
