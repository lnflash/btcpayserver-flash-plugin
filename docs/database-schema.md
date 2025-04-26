# Database Schema

This document outlines the database schema for the Flash BTCPayServer plugin, detailing the tables, relationships, and data models.

## Overview

The Flash BTCPayServer plugin uses Entity Framework Core to manage its database schema. The schema is defined in the `FlashCardDbContext` class and its associated model classes.

The database uses the schema name `BTCPayServer.Plugins.Flash` to separate its tables from other BTCPayServer tables.

## Database Diagram

```
┌─────────────────────────┐          ┌───────────────────────────┐
│  CardRegistration       │          │  CardTransaction          │
├─────────────────────────┤          ├───────────────────────────┤
│ Id (PK)                 │◄─────────┤ CardRegistrationId (FK)   │
│ CardUID                 │          │ Id (PK)                   │
│ PullPaymentId           │          │ PayoutId                  │
│ StoreId                 │          │ Amount                    │
│ UserId                  │          │ Currency                  │
│ CardName                │          │ Type                      │
│ Version                 │          │ Status                    │
│ CreatedAt               │          │ InvoiceId                 │
│ LastUsedAt              │          │ PaymentHash               │
│ IsBlocked               │          │ CreatedAt                 │
│ FlashWalletId           │          │ CompletedAt               │
│ SpendingLimitPerTx      │          │ MerchantId                │
└─────────────────────────┘          │ LocationId                │
                                    │ Description               │
                                    └───────────────────────────┘
```

## Tables

### CardRegistration

The `CardRegistration` table stores information about registered NFC cards.

| Column | Type | Description | Constraints |
|--------|------|-------------|------------|
| Id | string | Primary key | PK, auto-generated |
| CardUID | string | NFC card unique identifier | Required, Unique |
| PullPaymentId | string | ID of associated Pull Payment | Required |
| StoreId | string | ID of the store | Required |
| UserId | string | ID of the user (if assigned) | Nullable |
| CardName | string | Friendly name for the card | Required |
| Version | int | Card version number | Required, Default: 1 |
| CreatedAt | DateTimeOffset | When the card was registered | Required |
| LastUsedAt | DateTimeOffset | When the card was last used | Nullable |
| IsBlocked | bool | Whether the card is blocked | Required, Default: false |
| FlashWalletId | string | Associated Flash wallet ID | Nullable |
| SpendingLimitPerTransaction | decimal | Per-transaction limit | Nullable |

#### Indexes
- PK_CardRegistrations (Id)
- IX_CardRegistrations_CardUID (CardUID), Unique
- IX_CardRegistrations_PullPaymentId (PullPaymentId)

### CardTransaction

The `CardTransaction` table stores information about card transactions.

| Column | Type | Description | Constraints |
|--------|------|-------------|------------|
| Id | string | Primary key | PK, auto-generated |
| CardRegistrationId | string | ID of the card registration | Required, FK |
| PayoutId | string | ID of associated payout | Nullable |
| Amount | decimal | Transaction amount | Required |
| Currency | string | Transaction currency | Required, Default: "SATS" |
| Type | int | Transaction type enum | Required |
| Status | int | Transaction status enum | Required |
| InvoiceId | string | Associated invoice ID | Nullable |
| PaymentHash | string | Lightning payment hash | Nullable |
| CreatedAt | DateTimeOffset | When the transaction was created | Required |
| CompletedAt | DateTimeOffset | When the transaction was completed | Nullable |
| MerchantId | string | Merchant identifier | Nullable |
| LocationId | string | Location identifier | Nullable |
| Description | string | Transaction description | Nullable |

#### Indexes
- PK_CardTransactions (Id)
- IX_CardTransactions_CardRegistrationId (CardRegistrationId)

#### Foreign Keys
- FK_CardTransactions_CardRegistrations_CardRegistrationId: References CardRegistration (Id)

## Enums

### CardTransactionType

Defines the type of card transaction:

| Value | Name | Description |
|-------|------|-------------|
| 0 | Payment | Card payment transaction |
| 1 | TopUp | Card top-up transaction |
| 2 | Refund | Refund to card |

### CardTransactionStatus

Defines the status of a card transaction:

| Value | Name | Description |
|-------|------|-------------|
| 0 | Pending | Transaction is pending |
| 1 | Completed | Transaction is completed |
| 2 | Failed | Transaction failed |
| 3 | Cancelled | Transaction was cancelled |

## Database Context

The database context is defined in the `FlashCardDbContext` class:

```csharp
public class FlashCardDbContext : DbContext
{
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
```

## Model Classes

### CardRegistration

```csharp
public class CardRegistration
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    
    [Required]
    public string CardUID { get; set; } = null!;
    
    [Required]
    public string PullPaymentId { get; set; } = null!;
    
    [Required]
    public string StoreId { get; set; } = null!;
    
    public string? UserId { get; set; }
    
    [Required]
    public string CardName { get; set; } = "Flash Card";
    
    [Required]
    public int Version { get; set; } = 1;
    
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? LastUsedAt { get; set; }
    
    public bool IsBlocked { get; set; } = false;
    
    public string? FlashWalletId { get; set; }
    
    public decimal? SpendingLimitPerTransaction { get; set; }
}
```

### CardTransaction

```csharp
public class CardTransaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    
    [Required]
    public string CardRegistrationId { get; set; } = null!;
    
    [ForeignKey("CardRegistrationId")]
    public CardRegistration? CardRegistration { get; set; }
    
    public string? PayoutId { get; set; }
    
    [Required]
    public decimal Amount { get; set; }
    
    [Required]
    public string Currency { get; set; } = "SATS";
    
    [Required]
    public CardTransactionType Type { get; set; }
    
    [Required]
    public CardTransactionStatus Status { get; set; }
    
    public string? InvoiceId { get; set; }
    
    public string? PaymentHash { get; set; }
    
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? CompletedAt { get; set; }
    
    public string? MerchantId { get; set; }
    
    public string? LocationId { get; set; }
    
    public string? Description { get; set; }
}
```

## Migrations

Migrations are handled using Entity Framework Core's migration system. The initial migration (`20250426000000_Init.cs`) creates the database schema:

```csharp
public partial class Init : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "BTCPayServer.Plugins.Flash");

        migrationBuilder.CreateTable(
            name: "CardRegistrations",
            schema: "BTCPayServer.Plugins.Flash",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                CardUID = table.Column<string>(nullable: false),
                PullPaymentId = table.Column<string>(nullable: false),
                StoreId = table.Column<string>(nullable: false),
                UserId = table.Column<string>(nullable: true),
                CardName = table.Column<string>(nullable: false),
                Version = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(nullable: true),
                IsBlocked = table.Column<bool>(nullable: false),
                FlashWalletId = table.Column<string>(nullable: true),
                SpendingLimitPerTransaction = table.Column<decimal>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CardRegistrations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CardTransactions",
            schema: "BTCPayServer.Plugins.Flash",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                CardRegistrationId = table.Column<string>(nullable: false),
                PayoutId = table.Column<string>(nullable: true),
                Amount = table.Column<decimal>(nullable: false),
                Currency = table.Column<string>(nullable: false),
                Type = table.Column<int>(nullable: false),
                Status = table.Column<int>(nullable: false),
                InvoiceId = table.Column<string>(nullable: true),
                PaymentHash = table.Column<string>(nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(nullable: true),
                MerchantId = table.Column<string>(nullable: true),
                LocationId = table.Column<string>(nullable: true),
                Description = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CardTransactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_CardTransactions_CardRegistrations_CardRegistrationId",
                    column: x => x.CardRegistrationId,
                    principalSchema: "BTCPayServer.Plugins.Flash",
                    principalTable: "CardRegistrations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CardRegistrations_CardUID",
            schema: "BTCPayServer.Plugins.Flash",
            table: "CardRegistrations",
            column: "CardUID",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CardRegistrations_PullPaymentId",
            schema: "BTCPayServer.Plugins.Flash",
            table: "CardRegistrations",
            column: "PullPaymentId");

        migrationBuilder.CreateIndex(
            name: "IX_CardTransactions_CardRegistrationId",
            schema: "BTCPayServer.Plugins.Flash",
            table: "CardTransactions",
            column: "CardRegistrationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CardTransactions",
            schema: "BTCPayServer.Plugins.Flash");

        migrationBuilder.DropTable(
            name: "CardRegistrations",
            schema: "BTCPayServer.Plugins.Flash");
    }
}
```

## Integration with BTCPayServer

The database integrates with BTCPayServer through:

1. **Pull Payment References**: CardRegistration references BTCPayServer's Pull Payments
2. **Store References**: CardRegistration references BTCPayServer's Stores
3. **User References**: CardRegistration can reference BTCPayServer's Users
4. **Invoice References**: CardTransaction can reference BTCPayServer's Invoices
5. **Payout References**: CardTransaction can reference BTCPayServer's Payouts

## Database Access

The database is accessed through a factory pattern to ensure proper configuration:

```csharp
public class FlashCardDbContextFactory : BaseDbContextFactory<FlashCardDbContext>
{
    public FlashCardDbContextFactory(IOptions<DatabaseOptions> options) 
        : base(options, "BTCPayServer.Plugins.Flash")
    {
    }
    
    public override FlashCardDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<FlashCardDbContext>();
        ConfigureBuilder(builder);
        return new FlashCardDbContext(builder.Options);
    }
}
```

This factory is injected into services that need database access:

```csharp
public class FlashCardRegistrationService
{
    private readonly FlashCardDbContextFactory _dbContextFactory;
    
    public FlashCardRegistrationService(FlashCardDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }
    
    public async Task<CardRegistration> RegisterCard(...)
    {
        await using var context = _dbContextFactory.CreateContext();
        // Database operations...
    }
}
```

## Database Performance Considerations

To ensure optimal database performance:

1. **Indexes**:
   - CardUID is indexed for fast lookup by UID
   - PullPaymentId is indexed for efficient joins
   - CardRegistrationId is indexed for fast transaction lookup

2. **Query Optimization**:
   - Eager loading of related entities when appropriate
   - Projection queries to select only needed columns
   - Paging for large result sets

3. **Concurrency Control**:
   - Proper transaction handling for concurrent operations
   - Optimistic concurrency where appropriate

## Future Schema Evolution

As the plugin evolves, the schema might need to be updated. Future migrations should consider:

1. **Backward Compatibility**: Ensure existing data continues to work
2. **Performance Impact**: Evaluate impact of schema changes on performance
3. **Migration Strategy**: Plan for safe data migration
4. **Downtime Requirements**: Determine if schema changes require downtime

## Conclusion

The database schema for the Flash BTCPayServer plugin provides a solid foundation for storing and managing NFC card data and transactions. The schema design emphasizes data integrity, performance, and integration with the BTCPayServer ecosystem while maintaining proper separation of concerns.