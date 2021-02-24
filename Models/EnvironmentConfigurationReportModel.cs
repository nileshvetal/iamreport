namespace Gcs.Model
{
    public class EnvironmentConfigurationReportModel
    {
        public string subscriptionName { get; set; }

        public string resourceName { get; set; }

        public string resourceGroupName { get; set; }

        public string resourceLocation { get; set; }

        public string resourceType { get; set; }

        public string configurationDetails { get; set; }
    }
}