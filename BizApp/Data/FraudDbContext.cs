using System;
using System.Collections.Generic;
using BizApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Data;

public partial class FraudDbContext : DbContext
{
    public FraudDbContext()
    {
    }

    public FraudDbContext(DbContextOptions<FraudDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Card> Cards { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Label> Labels { get; set; }

    public virtual DbSet<Merchant> Merchants { get; set; }

    public virtual DbSet<Metric> Metrics { get; set; }

    public virtual DbSet<Run> Runs { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<TxScore> TxScores { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.card_id).HasName("PK__Cards__BDF201DD694A5520");

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.issue_country).IsFixedLength();
            entity.Property(e => e.last4).IsFixedLength();

            entity.HasOne(d => d.customer).WithMany(p => p.Cards)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cards_Customers");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.customer_id).HasName("PK__Customer__CD65CB85A09311C9");

            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Label>(entity =>
        {
            entity.HasKey(e => e.tx_id).HasName("PK__Labels__65E44DD10D4B61EF");

            entity.Property(e => e.tx_id).ValueGeneratedNever();
            entity.Property(e => e.labeled_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.source).HasDefaultValue("analyst");

            entity.HasOne(d => d.tx).WithOne(p => p.Label)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Labels_Tx");
        });

        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.HasKey(e => e.merchant_id).HasName("PK__Merchant__02BC30BA154F0C91");

            entity.Property(e => e.country).IsFixedLength();
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Metric>(entity =>
        {
            entity.HasOne(d => d.run).WithMany(p => p.Metrics)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Metrics_Runs");
        });

        modelBuilder.Entity<Run>(entity =>
        {
            entity.HasKey(e => e.run_id).HasName("PK__Runs__7D3D901BB03427A2");

            entity.Property(e => e.started_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.tx_id).HasName("PK__Transact__65E44DD170FC5D7B");

            entity.HasIndex(e => e.status, "IX_Tx_Status").HasFilter("([status]='Pending')");

            entity.Property(e => e.currency).IsFixedLength();
            entity.Property(e => e.status).HasDefaultValue("Pending");

            entity.HasOne(d => d.card).WithMany(p => p.Transactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tx_Cards");

            entity.HasOne(d => d.customer).WithMany(p => p.Transactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tx_Customers");

            entity.HasOne(d => d.merchant).WithMany(p => p.Transactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tx_Merchants");
        });

        modelBuilder.Entity<TxScore>(entity =>
        {
            entity.HasKey(e => e.tx_id).HasName("PK__TxScores__65E44DD10FDA168F");

            entity.Property(e => e.tx_id).ValueGeneratedNever();

            entity.HasOne(d => d.run).WithMany(p => p.TxScores)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TxScores_Runs");

            entity.HasOne(d => d.tx).WithOne(p => p.TxScore)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TxScores_Tx");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
