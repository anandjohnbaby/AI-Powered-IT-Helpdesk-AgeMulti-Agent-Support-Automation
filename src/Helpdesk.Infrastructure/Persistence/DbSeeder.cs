using Helpdesk.Application.Common;
using Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence;

/// Creates the database and the demo PORTAL accounts (independent of Keycloak passwords).
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IAppPasswordHasher hasher)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.AppUsers.AnyAsync()) return;

        var seed = new List<(AppUser User, string Password)>
        {
            (new AppUser { Username = "alice", FullName = "Alice Sharma", Role = Roles.Employee, DirectoryAccount = "alice" }, "AlicePortal@1"),
            (new AppUser { Username = "bob", FullName = "Bob Mehta", Role = Roles.Employee, DirectoryAccount = "bob" }, "BobPortal@1"),
            (new AppUser { Username = "itadmin", FullName = "IT Admin", Role = Roles.Approver, DirectoryAccount = null }, "ItAdmin@123"),
        };

        foreach (var (user, password) in seed)
        {
            user.PasswordHash = hasher.Hash(user, password);
            db.AppUsers.Add(user);
        }
        await db.SaveChangesAsync();
    }
}