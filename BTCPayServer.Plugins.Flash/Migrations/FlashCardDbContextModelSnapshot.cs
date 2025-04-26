using System;
using BTCPayServer.Plugins.Flash.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BTCPayServer.Plugins.Flash.Migrations
{
    [DbContext(typeof(FlashCardDbContext))]
    partial class FlashCardDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("BTCPayServer.Plugins.Flash")
                .HasAnnotation("ProductVersion", "8.0.6");

            modelBuilder.Entity("BTCPayServer.Plugins.Flash.Data.Models.CardRegistration", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<string>("CardName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("CardUID")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("FlashWalletId")
                        .HasColumnType("text");

                    b.Property<bool>("IsBlocked")
                        .HasColumnType("boolean");

                    b.Property<DateTimeOffset?>("LastUsedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("PullPaymentId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal?>("SpendingLimitPerTransaction")
                        .HasColumnType("numeric");

                    b.Property<string>("StoreId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.Property<int>("Version")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CardUID")
                        .IsUnique();

                    b.HasIndex("PullPaymentId");

                    b.ToTable("CardRegistrations");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.Flash.Data.Models.CardTransaction", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<decimal>("Amount")
                        .HasColumnType("numeric");

                    b.Property<string>("CardRegistrationId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<string>("InvoiceId")
                        .HasColumnType("text");

                    b.Property<string>("LocationId")
                        .HasColumnType("text");

                    b.Property<string>("MerchantId")
                        .HasColumnType("text");

                    b.Property<string>("PaymentHash")
                        .HasColumnType("text");

                    b.Property<string>("PayoutId")
                        .HasColumnType("text");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CardRegistrationId");

                    b.ToTable("CardTransactions");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.Flash.Data.Models.CardTransaction", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.Flash.Data.Models.CardRegistration", "CardRegistration")
                        .WithMany()
                        .HasForeignKey("CardRegistrationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}