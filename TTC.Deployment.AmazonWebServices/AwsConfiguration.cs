using Amazon;
using Amazon.Runtime;

namespace TTC.Deployment.AmazonWebServices
{
    public class AwsConfiguration
    {
        public string AssumeRoleTrustDocument { get; set; }
        public string IamRolePolicyDocument { get; set; }
        public string Bucket { get; set; }
        public string RoleName { get; set; }
        public string CodeDeployRoleName { get; set; }
        public RegionEndpoint AwsEndpoint { get; set; }
        public AwsProxy Proxy { private get; set; }
        public AWSCredentials Credentials { get; set; }
        public string ProxyHost { get { return Proxy == null ? null : Proxy.Host; } }
        public int ProxyPort { get { return Proxy == null ? -1 : Proxy.Port; } }
    }

    public class AwsProxy 
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }
}