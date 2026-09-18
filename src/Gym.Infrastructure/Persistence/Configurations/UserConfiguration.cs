using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// Applied by <c>AppDbContext.OnModelCreating</c> after <c>base.OnModelCreating</c>, so
/// <see cref="ToTable"/> here overrides the "AspNetUsers" name the Identity base class sets,
/// instead of being overwritten by it.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Required only stops NULL; an all-spaces name would still pass. The check constraint is
        // the database's own copy of that rule, so a write that skips the application code
        // (a script, a future handler that forgets to trim) still cannot store a blank name.
        builder.ToTable("users", table => table.HasCheckConstraint(
            "ck_users_full_name_not_blank",
            "btrim(full_name) <> ''"));

        builder.Property(user => user.FullName)
            .IsRequired()
            .HasMaxLength(200);
    }
}
