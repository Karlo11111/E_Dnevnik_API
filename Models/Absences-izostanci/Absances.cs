namespace E_Dnevnik_API.Models.Absences_izostanci
{
    // jedan zapis izostanka - datum i lista predmeta s kojih je učenik izostao taj dan
    public class AbsenceRecord
    {
        public string? Date { get; set; }
        public List<string>? Subjects { get; set; }
    }
}
