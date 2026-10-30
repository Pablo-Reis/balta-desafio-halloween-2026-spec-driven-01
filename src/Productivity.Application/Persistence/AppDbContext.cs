using Microsoft.EntityFrameworkCore;
using Productivity.Domain;

namespace Productivity.Application.Persistence;

/// <summary>Contexto do banco. Somente classes *Service podem usá-lo (constituição).</summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Demand> Demands => Set<Demand>();
    public DbSet<DemandEvent> DemandEvents => Set<DemandEvent>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Demand>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Id).ValueGeneratedNever();
            b.Property(d => d.Title).HasMaxLength(Demand.MaxTitleLength).IsRequired();
            b.Property(d => d.Type).HasMaxLength(Demand.MaxTypeLength).IsRequired();
            b.Property(d => d.Description).HasMaxLength(Demand.MaxDescriptionLength);
            b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            b.HasMany(d => d.Events).WithOne().HasForeignKey(e => e.DemandId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(d => d.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(d => d.OrderedEvents);
            b.Ignore(d => d.LastEvent);
            b.HasIndex(d => d.CreatedAt);
            b.HasIndex(d => d.Status);
        });

        modelBuilder.Entity<DemandEvent>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(e => new { e.DemandId, e.Sequence });
        });

        modelBuilder.Entity<Note>(b =>
        {
            b.HasKey(n => n.Id);
            b.Property(n => n.Id).ValueGeneratedNever();
            b.Property(n => n.Text).HasMaxLength(Note.MaxTextLength).IsRequired();
            b.Property(n => n.Source).HasConversion<string>().HasMaxLength(20);
            b.HasOne<Demand>().WithMany().HasForeignKey(n => n.DemandId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(n => n.DemandId);
        });

        modelBuilder.Entity<AppSetting>(b =>
        {
            b.HasKey(s => s.Key);
            b.Property(s => s.Key).HasMaxLength(100);
        });
    }
}

