using CoreBankingTest.CORE.Common;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using CoreBankingTest.DAL.Persistence.Configurations;
using CoreBankingTest.DAL.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;

namespace CoreBankingTest.DAL.Data
{
    public class BankingDbContext : DbContext
    {
        public BankingDbContext(DbContextOptions<BankingDbContext> options)
            : base(options) { }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Transaction> Transactions => Set<Transaction>();


        public DbSet<OutBoxMessage> OutboxMessages { get; set; } = null!;
        public DbSet<DomainEvent> DomainEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)


        {
            modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
            base.OnModelCreating(modelBuilder);

            

            modelBuilder.Ignore<DomainEvent>();
            modelBuilder.Ignore<IDomainEvent>();

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(c => c.CustomerId);
                entity.Property(c => c.CustomerId)
                    .HasConversion(customerId => customerId.Value,
                                value => CustomerId.Create(value));
                
                entity.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(c => c.LastName).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Email).IsRequired().HasMaxLength(255);
                entity.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(c => c.DateOfBirth).IsRequired();
                entity.HasIndex(c => c.Email).IsUnique();
               
                // Customer has many Accounts
                entity.HasMany(c => c.Accounts)
                    .WithOne(a => a.Customer)
                    .HasForeignKey(a => a.CustomerId);
            });

            // Account configuration
            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasKey(a => a.AccountId);
                entity.Property(c => c.AccountId)
                    .HasConversion(AccountId => AccountId.Value,
                                value => AccountId.Create(value));

                // Configure AccountNumber as owned type (Value Object)
                entity.Property(a => a.AccountNumber)
                        .HasConversion(
                         accountNumber => accountNumber.Value,
                         value => AccountNumber.Create(value))
                        .HasColumnName("AccountNumber")
                        .HasMaxLength(10).IsRequired();
                

                // Configure Money as owned type (Value Object)
                entity.OwnsOne(a => a.Balance, money =>
                {
                    money.Property(m => m.Amount)
                        .HasColumnName("Amount")
                        .HasPrecision(18, 2);
                    money.Property(m => m.Currency)
                        .HasColumnName("Currency")
                        .HasMaxLength(3)
                        .HasDefaultValue("NGN");
                });

                entity.Property(a => a.AccountType)
                    .HasConversion<string>()
                    .IsRequired();

                // Account has many Transactions
                entity.HasMany(a => a.Transactions)
                    .WithOne(t => t.Account)
                    .HasForeignKey(t => t.AccountId);

                // Ensure we don't accidentally load all transactions
                entity.Navigation(a => a.Transactions).AutoInclude(false);
            });

            // Transaction configuration
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(t => t.TransactionId);
                entity.Property(c => c.TransactionId)
                    .HasConversion(TransactionId => TransactionId.Value,
                                value => TransactionId.Create(value));

                // Configure Money as owned type
                entity.OwnsOne(t => t.Amount, money =>
                {
                    money.Property(m => m.Amount)
                        .HasColumnName("Amount")
                        .HasPrecision(18, 2);
                    money.Property(m => m.Currency)
                        .HasColumnName("Currency")
                        .HasMaxLength(3);
                });

                entity.Property(t => t.Type)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(t => t.Description).HasMaxLength(500);
                entity.Property(t => t.Reference).HasMaxLength(50);
                entity.Property(t => t.Timestamp).IsRequired();
            });

            // Global query filter in DbContext - Automatically Exclude Deleted Records
            modelBuilder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Account>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<Transaction>().HasQueryFilter(t => !t.Account.IsDeleted);

            // Account concurrency implementation
            modelBuilder.Entity<Account>(entity =>
            {
                entity.Property(a => a.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            });

            // Seed the DB
            modelBuilder.Entity<Customer>().HasData(new
            {
                CustomerId = CustomerId.Create(Guid.Parse("a1b2c3d4-1234-5678-9abc-123456789abc")),
                FirstName = "Alice",
                LastName = "Johnson",
                Email = "alice.johnson@email.com",
                PhoneNumber = "555-0101",
                BVN = "20000000000",
                Address="13, oshinowo street , abue osho",
                CreditScore = 40,
                DateOfBirth = DateTime.UtcNow.AddYears(-30),
                DateCreated = DateTime.UtcNow.AddDays(-30),
                IsActive = true,
                IsDeleted = false,
            }
             );



            modelBuilder.Entity<Account>().HasData(new
            {
                AccountId = AccountId.Create(Guid.Parse("c3d4e5f6-3456-7890-cde1-345678901cde")),
                AccountNumber = AccountNumber.Create("1000000001"),
                AccountType = AccountType.Checking, // EF handles enum conversion
                CustomerId = CustomerId.Create(Guid.Parse("a1b2c3d4-1234-5678-9abc-123456789abc")),
                Currency = "NGN",
                DateOpened = DateTime.UtcNow.AddDays(-20),
                IsActive = true,
                IsDeleted = false   
            }
            );


            modelBuilder.Entity<Account>().OwnsOne(a => a.Balance).HasData(
                new
                {
                    AccountId = AccountId.Create(Guid.Parse("c3d4e5f6-3456-7890-cde1-345678901cde")),
                    Amount = 1500.00m,
                    Currency = "NGN"
                }
            );


        }

        public async Task SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default)
        {
            // Convert domain events to outbox messages
            var events = ChangeTracker.Entries<AggregateRoot<AccountId>>()
                .SelectMany(x => x.Entity.DomainEvents)
                .Select(domainEvent => new OutBoxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().Name,
                    Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredOn = domainEvent.OccurredOn
                })
                .ToList();

            // Clear domain events from aggregates
            ChangeTracker.Entries<AggregateRoot<AccountId>>()
                .ToList()
                .ForEach(entry => entry.Entity.ClearDomainEvents());

            // Save changes (including outbox messages) in single transaction
            await base.SaveChangesAsync(cancellationToken);

            // Add outbox messages after saving to ensure they're included in transaction
            if (events.Any())
            {
                await OutboxMessages.AddRangeAsync(events, cancellationToken);
                await base.SaveChangesAsync(cancellationToken);
            }
        }


    }
}
