using Amazon;


namespace TTC.Deployment.AmazonWebServices
{
    public class AwsConfiguration
    {
        public string AssumeRoleTrustDocument { get; set; }
        public string IamRolePolicyDocument { get; set; }
        public string Bucket { get; set; }
        public string RoleName { get; set; }
        public RegionEndpoint AwsEndpoint { get; set; }
        public AwsProxy Proxy { get; set; }       
        public string ParametersPath { get; set; }
        public string StackOutputFile { get; set; }
        public string StackOutputBucket { get; set; }
        public string DeployToAutoScalingGroups { get; set; }
        public string AssumeRoleName { get; set; }
    }

    public class AwsProxy 
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }
}