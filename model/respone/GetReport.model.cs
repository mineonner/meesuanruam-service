using meesuanruam_service.model.request;

namespace meesuanruam_service.model.respone
{
    public class GetReportModel
    {
        public string persernal_type { get; set; }
        public string name_title { get; set; }
        public string name_title_another { get; set; }
        public string firstname { get; set; }
        public string lastname { get; set; }
        public string email { get; set; }
        public string telephone { get; set; }
        public string report_government_agencies { get; set; }
        public string report_detail { get; set; }
        public string create_date { get; set; }
        public ReportTopic report_topic { get; set; }
        public List<FileAttachment> files { get; set; }
    }
}
