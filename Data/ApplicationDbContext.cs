using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenAcademy.Entities;
using OpenAcademy.Entities.Administration;
using OpenAcademy.Entities.Courses;
using OpenAcademy.Entities.Moderation;
using OpenAcademy.Entities.Tokens;
using OpenAcademy.Entities.Users;
using OpenAcademy.Entities.Users.RoleProfiles;
using Category = OpenAcademy.Entities.Courses.Category;

namespace OpenAcademy.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public DbSet<StudentProfile> StudentProfiles { get; set; }
    public DbSet<InstructorProfile> InstructorProfiles { get; set; }
    public DbSet<ModeratorProfile> ModeratorProfiles { get; set; }
    public DbSet<AdminProfile> AdminProfiles { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<LessonAsset> LessonAssets { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    // Convention used below:
    //   entity classes  -> lengths, [Required], [Range]   (data annotations, as the team already does)
    //   this method     -> keys, relationships, delete behaviour, indexes, enum conversions,
    //                      DB defaults, check constraints
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity's own tables first

        ConfigureUsers(builder);
        ConfigureCourses(builder);
        ConfigureModeration(builder);
        ConfigureAdministration(builder);
    }

    // ---------------------------------------------------------------- users & auth

    private static void ConfigureUsers(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.Id).ValueGeneratedNever(); // assigned in the User constructor (Guid.NewGuid())
            b.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            b.Property(u => u.Status).HasConversion<string>().HasMaxLength(32);
        });

        // Role profiles share their primary key with the user (1:1, no extra surrogate key).
        builder.Entity<StudentProfile>(b =>
        {
            b.HasKey(p => p.UserId);
            b.HasOne(p => p.User).WithOne(u => u.StudentProfile)
                .HasForeignKey<StudentProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InstructorProfile>(b =>
        {
            b.HasKey(p => p.UserId);
            b.HasOne(p => p.User).WithOne(u => u.InstructorProfile)
                .HasForeignKey<InstructorProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModeratorProfile>(b =>
        {
            b.HasKey(p => p.UserId);
            b.HasOne(p => p.User).WithOne(u => u.ModeratorProfile)
                .HasForeignKey<ModeratorProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        });

        builder.Entity<AdminProfile>(b =>
        {
            b.HasKey(p => p.UserId);
            b.HasOne(p => p.User).WithOne(u => u.AdminProfile)
                .HasForeignKey<AdminProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.Property(t => t.Id).ValueGeneratedNever();

            b.HasOne(t => t.User).WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade); // tokens die with the account

            b.HasIndex(t => t.TokenHash).IsUnique(); // lookup on every refresh
            b.HasIndex(t => new { t.UserId, t.FamilyId }); // revoke a whole family / all of a user's tokens
            b.HasIndex(t => t.ExpiresAt); // cleanup job

            b.Ignore(t => t.IsExpired);
            b.Ignore(t => t.IsRevoked);
        });
    }

    // ---------------------------------------------------------------- courses

    private static void ConfigureCourses(ModelBuilder builder)
    {
        builder.Entity<Category>(b =>
        {
            b.HasIndex(c => c.Slug).IsUnique();
            b.HasIndex(c => c.Name).IsUnique();
        });

        builder.Entity<Course>(b =>
        {
            b.Property(c => c.Id).ValueGeneratedNever();
            b.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);

            // An instructor profile that still owns courses cannot be deleted.
            b.HasOne(c => c.InstructorProfile).WithMany(p => p.Courses)
                .HasForeignKey(c => c.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Many-to-many: EF creates and manages the join table itself (CategoryCourse).
            // There is no entity class for it.
            b.HasMany(c => c.Categories).WithMany(cat => cat.Courses);
        });

        builder.Entity<Module>(b =>
        {
            b.Property(m => m.Id).ValueGeneratedNever();

            b.HasOne(m => m.Course).WithMany(c => c.Modules)
                .HasForeignKey(m => m.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deliberately NOT unique: swapping two positions would violate a unique index mid-update.
            b.HasIndex(m => new { m.CourseId, m.Order });

            b.ToTable(t => t.HasCheckConstraint("CK_Modules_Order_NonNegative", "\"Order\" >= 0"));
        });

        builder.Entity<Lesson>(b =>
        {
            b.Property(l => l.Id).ValueGeneratedNever();

            b.HasOne(l => l.Module).WithMany(m => m.Lessons)
                .HasForeignKey(l => l.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(l => new { l.ModuleId, l.Order });

            b.ToTable(t => t.HasCheckConstraint("CK_Lessons_Order_NonNegative", "\"Order\" >= 0"));
        });

        builder.Entity<LessonAsset>(b =>
        {
            b.Property(a => a.Id).ValueGeneratedNever();
            b.Property(a => a.Type).HasConversion<string>().HasMaxLength(32);
            b.Property(a => a.Status).HasConversion<string>().HasMaxLength(32);
            b.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

            b.HasOne(a => a.Lesson).WithMany(l => l.LessonAssets)
                .HasForeignKey(a => a.LessonId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(a => new { a.LessonId, a.Order });

            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_LessonAssets_Order_NonNegative", "\"Order\" >= 0");
                t.HasCheckConstraint("CK_LessonAssets_SizeBytes_NonNegative", "\"SizeBytes\" >= 0");
            });
        });

        builder.Entity<Enrollment>(b =>
        {
            // One row per (student, course): withdrawing and re-enrolling flips Status on the same row.
            b.HasKey(e => new { e.StudentId, e.CourseId });

            b.Property(e => e.EnrollmentDate).HasDefaultValueSql("now()");
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);

            b.HasOne(e => e.StudentProfile).WithMany(p => p.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // A course with enrolled students cannot be hard-deleted. The service must catch this and return 409.
            b.HasOne(e => e.Course).WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(e => e.CourseId); // "who is enrolled in this course" / enrollment counts
        });
    }

    // ---------------------------------------------------------------- moderation

    private static void ConfigureModeration(ModelBuilder builder)
    {
        builder.Entity<Report>(b =>
        {
            // Id is a long -> identity column by convention.
            b.Property(r => r.Reason).HasConversion<string>().HasMaxLength(32);
            b.Property(r => r.ClosureReason).HasConversion<string>().HasMaxLength(32);

            // Report history must outlive any of the people involved, so nothing here cascades.
            b.HasOne(r => r.Issuer).WithMany(u => u.IssuedReports)
                .HasForeignKey(r => r.IssuerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.AssignedTo).WithMany(m => m.AssignedReports)
                .HasForeignKey(r => r.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.ElevatedTo).WithMany(a => a.ElevatedReports)
                .HasForeignKey(r => r.ElevatedToId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.ClosedBy).WithMany(u => u.ClosedReports)
                .HasForeignKey(r => r.ClosedById).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(r => new { r.ContentType, r.ContentId }); // "all reports about this lesson"
            b.HasIndex(r => r.ClosedAt); // open-queue queries

            b.Ignore(r => r.IsClosed);

            // Closing sets the three closure columns together, or none of them.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_Reports_ClosureColumnsTogether",
                "(\"ClosedAt\" IS NULL AND \"ClosedById\" IS NULL AND \"ClosureReason\" IS NULL) OR " +
                "(\"ClosedAt\" IS NOT NULL AND \"ClosedById\" IS NOT NULL AND \"ClosureReason\" IS NOT NULL)"));
        });
    }

    // ---------------------------------------------------------------- administration

    private static void ConfigureAdministration(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(b =>
        {
            // Id is a long -> identity column by convention.
            b.Property(a => a.Timestamp).HasDefaultValueSql("now()");

            b.HasOne(a => a.Actor).WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.ActorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(a => a.Timestamp);
            b.HasIndex(a => new { a.TargetType, a.TargetId });
        });
    }

    // ---------------------------------------------------------------- timestamps

    // SaveChanges() calls SaveChanges(true) and SaveChangesAsync(ct) calls SaveChangesAsync(true, ct),
    // so overriding these two covers every entry point.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Note: ExecuteUpdate/ExecuteDelete bypass this, so UpdatedAt is not touched by bulk updates.
    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}