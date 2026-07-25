namespace meesuanruam_service.DTO.table
{
    public class REPORT_TOPIC
    {
        public Int64 id { get; set; }
        public string? report_topic_code { get; set; }
        public bool? building_permit_issuance { get; set; }
        public bool? procurement_local_road { get; set; }
        public bool? another { get; set; }
        public string? another_detail { get; set; }
        public string report_code { get; set; }
    }
}
