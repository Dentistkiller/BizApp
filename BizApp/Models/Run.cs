using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Models;

[Table("Runs", Schema = "ml")]
public partial class Run
{
    [Key]
    public long run_id { get; set; }

    public DateTime started_at { get; set; }

    public DateTime? finished_at { get; set; }

    [StringLength(50)]
    public string model_version { get; set; } = null!;

    [StringLength(200)]
    public string? label_policy { get; set; }

    [StringLength(4000)]
    public string? notes { get; set; }

    [InverseProperty("run")]
    public virtual ICollection<Metric> Metrics { get; set; } = new List<Metric>();

    [InverseProperty("run")]
    public virtual ICollection<TxScore> TxScores { get; set; } = new List<TxScore>();
}
