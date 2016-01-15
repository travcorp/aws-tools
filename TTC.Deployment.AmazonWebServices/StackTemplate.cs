namespace TTC.Deployment.AmazonWebServices
{
    public class StackTemplate
    {
        public string StackName { get; set; }
        public string TemplatePath { get; set; }
        public string ParameterPath { get; set; }
    }
}