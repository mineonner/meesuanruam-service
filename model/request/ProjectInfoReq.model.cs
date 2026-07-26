using meesuanruam_service.model.respone;
using System.ComponentModel.DataAnnotations;

namespace meesuanruam_service.model.request
{
    public class ProjectInfoReqModel
    {
        public string? code { get; set; }
        [Required]
        public string name { get; set; }
        [Required]
        public string status { get; set; }
        public string? create_date { get; set; }
        public string? create_by { get; set; }
        public string? years { get; set; }
        public List<MeasuresResModel> measures { get; set; }
        public List<ProcessResModel> process { get; set; }
        public List<IndicatorsActhievementResModel> indicators_acthievement { get; set; }
    }
}
