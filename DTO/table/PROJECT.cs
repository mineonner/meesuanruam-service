namespace meesuanruam_service.DTO.table
{
    public class PROJECT
    {
        public Int64 id { get; set; }
        public string code { get; set; }
        public string? name { get; set; }
        public string status { get; set; }
        public DateTime create_date { get; set; }
        public string create_by { get; set; }
        public string years { get; set; }
        public string org_unit_code { get; set; }
    }
}
