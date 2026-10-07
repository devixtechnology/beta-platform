using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MachineType> MachineTypes => Set<MachineType>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderInput> WorkOrderInputs => Set<WorkOrderInput>();
    public DbSet<WorkOrderOutput> WorkOrderOutputs => Set<WorkOrderOutput>();
    public DbSet<WorkOrderInputProduct> WorkOrderInputProducts => Set<WorkOrderInputProduct>();
    public DbSet<OeeData> OeeData => Set<OeeData>();
    public DbSet<PowerData> PowerData => Set<PowerData>();

    // IoT configuration tables (ported from the reference SPackEdgeView schema).
    public DbSet<SourceAvailable> SourcesAvailable => Set<SourceAvailable>();
    public DbSet<MachineTag> MachineTags => Set<MachineTag>();
    public DbSet<MachineKpi> MachineKpis => Set<MachineKpi>();
    public DbSet<MachineMasterData> MachinesMasterData => Set<MachineMasterData>();
    public DbSet<MachineProperty> MachineProperties => Set<MachineProperty>();

    /// <summary>Single-row ERP integration credentials, maintained from the ERP Settings screen.</summary>
    public DbSet<ErpSetting> ErpSettings => Set<ErpSetting>();

    // Fixed timestamp for deterministic seed data (HasData must not use DateTime.Now).
    private static readonly DateTime SeedDate = new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Unspecified);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---- MachineType (US1) ----
        modelBuilder.Entity<MachineType>(entity =>
        {
            entity.HasKey(e => e.MachineTypeId);
            entity.HasIndex(e => e.Name).IsUnique();

            // Phase 1 seed — only the two in-scope types (FR-013). Others added by a later migration.
            entity.HasData(
                new MachineType { MachineTypeId = 1, Name = "Forming Machine", NameEnglish = "Forming Machine", ProductionLine = "Armor Rod & Guy Grip line", IsActive = true, CreatedAt = SeedDate },
                new MachineType { MachineTypeId = 2, Name = "Flat Washer Line", NameEnglish = "Flat Washer Line", ProductionLine = "Flat Washer Line", IsActive = true, CreatedAt = SeedDate }
            );
        });

        // ---- Machine (US1) ----
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(e => e.MachineId);
            entity.HasIndex(e => e.MachineCode).IsUnique();
            entity.HasIndex(e => e.MachineName).IsUnique();
            entity.HasIndex(e => e.MachineTypeId);

            entity.HasOne(e => e.MachineType)
                .WithMany(t => t.Machines)
                .HasForeignKey(e => e.MachineTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Product (US2) ----
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId);
            entity.HasIndex(e => e.ProductCode).IsUnique();
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
        });

        // ---- WorkOrder (US3) ----
        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.HasKey(e => e.WorkOrderId);
            entity.HasIndex(e => e.WorkOrderNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.MachineId);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.QtyToManufacture).HasPrecision(10, 2);
            entity.Property(e => e.HourRate).HasPrecision(10, 2);
            entity.Property(e => e.WorkstationCapabilityPerHour).HasPrecision(10, 2);
            entity.Property(e => e.TotalRuntime).HasPrecision(10, 2);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.SyncStatus).HasConversion<int>();

            // The production-chain stage the order belongs to, and the stage it actually ran on.
            // Restrict on both: a machine type in use by an order is deactivated, never deleted.
            entity.HasOne(e => e.OrderType)
                .WithMany()
                .HasForeignKey(e => e.OrderTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.MachineType)
                .WithMany()
                .HasForeignKey(e => e.MachineTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.InputProduct)
                .WithMany()
                .HasForeignKey(e => e.InputProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OutputProduct)
                .WithMany()
                .HasForeignKey(e => e.OutputProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- WorkOrderInput (003) — a weight, and optionally the upstream unit it came from ----
        modelBuilder.Entity<WorkOrderInput>(entity =>
        {
            entity.HasKey(e => e.InputId);
            entity.HasIndex(e => e.WorkOrderId);

            entity.Property(e => e.Weight).HasPrecision(10, 2);

            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.Inputs)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: products are deactivated, never deleted, and a consumed product is history.
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // One output is consumed once: a unit cannot be fed into two orders. Unique rather
            // than plain, so a double scan fails at the database instead of double-counting.
            entity.HasIndex(e => e.SourceOutputId).IsUnique();

            // Restrict: an output that has been consumed is history, not something a delete of the
            // producing order may quietly erase from under the order that consumed it.
            entity.HasOne(e => e.SourceOutput)
                .WithOne(o => o.ConsumedBy)
                .HasForeignKey<WorkOrderInput>(e => e.SourceOutputId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- WorkOrderOutput — the units an order produced ----
        modelBuilder.Entity<WorkOrderOutput>(entity =>
        {
            entity.HasKey(e => e.OutputId);

            // The code is how the next order in the chain names this unit, so it must be unique
            // across the whole table, not just within its order.
            entity.HasIndex(e => e.UniqueCode).IsUnique();

            // What vw_running_orders_summary aggregates, and how the printer claims work.
            entity.HasIndex(e => e.WorkOrderId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.PrintStatus);

            entity.Property(e => e.Weight).HasPrecision(10, 2);

            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.Outputs)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- WorkOrderInputProduct (006) — which products an order consumes, in the caller's order ----
        modelBuilder.Entity<WorkOrderInputProduct>(entity =>
        {
            entity.HasKey(e => e.WorkOrderInputProductId);

            // One row per product per order. 005 refuses a repeated code at the edge (R13); this
            // index is the same rule stated where it cannot be bypassed, and it is what makes two
            // simultaneous callers fail loudly instead of quietly storing a duplicate.
            entity.HasIndex(e => new { e.WorkOrderId, e.ProductId }).IsUnique();

            // The read this table exists for: "every input of this order, in order".
            entity.HasIndex(e => new { e.WorkOrderId, e.Position });

            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.InputProducts)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, matching work_orders.input_product_id: a product referenced by an order is
            // deactivated, never deleted out from under it.
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- ErpSetting — single-row ERP credentials ----
        modelBuilder.Entity<ErpSetting>(entity =>
        {
            entity.HasKey(e => e.ErpSettingId);

            // The key is the constant ErpSetting.SingletonId, not a generated value. Letting MySQL
            // auto-increment it would allow a second row to appear, and "the ERP settings" would
            // stop being a single answerable question.
            entity.Property(e => e.ErpSettingId).ValueGeneratedNever();
        });

        // ---- OeeData (US4) — compatibility-locked, read-only ----
        modelBuilder.Entity<OeeData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MachineId, e.Timestamp });
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.OrderId);

            entity.Property(e => e.Timestamp).HasColumnType("datetime(6)");
            entity.Property(e => e.Availability).HasPrecision(5, 2);
            entity.Property(e => e.Quality).HasPrecision(5, 2);
            entity.Property(e => e.Performance).HasPrecision(5, 2);
            // total_count / total_goods intentionally have NO precision set (MySQL default
            // decimal(65,30)) to match the reference writer contract — do not add precision.

            entity.HasOne(e => e.Machine)
                .WithMany(m => m.OeeData)
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WorkOrder)
                .WithMany()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- PowerData (US4) — compatibility-locked, read-only ----
        modelBuilder.Entity<PowerData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MachineId, e.Timestamp });
            entity.HasIndex(e => e.Timestamp);

            entity.Property(e => e.Timestamp).HasColumnType("datetime(3)");
            entity.Property(e => e.KwHr).HasPrecision(10, 2);
            entity.Property(e => e.V1).HasPrecision(8, 2);
            entity.Property(e => e.V2).HasPrecision(8, 2);
            entity.Property(e => e.V3).HasPrecision(8, 2);
            entity.Property(e => e.V12).HasPrecision(8, 2);
            entity.Property(e => e.V23).HasPrecision(8, 2);
            entity.Property(e => e.V13).HasPrecision(8, 2);
            entity.Property(e => e.A1).HasPrecision(8, 2);
            entity.Property(e => e.A2).HasPrecision(8, 2);
            entity.Property(e => e.A3).HasPrecision(8, 2);
            entity.Property(e => e.AAvg).HasPrecision(8, 2);
            entity.Property(e => e.Frequency).HasPrecision(5, 2);

            entity.HasOne(e => e.Machine)
                .WithMany(m => m.PowerData)
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- IoT config tables (ported from reference SPackEdgeView) ----

        // sources_available — unique source_identifier is the principal key for machine_properties.
        modelBuilder.Entity<SourceAvailable>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceIdentifier).IsUnique();
            entity.HasIndex(e => e.Source);
        });

        // machine_tags — per-machine tags; cascade-deleted with the owning machine.
        modelBuilder.Entity<MachineTag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MachineId);
            entity.HasIndex(e => e.Source);
            entity.HasIndex(e => e.SourceIdentifier);

            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // machines_kpis
        modelBuilder.Entity<MachineKpi>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceIdentifier);
        });

        // machines_master_data
        modelBuilder.Entity<MachineMasterData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceIdentifier);
        });

        // machine_properties — FK to machines (restrict) and to sources_available.source_identifier.
        modelBuilder.Entity<MachineProperty>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MachineId, e.Name, e.Code })
                .IsUnique()
                .HasDatabaseName("uk_machine_prop_detailed");
            entity.HasIndex(e => e.MachineId);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.SourceIdentifier);

            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SourceAvailable)
                .WithMany()
                .HasForeignKey(e => e.SourceIdentifier)
                .HasPrincipalKey(s => s.SourceIdentifier)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
