namespace meesuanruam_service.model.request
{
    public class ReportTopic
    {
        public bool? building_permit_issuance { get; set; }
        public bool? procurement_local_road { get; set; }
        public bool? another { get; set; }
        public string? another_detail { get; set; }
    }
}
