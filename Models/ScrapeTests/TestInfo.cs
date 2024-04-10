namespace E_Dnevnik_API.Models.ScrapeTests
{
    public class TestInfo
    {
        public TestInfo(string testName, string testDescription, string testDate)
        {
            TestName = testName;
            TestDescription= testDescription;
            TestDate = testDate;
        }
        public string TestName { get; set; }
        public string TestDescription { get; set; }
        public string TestDate { get; set; }
        
    }
}
