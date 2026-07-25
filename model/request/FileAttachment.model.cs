namespace meesuanruam_service.model.request
{
    public class FileAttachment
    {
        public string? path { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
        public long? size { get; set; }
        //public IFormFile? file { get; set; }
    }
}
