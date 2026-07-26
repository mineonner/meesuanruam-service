namespace meesuanruam_service.model.respone
{
    public class ProjectFileResModel
    {
        public Int64 id { get; set; }
        /// <summary>คีย์ของตัวชี้วัดที่ไฟล์นี้ผูกอยู่ เช่น Policy_Process_1 หรือ TOR_Acthievement_1</summary>
        public string measures_prefix { get; set; }
        public string? path { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
        public long? size { get; set; }
    }
}
