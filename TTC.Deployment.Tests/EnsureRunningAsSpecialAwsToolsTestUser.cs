using System.Configuration;
using Amazon.IdentityManagement;
using NUnit.Framework;

namespace TTC.Deployment.Tests
{
    [SetUpFixture]
    public class EnsureRunningAsSpecialAwsToolsTestUser
    {
        [SetUp]
        public void CheckWeAreRunningAsTheSpecialUser()
	    {
            ConfigurationManager.AppSettings["AWSProfileName"] = "aws-tools-tests";
            var iamClient = new AmazonIdentityManagementServiceClient();
            var me = iamClient.GetUser();
            Assert.That(me.User.UserName, Is.EqualTo("aws-tools-tests"));
	    }
    }
}
