using Newtonsoft.Json;

namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    public class SubjectScrapeResult
    {
        [JsonProperty(Order = 1)]
        public List<SubjectInfo>? Subjects { get; set; }
    }
}
