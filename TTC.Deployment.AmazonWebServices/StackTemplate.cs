namespace TTC.Deployment.AmazonWebServices
{
    public class StackTemplate
    {
        public string StackName { get; set; }
        public string TemplatePath { get; set; }
        public string AssumedRoleARN { get; set; }
    }
}