#nullable enable
using System;
using Microsoft.EntityFrameworkCore;
using BTCPayServer.Plugins.Flash.Data.Models;

namespace BTCPayServer.Plugins.Flash.Data
{
    public class FlashCardDbContext : DbContext
    {
        private readonly bool _designTime;

        public FlashCardDbContext(DbContextOptions<FlashCardDbContext> options, bool designTime = false)
            : base(options)
        {
            _designTime = designTime;
        }

        public DbSet<CardRegistration> CardRegistrations { get; set; }
        public DbSet<CardTransaction> CardTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.Flash");
            
            // Configure relationships and indexes
            modelBuilder.Entity<CardRegistration>()
                .HasIndex(c => c.CardUID)
                .IsUnique();
                
            modelBuilder.Entity<CardRegistration>()
                .HasIndex(c => c.PullPaymentId);
                
            modelBuilder.Entity<CardTransaction>()
                .HasIndex(t => t.CardRegistrationId);
        }
    }
}