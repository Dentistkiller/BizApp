using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Models;

[Table("Labels", Schema = "ml")]
public partial class Label
{
    [Key]
    public long tx_id { get; set; }

    public bool label { get; set; }

    public string labeled_at { get; set; }

    [StringLength(20)]
    public string source { get; set; } = null!;

    [ForeignKey("tx_id")]
    [InverseProperty("Label")]
    public virtual Transaction tx { get; set; } = null!;
}
