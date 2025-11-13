using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Models;

[Table("Transactions", Schema = "ops")]
[Index(nameof(customer_id), nameof(tx_utc), Name = "IX_Tx_Customer")]
[Index(nameof(merchant_id), nameof(tx_utc), Name = "IX_Tx_Merchant")]
[Index(nameof(tx_utc), Name = "IX_Tx_Time")]
public partial class Transaction
{
    [Key]
    public long tx_id { get; set; }

    [Required(ErrorMessage = "Customer is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "Customer is required.")]
    public long customer_id { get; set; }

    [Required(ErrorMessage = "Card is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "Card is required.")]
    public long card_id { get; set; }

    [Required(ErrorMessage = "Merchant is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "Merchant is required.")]
    public long merchant_id { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Column(TypeName = "decimal(12, 2)")]
    [Range(typeof(decimal), "0.01", "9999999999.99", ErrorMessage = "Amount must be at least 0.01.")]
    public decimal amount { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be 3 letters.")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be 3 uppercase letters (ISO-4217).")]
    [Unicode(false)]
    public string currency { get; set; } = null!;

    // Stored as text 'yyyy-MM-dd HH:mm:ss'
    [Required]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}$",
        ErrorMessage = "Timestamp must be in format yyyy-MM-dd HH:mm:ss (UTC).")]
    public string tx_utc { get; set; } = null!;

    [StringLength(20, ErrorMessage = "Entry mode cannot exceed 20 characters.")]
    public string? entry_mode { get; set; }

    [StringLength(20, ErrorMessage = "Channel cannot exceed 20 characters.")]
    public string? channel { get; set; }

    // SHA-256 => 32 bytes; column allows up to 64 bytes, which is fine.
    [MaxLength(64)]
    public byte[]? device_id_hash { get; set; }

    [MaxLength(64)]
    public byte[]? ip_hash { get; set; }

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double? lat { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double? lon { get; set; }

    [Required]
    [StringLength(20)]
    public string status { get; set; } = "Pending";

    [InverseProperty(nameof(Models.Label.tx))]
    public virtual Label? Label { get; set; }

    [InverseProperty(nameof(Models.TxScore.tx))]
    public virtual TxScore? TxScore { get; set; }

    [ForeignKey(nameof(card_id))]
    [InverseProperty(nameof(Models.Card.Transactions))]
    public virtual Card card { get; set; } = null!;

    [ForeignKey(nameof(customer_id))]
    [InverseProperty(nameof(Models.Customer.Transactions))]
    public virtual Customer customer { get; set; } = null!;

    [ForeignKey(nameof(merchant_id))]
    [InverseProperty(nameof(Models.Merchant.Transactions))]
    public virtual Merchant merchant { get; set; } = null!;
}
