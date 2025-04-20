using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace User.Models
{
    public enum UserRole
    {
        User,
        Admin,
        SuperAdmin
    }

    public class UserModel
    {
        public UserModel()
        {
            id = string.Empty;
            userName = string.Empty;
            email = string.Empty;
            password = string.Empty;
            Role = UserRole.User;
            validated = false;
        }

        public required string id { get; set; }
        public required string userName { get; set; }
        public required string email { get; set; }
        public required string password { get; set; }
        public UserRole Role { get; set; } = UserRole.User; // Par défaut, un nouvel utilisateur est un utilisateur normal
        public bool validated { get; set; }
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // Informations supplémentaires du profil
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? ProfilePicture { get; set; }
    }
}