namespace meesuanruam_service.model.request
{
    public class SaveComment
    {
        public string gender { get; set; }
        public string occupation { get; set; }
        public string? location { get; set; }
        public string plan_topic { get; set; }
        public string plan_another_detail { get; set; }
        public string detail { get; set; }
        public List<FileAttachment>? files { get; set; }
    }
}
