using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Models;

[Table("Cards", Schema = "ops")]
public partial class Card
{
    [Key]
    public long card_id { get; set; }

    public long customer_id { get; set; }

    [StringLength(20)]
    public string network { get; set; } = null!;

    [StringLength(4)]
    [Unicode(false)]
    public string last4 { get; set; } = null!;

    [StringLength(2)]
    [Unicode(false)]
    public string? issue_country { get; set; }

    public DateTime created_at { get; set; }

    [InverseProperty("card")]
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    [ForeignKey("customer_id")]
    [InverseProperty("Cards")]
    public virtual Customer customer { get; set; } = null!;
}
