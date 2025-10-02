using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Models;

[Table("TxScores", Schema = "ml")]
[Index("run_id", Name = "IX_TxScores_Run")]
public partial class TxScore
{
    [Key]
    public long tx_id { get; set; }

    public long run_id { get; set; }

    public double score { get; set; }

    public bool label_pred { get; set; }

    public double threshold { get; set; }

    public string? reason_json { get; set; }

    public DateTime? explained_at { get; set; }

    [ForeignKey("run_id")]
    [InverseProperty("TxScores")]
    public virtual Run run { get; set; } = null!;

    [ForeignKey("tx_id")]
    [InverseProperty("TxScore")]
    public virtual Transaction tx { get; set; } = null!;
}
