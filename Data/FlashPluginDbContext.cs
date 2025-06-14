using BTCPayServer.Plugins.Flash.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BTCPayServer.Plugins.Flash.Data
{
    public class FlashPluginDbContext : DbContext
    {
        public FlashPluginDbContext(DbContextOptions<FlashPluginDbContext> options)
            : base(options)
        {
        }

        public DbSet<FlashPayout> FlashPayouts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<FlashPayout>(entity =>
            {
                entity.ToTable("FlashPayouts");
                
                entity.HasKey(p => p.Id);
                
                entity.HasIndex(p => new { p.StoreId, p.CreatedAt })
                    .HasDatabaseName("IX_FlashPayouts_StoreId_CreatedAt");
                
                entity.HasIndex(p => p.Status)
                    .HasDatabaseName("IX_FlashPayouts_Status")
                    .HasFilter("[Status] IN (0, 1)"); // Pending, Processing
                
                entity.HasIndex(p => p.BoltcardId)
                    .HasDatabaseName("IX_FlashPayouts_BoltcardId")
                    .HasFilter("[BoltcardId] IS NOT NULL");
                
                entity.HasIndex(p => p.PaymentHash)
                    .HasDatabaseName("IX_FlashPayouts_PaymentHash");
                
                entity.HasIndex(p => p.PullPaymentId)
                    .HasDatabaseName("IX_FlashPayouts_PullPaymentId");

                entity.Property(p => p.Id)
                    .HasMaxLength(50);
                
                entity.Property(p => p.StoreId)
                    .IsRequired()
                    .HasMaxLength(50);
                
                entity.Property(p => p.PullPaymentId)
                    .IsRequired()
                    .HasMaxLength(50);
                
                entity.Property(p => p.BoltcardId)
                    .HasMaxLength(100);
                
                entity.Property(p => p.PaymentHash)
                    .HasMaxLength(100);
                
                entity.Property(p => p.LightningInvoice)
                    .HasMaxLength(2000);
                
                entity.Property(p => p.ErrorMessage)
                    .HasMaxLength(500);
                
                entity.Property(p => p.Memo)
                    .HasMaxLength(200);
                
                entity.Property(p => p.DestinationAddress)
                    .HasMaxLength(200);
            });
        }
    }

    public class FlashPluginDbContextFactory : IDesignTimeDbContextFactory<FlashPluginDbContext>
    {
        public FlashPluginDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FlashPluginDbContext>();
            // In production, this will be configured by BTCPay Server
            // For design time, use in-memory database
            optionsBuilder.UseInMemoryDatabase("FlashPlugin");
            return new FlashPluginDbContext(optionsBuilder.Options);
        }
    }
}