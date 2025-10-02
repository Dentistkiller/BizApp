using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Models;

[PrimaryKey("run_id", "metric")]
[Table("Metrics", Schema = "ml")]
[Index("run_id", Name = "IX_Metrics_Run")]
public partial class Metric
{
    [Key]
    public long run_id { get; set; }

    [Key]
    [StringLength(40)]
    public string metric { get; set; } = null!;

    public double value { get; set; }

    [ForeignKey("run_id")]
    [InverseProperty("Metrics")]
    public virtual Run run { get; set; } = null!;
}
