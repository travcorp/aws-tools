using Amazon;
using Amazon.Runtime;

namespace TTC.Deployment.Tests
{
    public static class TestConfiguration
    {
        public static RegionEndpoint AwsEndpoint { get { return RegionEndpoint.USWest2; } }
        public static AWSCredentials Credentials { get { return new TestSuiteCredentials(); } }
    }
}
