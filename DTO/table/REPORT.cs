namespace meesuanruam_service.DTO.table
{
    public class REPORT
    {
        public Int64 id { get; set; }
        public string report_code { get; set; }
        public string persernal_type { get; set; }
        public string? name_title { get; set; }
        public string? name_title_another { get; set; }
        public string? firstname { get; set; }
        public string? lastname { get; set; }
        public string? email { get; set; }
        public string? telephone { get; set; }
        public string? report_government_agencies { get; set; }
        public string report_detail { get; set; }
        public DateTime create_date { get; set; }
    }
}
