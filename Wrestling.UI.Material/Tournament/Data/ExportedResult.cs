namespace Wrestling.UI.Material.Tournament.Data
{
    // CSV export row — public surface for CsvHelper. Fields mirror what
    // operators have come to expect (and what the legacy Dashboard CSV
    // produced 1:1, so existing post-processing scripts stay compatible).
    public class ExportedResult
    {
        public string GroupName { get; set; }
        public string FullName { get; set; }
        public string TeamName { get; set; }
        public string TeamCity { get; set; }
        public string TeamCoach { get; set; }
        public string BirthDate { get; set; }
        public int? FinalPlace { get; set; }
        public int PointsEarned { get; set; }
        public int PointsLost { get; set; }
        public int WinsCount { get; set; }
        public int LoseCount { get; set; }
        public int WinsByTushe { get; set; }
        public int WinsByDomination { get; set; }
        public int WinsByPoints { get; set; }
        public int LoseByTushe { get; set; }
        public int LoseByDomination { get; set; }
        public int LoseByPoints { get; set; }
    }
}
