using Gym.Application.Members;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>members</c> table. The rules live in <see cref="Member"/>; the database repeats the
/// ones it can check, so a bug in the application cannot break them.
/// </summary>
public sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    /// <summary>E.164 allows at most 15 digits after the plus sign.</summary>
    private const int PhoneMaxLength = 16;

    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members", table =>
        {
            table.HasCheckConstraint("ck_members_full_name_not_blank", "btrim(full_name) <> ''");

            // Only normalized numbers may be stored; anything else would slip past the unique
            // index as a "different" string.
            table.HasCheckConstraint("ck_members_phone_number_e164", "phone_number ~ '^\\+[0-9]{7,15}$'");
        });

        builder.Property(member => member.FullName).HasMaxLength(Member.FullNameMaxLength).IsRequired();
        builder.Property(member => member.NormalizedFullName).HasMaxLength(Member.FullNameMaxLength).IsRequired();
        builder.Property(member => member.PhoneNumber).HasMaxLength(PhoneMaxLength).IsRequired();
        builder.Property(member => member.Notes).HasMaxLength(Member.NotesMaxLength);

        // BUSINESS_RULES.md §2: unique across all members, inactive ones included, so no filter.
        // Named explicitly because the handlers recognise a violation of it by name.
        builder.HasIndex(member => member.PhoneNumber)
            .IsUnique()
            .HasDatabaseName(MemberConstraints.UniquePhone);

        // Name search (task 2.2) reads this column.
        builder.HasIndex(member => member.NormalizedFullName);

        builder.Property(member => member.Version).IsRowVersion();
    }
}
