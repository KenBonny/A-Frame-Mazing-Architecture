using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MoreThanCode.AFrameExample.Database;

public class DogWalkingContext(DbContextOptions<DogWalkingContext> options) : DbContext(options)
{
    public DbSet<Dog> Dogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dog>().Property(d => d.Birthday).HasConversion<DateOnlyConverter>();
        base.OnModelCreating(modelBuilder);
    }
}

internal class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
    d => d.ToDateTime(TimeOnly.MinValue),
    d => DateOnly.FromDateTime(d));