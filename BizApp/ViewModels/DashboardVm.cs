namespace BizApp.ViewModels
{
    public class DashboardVm
    {
        // KPI cards
        public int TotalTx24h { get; set; }
        public int FlaggedTx24h { get; set; }
        public decimal TotalAmount24h { get; set; }
        public double FlagRate24h => TotalTx24h == 0 ? 0 : (double)FlaggedTx24h / TotalTx24h;

        // Latest model run
        public long? LatestRunId { get; set; }
        public string? LatestModelVersion { get; set; }
        public DateTime? LatestRunStarted { get; set; }
        public DateTime? LatestRunFinished { get; set; }

        // Top merchants table
        public List<TopMerchantRow> TopMerchants { get; set; } = new();

        public class TopMerchantRow
        {
            public long MerchantId { get; set; }
            public string MerchantName { get; set; } = "";
            public int TxCount { get; set; }
            public int FlaggedCount { get; set; }
            public double FlagRate => TxCount == 0 ? 0 : (double)FlaggedCount / TxCount;
        }
    }

    // For API responses (charts)
    public record DailySeriesPoint(string date, int total, int flagged);
}
