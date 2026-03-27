namespace E_Dnevnik_API.ScrapingServices
{
    // custom exception koji nosi http status kod - servis ga baca, a kontroler ga hvata i pretvara u odgovor
    public class ScraperException : Exception
    {
        public int StatusCode { get; }

        public ScraperException(int statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
