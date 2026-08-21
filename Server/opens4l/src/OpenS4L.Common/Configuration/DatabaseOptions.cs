namespace OpenS4L.Common.Configuration
{
    public class DatabaseOptions
    {
        public ConnectionStrings ConnectionStrings { get; set; }
        public bool RunMigration { get; set; }
    }
}
