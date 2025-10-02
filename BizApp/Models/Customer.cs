using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BizApp.Models;

[Table("Customers", Schema = "ops")]
public partial class Customer
{
    [Key]
    public long customer_id { get; set; }

    [StringLength(200)]
    public string name { get; set; } = null!;

    // SHA-256(email) as bytes
    [MaxLength(64)]
    [Column(TypeName = "varbinary(64)")]
    public byte[]? email_hash { get; set; }

    // Optional: SHA-256(normalized phone) as bytes
    [MaxLength(64)]
    [Column(TypeName = "varbinary(64)")]
    public byte[]? phone_hash { get; set; }

    // Password PBKDF2(hash), Salt
    [MaxLength(64)]
    [Column(TypeName = "varbinary(64)")]
    public byte[]? password_hash { get; set; }

    [MaxLength(32)]
    [Column(TypeName = "varbinary(32)")]
    public byte[]? password_salt { get; set; }

    public bool is_admin { get; set; } = false;   

    public DateTime created_at { get; set; }

    [InverseProperty("customer")]
    public virtual ICollection<Card> Cards { get; set; } = new List<Card>();

    [InverseProperty("customer")]
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
