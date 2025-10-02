using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Models;

[Table("Transactions", Schema = "ops")]
[Index("customer_id", "tx_utc", Name = "IX_Tx_Customer")]
[Index("merchant_id", "tx_utc", Name = "IX_Tx_Merchant")]
[Index("tx_utc", Name = "IX_Tx_Time")]
public partial class Transaction
{
    [Key]
    public long tx_id { get; set; }

    public long customer_id { get; set; }

    public long card_id { get; set; }

    public long merchant_id { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal amount { get; set; }

    [StringLength(3)]
    [Unicode(false)]
    public string currency { get; set; } = null!;

    public string tx_utc { get; set; }

    [StringLength(20)]
    public string? entry_mode { get; set; }

    [StringLength(20)]
    public string? channel { get; set; }

    [MaxLength(64)]
    public byte[]? device_id_hash { get; set; }

    [MaxLength(64)]
    public byte[]? ip_hash { get; set; }

    public double? lat { get; set; }

    public double? lon { get; set; }

    [StringLength(20)]
    public string status { get; set; } = null!;

    [InverseProperty("tx")]
    public virtual Label? Label { get; set; }

    [InverseProperty("tx")]
    public virtual TxScore? TxScore { get; set; }

    [ForeignKey("card_id")]
    [InverseProperty("Transactions")]
    public virtual Card card { get; set; } = null!;

    [ForeignKey("customer_id")]
    [InverseProperty("Transactions")]
    public virtual Customer customer { get; set; } = null!;

    [ForeignKey("merchant_id")]
    [InverseProperty("Transactions")]
    public virtual Merchant merchant { get; set; } = null!;
}
