using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Models;

[Table("Merchants", Schema = "ops")]
public partial class Merchant
{
    [Key]
    public long merchant_id { get; set; }

    [StringLength(200)]
    public string name { get; set; } = null!;

    [StringLength(100)]
    public string? category { get; set; }

    [StringLength(2)]
    [Unicode(false)]
    public string? country { get; set; }

    [StringLength(20)]
    public string? risk_level { get; set; }

    public DateTime created_at { get; set; }

    [InverseProperty("merchant")]
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
