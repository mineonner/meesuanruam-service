namespace meesuanruam_service.DTO.table
{
    public class FILE
    {
        public Int64 id { get; set; }
        public string code_reference { get; set; }
        public string file_path { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
        public Int64? size { get; set; }
    }
}
