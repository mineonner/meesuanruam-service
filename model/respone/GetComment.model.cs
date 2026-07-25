using meesuanruam_service.model.request;

namespace meesuanruam_service.model.respone
{
    public class GetCommentModel
    {
        public string gender { get; set; }
        public string occupation { get; set; }
        public string location { get; set; }
        public string plan_topic { get; set; }
        public string plan_another_detail { get; set; }
        public string detail { get; set; }
        public string create_date { get; set; }
        public List<FileAttachment>? files { get; set; }
    }
}
