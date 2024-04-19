namespace E_Dnevnik_API.Models.ScheduleTable
{
    public class ScheduleTable
    {
        public string? Day { get; set; }
        public List<string>? Subjects { get; set; }
    }
    public class ScheduleResult
    {
        public List<ScheduleTable>? Schedule { get; set; }
    }
}
