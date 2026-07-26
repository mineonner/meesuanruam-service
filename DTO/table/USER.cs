namespace meesuanruam_service.DTO.table
{
    public class USER
    {
        public Int64 id { get; set; }
        public string user_email { get; set; }
        public string password { get; set; }
        public string? token { get; set; }
        public string? token_reset_password { get; set; }
        public DateTime? begin_date { get; set; }
        public DateTime? end_date { get; set; }
        public string org_unit_code { get; set; }
    }
}
