using Helpdesk.Application.Common;
using Helpdesk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Helpdesk.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<ApprovalRequest> Approvals => Set<ApprovalRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        => builder.Properties<DateTime>().HaveConversion<UtcConverter>();

    private class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
