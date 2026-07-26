namespace meesuanruam_service.DTO.table
{
    public class COMMENT
    {
        public Int64 id { get; set; }
        public string comment_code { get; set; }
        public string? gender { get; set; }
        public string? occupation { get; set; }
        public string? location { get; set; }
        public string? plan_topic { get; set; }
        public string? plan_another_detail { get; set; }
        public DateTime create_date { get; set; }
        public string? detail { get; set; }
        public string org_unit_code { get; set; }
    }
}
