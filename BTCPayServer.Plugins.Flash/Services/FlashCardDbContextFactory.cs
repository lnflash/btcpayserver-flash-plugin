#nullable enable
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.Flash.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;

namespace BTCPayServer.Plugins.Flash.Services
{
    // For design-time migrations
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FlashCardDbContext>
    {
        public FlashCardDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<FlashCardDbContext>();
            
            // Using Postgres for design-time
            builder.UseNpgsql("User ID=postgres;Host=127.0.0.1;Port=39372;Database=designtimebtcpay");
            
            return new FlashCardDbContext(builder.Options, true);
        }
    }
    
    // For runtime
    public class FlashCardDbContextFactory : BaseDbContextFactory<FlashCardDbContext>
    {
        public FlashCardDbContextFactory(IOptions<DatabaseOptions> options) 
            : base(options, "BTCPayServer.Plugins.Flash")
        {
        }
        
        public override FlashCardDbContext CreateContext(Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        {
            var builder = new DbContextOptionsBuilder<FlashCardDbContext>();
            ConfigureBuilder(builder, npgsqlOptionsAction);
            return new FlashCardDbContext(builder.Options);
        }
    }
}