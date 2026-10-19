using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<HostProperty> Properties => Set<HostProperty>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<HostApplication> HostApplications => Set<HostApplication>();
    public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
    public DbSet<GuestCheckIn> GuestCheckIns => Set<GuestCheckIn>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // SQL Server rejects "multiple cascade paths" (e.g. User -> Property -> Booking AND User -> Booking).
        // Default every domain FK to Restrict, then opt in to Cascade only where a child is owned by its parent.
        var domainNamespace = typeof(HostProperty).Namespace;
        foreach (var fk in b.Model.GetEntityTypes()
                     .Where(e => e.ClrType.Namespace == domainNamespace)
                     .SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // One-to-Many
        b.Entity<HostProperty>()
            .HasOne(p => p.Host).WithMany(u => u.HostedProperties)
            .HasForeignKey(p => p.HostId);

        b.Entity<Booking>()
            .HasOne(x => x.Guest).WithMany(u => u.Bookings)
            .HasForeignKey(x => x.GuestId);

        b.Entity<Booking>()
            .HasOne(x => x.Property).WithMany(p => p.Bookings)
            .HasForeignKey(x => x.PropertyId);

        b.Entity<Review>()
            .HasOne(r => r.Reviewer).WithMany(u => u.Reviews)
            .HasForeignKey(r => r.ReviewerId);

        b.Entity<Review>()
            .HasOne(r => r.Property).WithMany(p => p.Reviews)
            .HasForeignKey(r => r.PropertyId);

        b.Entity<PropertyImage>()
            .HasOne(i => i.Property).WithMany(p => p.Images)
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Message>()
            .HasOne(m => m.Sender).WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId);

        b.Entity<Message>()
            .HasOne(m => m.Receiver).WithMany(u => u.ReceivedMessages)
            .HasForeignKey(m => m.ReceiverId);

        b.Entity<HostApplication>()
            .HasOne(a => a.ApplicationUser).WithMany(u => u.HostApplications)
            .HasForeignKey(a => a.ApplicationUserId);

        b.Entity<HostApplication>()
            .HasOne(a => a.Property).WithMany()
            .HasForeignKey(a => a.PropertyId);

        b.Entity<Notification>()
            .HasOne(n => n.User).WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ActivityLog>()
            .HasOne(l => l.User).WithMany()
            .HasForeignKey(l => l.UserId);   // Restrict: audit rows must outlive nothing silently

        b.Entity<WishlistItem>()
            .HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId);
        b.Entity<WishlistItem>()
            .HasOne(w => w.Property).WithMany().HasForeignKey(w => w.PropertyId);
        b.Entity<WishlistItem>()
            .HasIndex(w => new { w.UserId, w.PropertyId }).IsUnique();

        // One-to-One 
        b.Entity<Payment>()
            .HasOne(p => p.Booking).WithOne(x => x.Payment)
            .HasForeignKey<Payment>(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<GuestCheckIn>()
            .HasOne(g => g.Booking).WithOne(x => x.GuestCheckIn)
            .HasForeignKey<GuestCheckIn>(g => g.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CheckInProcess>()
            .HasOne(c => c.Property).WithOne(p => p.CheckInProcess)
            .HasForeignKey<CheckInProcess>(c => c.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<GuestCheckIn>()
            .HasOne(g => g.CheckInProcess).WithMany()
            .HasForeignKey(g => g.CheckInProcessId);

        // One-to-Many (child owned by check-in)
        b.Entity<GuestDocument>()
            .HasOne(d => d.GuestCheckIn).WithMany(g => g.Documents)
            .HasForeignKey(d => d.GuestCheckInId)
            .OnDelete(DeleteBehavior.Cascade);

        // Many-to-Many: HostProperty <-> Amenity (EF creates the join table)
        b.Entity<HostProperty>()
            .HasMany(p => p.Amenities).WithMany(a => a.Properties);

        // Decimal precision (18,2) as per the ERD
        b.Entity<HostProperty>().Property(p => p.PricePerNight).HasPrecision(18, 2);
        b.Entity<HostProperty>().Property(p => p.CleaningFee).HasPrecision(18, 2);
        b.Entity<Booking>().Property(x => x.TotalPrice).HasPrecision(18, 2);
        b.Entity<Booking>().Property(x => x.CleaningFee).HasPrecision(18, 2);
        b.Entity<Booking>().Property(x => x.ServiceFee).HasPrecision(18, 2);
        b.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);

        // Store enums as readable strings in SQL
        b.Entity<Booking>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Entity<Payment>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Entity<HostApplication>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Entity<ActivityLog>().Property(x => x.ActivityType).HasConversion<string>().HasMaxLength(40);
        b.Entity<Notification>().Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
        b.Entity<GuestCheckIn>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Entity<GuestDocument>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

        // Query-speed indexes for the pages we'll build
        b.Entity<HostProperty>().HasIndex(p => new { p.IsActive, p.City });
        b.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });
        b.Entity<Message>().HasIndex(m => new { m.ReceiverId, m.IsRead });

        // Seed reference data: amenities (no programmatic step needed)
        b.Entity<Amenity>().HasData(
            new Amenity { Id = 1, Name = "Wifi", IconClass = "bi-wifi" },
            new Amenity { Id = 2, Name = "Kitchen", IconClass = "bi-cup-hot" },
            new Amenity { Id = 3, Name = "Washer", IconClass = "bi-droplet" },
            new Amenity { Id = 4, Name = "Dryer", IconClass = "bi-wind" },
            new Amenity { Id = 5, Name = "Air Conditioning", IconClass = "bi-snow" },
            new Amenity { Id = 6, Name = "Heating", IconClass = "bi-thermometer-sun" },
            new Amenity { Id = 7, Name = "Pool", IconClass = "bi-water" },
            new Amenity { Id = 8, Name = "Free Parking", IconClass = "bi-p-circle" });
    }
}