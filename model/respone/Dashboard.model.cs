namespace meesuanruam_service.model.respone
{
    public class DashboardModel
    {
        public IEnumerable<List<long>> building_permit_issuance { get; set; }
        public IEnumerable<List<long>> procurement_local_road { get; set; }
        public IEnumerable<List<long>> another { get; set; }
        public IEnumerable<List<long>> report_per_day { get; set; }
        public IEnumerable<List<long>> comment_per_day { get; set; }
    }
}
