# Database Migration Instructions
## Adding Hangfire Background Job Properties to Account Entity

---

## Prerequisites
- Entity Framework Core CLI tools installed
- SQL Server running and accessible
- Application buildable without errors

---

## Step 1: Verify EF Core Tools

```bash
# Check if EF Core tools are installed
dotnet ef --version

# If not installed, install globally
dotnet tool install --global dotnet-ef

# Or update if already installed
dotnet tool update --global dotnet-ef
```

---

## Step 2: Create the Migration

Open a terminal in the project root directory and run:

```bash
# Navigate to the infrastructure project
cd CoreBanking.Infrastructure

# Create the migration
dotnet ef migrations add AddHangfireAccountProperties --startup-project ../CoreBankingTest.API --context BankingDbContext --output-dir Data/Migrations

# Alternative: If above doesn't work, try from the root directory
cd ..
dotnet ef migrations add AddHangfireAccountProperties --project CoreBanking.Infrastructure --startup-project CoreBankingTest.API --context BankingDbContext
```

---

## Step 3: Review the Migration

After creating the migration, review the generated file in:
`CoreBanking.Infrastructure/Data/Migrations/[Timestamp]_AddHangfireAccountProperties.cs`

The migration should add these columns to the `Accounts` table:

### New Columns
| Column Name | Data Type | Nullable | Default |
|-------------|-----------|----------|---------|
| LastActivityDate | datetime2 | No | GETUTCDATE() |
| Status | nvarchar(50) | No | 'Active' |
| IsInterestBearing | bit | No | 1 (true) |
| IsArchived | bit | No | 0 (false) |

### Example Migration Up Method
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<DateTime>(
        name: "LastActivityDate",
        table: "Accounts",
        type: "datetime2",
        nullable: false,
        defaultValueSql: "GETUTCDATE()");

    migrationBuilder.AddColumn<string>(
        name: "Status",
        table: "Accounts",
        type: "nvarchar(50)",
        maxLength: 50,
        nullable: false,
        defaultValue: "Active");

    migrationBuilder.AddColumn<bool>(
        name: "IsInterestBearing",
        table: "Accounts",
        type: "bit",
        nullable: false,
        defaultValue: true);

    migrationBuilder.AddColumn<bool>(
        name: "IsArchived",
        table: "Accounts",
        type: "bit",
        nullable: false,
        defaultValue: false);
}
```

### Example Migration Down Method
```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "LastActivityDate",
        table: "Accounts");

    migrationBuilder.DropColumn(
        name: "Status",
        table: "Accounts");

    migrationBuilder.DropColumn(
        name: "IsInterestBearing",
        table: "Accounts");

    migrationBuilder.DropColumn(
        name: "IsArchived",
        table: "Accounts");
}
```

---

## Step 4: Apply the Migration

### Option A: Using EF Core CLI (Recommended for Development)

```bash
# Apply to database
dotnet ef database update --startup-project CoreBankingTest.API --context BankingDbContext
```

### Option B: At Application Startup (Optional)

Add this to Program.cs (already configured):

```csharp
// Apply pending migrations automatically
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    dbContext.Database.Migrate();
}
```

---

## Step 5: Verify the Migration

### SQL Server Management Studio
```sql
-- Check if columns exist
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Accounts'
  AND COLUMN_NAME IN ('LastActivityDate', 'Status', 'IsInterestBearing', 'IsArchived');

-- Expected output: 4 rows showing the new columns
```

### Using dotnet ef
```bash
# List all migrations
dotnet ef migrations list --startup-project CoreBankingTest.API --context BankingDbContext

# Check if migration was applied (should show as applied)
```

---

## Step 6: Update Existing Data (If Needed)

If you have existing accounts that need initial values:

```sql
-- Set default values for existing records
UPDATE Accounts
SET
    LastActivityDate = GETUTCDATE(),
    Status = 'Active',
    IsInterestBearing = CASE
        WHEN AccountType IN ('Savings', 'FixedDeposit') THEN 1
        ELSE 0
    END,
    IsArchived = 0
WHERE LastActivityDate IS NULL;  -- Only update records that don't have these values
```

---

## Troubleshooting

### Issue: "Build failed"
**Solution**: Build the project first
```bash
dotnet build CoreBankingTest.API/CoreBankingTest.API.csproj
```

### Issue: "No DbContext named 'BankingDbContext' was found"
**Solution**: Ensure you're specifying the correct project and context
```bash
dotnet ef migrations add AddHangfireAccountProperties \
  --project CoreBanking.Infrastructure/CoreBankingTest.DAL.csproj \
  --startup-project CoreBankingTest.API/CoreBankingTest.API.csproj \
  --context BankingDbContext
```

### Issue: "Unable to resolve service for type 'Microsoft.EntityFrameworkCore.DbContextOptions'"
**Solution**: Make sure your connection string in appsettings.json is correct

### Issue: Migration creates duplicate columns
**Solution**: Remove the migration and try again
```bash
# Remove the last migration
dotnet ef migrations remove --startup-project CoreBankingTest.API --context BankingDbContext

# Clean, rebuild, and try again
dotnet clean
dotnet build
dotnet ef migrations add AddHangfireAccountProperties --startup-project CoreBankingTest.API
```

### Issue: "A network-related or instance-specific error occurred"
**Solution**: Check SQL Server connection
```bash
# Test connection string
sqlcmd -S "DESKTOP-UHUG7MP\SQLEXPRESS01" -d BankingManagement -E

# Or update connection string in appsettings.json
```

---

## Customer Entity Migration (If EmailOptIn doesn't exist)

If you also need to add the EmailOptIn property to Customer:

```bash
dotnet ef migrations add AddCustomerEmailOptIn --startup-project CoreBankingTest.API --context BankingDbContext
dotnet ef database update --startup-project CoreBankingTest.API --context BankingDbContext
```

---

## Rollback Instructions

If you need to rollback the migration:

```bash
# Rollback to previous migration
dotnet ef database update [PreviousMigrationName] --startup-project CoreBankingTest.API --context BankingDbContext

# Or rollback completely
dotnet ef database update 0 --startup-project CoreBankingTest.API --context BankingDbContext

# Remove the migration file
dotnet ef migrations remove --startup-project CoreBankingTest.API --context BankingDbContext
```

---

## Production Deployment

For production, generate SQL scripts instead of applying directly:

```bash
# Generate SQL script for review
dotnet ef migrations script --startup-project CoreBankingTest.API --context BankingDbContext --output migration.sql

# Or generate from specific migration
dotnet ef migrations script [FromMigration] AddHangfireAccountProperties --startup-project CoreBankingTest.API --context BankingDbContext --output migration.sql

# Review the script and apply manually in production
```

---

## Post-Migration Checklist

- [ ] Migration created successfully
- [ ] Migration reviewed for correctness
- [ ] Database updated successfully
- [ ] New columns visible in database
- [ ] Existing data updated with default values
- [ ] Application builds and runs without errors
- [ ] Hangfire jobs can access new properties
- [ ] Test account creation/updates work correctly

---

## Quick Reference Commands

```bash
# Full migration workflow
dotnet ef migrations add AddHangfireAccountProperties --startup-project CoreBankingTest.API
dotnet ef database update --startup-project CoreBankingTest.API

# Check migration status
dotnet ef migrations list --startup-project CoreBankingTest.API

# Generate SQL script (for production)
dotnet ef migrations script --startup-project CoreBankingTest.API --output migration.sql

# Rollback
dotnet ef database update [PreviousMigrationName] --startup-project CoreBankingTest.API
dotnet ef migrations remove --startup-project CoreBankingTest.API
```

---

**Created**: Day 11 - Hangfire Implementation
**Last Updated**: 2025-11-13
