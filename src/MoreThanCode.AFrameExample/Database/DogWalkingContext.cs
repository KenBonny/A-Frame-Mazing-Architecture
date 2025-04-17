using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MoreThanCode.AFrameExample.Walk;

namespace MoreThanCode.AFrameExample.Database;

public class DogWalkingContext(DbContextOptions<DogWalkingContext> options) : DbContext(options)
{
    public DbSet<Dog> Dogs { get; set; }

    public DbSet<WalkWithDogs> WalksWithDogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dog>().Property(d => d.Birthday).HasConversion<DateOnlyConverter>();
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WalkWithDogs>(entity =>
        {
            entity.ToTable("Walks");
            // Configure the many-to-many relationship with Dogs
            entity.HasMany(w => w.Dogs)
                .WithMany(d => d.Walks)
                .UsingEntity(
                    "WalkDogs",
                    joint =>
                    {
                        joint.Property("WalksId").HasColumnName("WalkId");
                        joint.Property("DogsId").HasColumnName("DogId");
                    });

            // Configure the one-to-many relationship with Coordinates
            entity.HasMany(w => w.Path)
                .WithOne(c => c.Walk)
                .HasForeignKey(c => c.WalkId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Path as a collection that should be ordered by the Order property
            entity.Navigation(w => w.Path)
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();
        });

        modelBuilder.Entity<CoordinateEntity>(entity =>
        {
            entity.ToTable("WalkCoordinates");
            entity.HasKey(c => c.Id);

            // Ensure coordinates are retrieved in the correct order
            entity.HasIndex(c => new { c.WalkId, c.SequenceOrder });
        });

    }
}

internal class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
    d => d.ToDateTime(TimeOnly.MinValue),
    d => DateOnly.FromDateTime(d));