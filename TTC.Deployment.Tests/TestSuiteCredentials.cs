using System;
using Amazon.Runtime;

namespace TTC.Deployment.Tests
{
    public class TestSuiteCredentials : AWSCredentials
    {
        private readonly ImmutableCredentials _creds;

        public TestSuiteCredentials()
        {
            _creds = ReadCredentialsFromEnvironmentVariablesOrStoredProfile();
        }

        public override ImmutableCredentials GetCredentials()
        {
            return _creds;
        }

        private static ImmutableCredentials ReadCredentialsFromEnvironmentVariablesOrStoredProfile()
        {
            try
            {
                return new EnvironmentVariablesAWSCredentials().GetCredentials();
            }
            catch (InvalidOperationException)
            {
                return new StoredProfileAWSCredentials("aws-tools-tests").GetCredentials();
            }
        }
    }
}
