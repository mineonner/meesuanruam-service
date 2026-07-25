namespace meesuanruam_service.model.respone
{
    public class ProjectInfoResModel
    {
        public string code { get; set; }
        public string name { get; set; }
        public string status { get; set; }
        public string create_date { get; set; }
        public string create_by { get; set; }
        public List<MeasuresResModel> measures { get; set; }
        public List<ProcessResModel> process { get; set; }
        public List<IndicatorsActhievementResModel> indicators_acthievement { get; set; }

    }
}
