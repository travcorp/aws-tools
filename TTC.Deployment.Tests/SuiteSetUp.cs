using Amazon.IdentityManagement;
using NUnit.Framework;

namespace TTC.Deployment.Tests
{
    [SetUpFixture]
    public class SuiteSetUp
    {
        [SetUp]
        public void	CheckWeAreRunningAsTheSpecialUser()
	    {
            var iamClient = new AmazonIdentityManagementServiceClient();
            var me = iamClient.GetUser();
            Assert.That(me.User.UserName, Is.EqualTo("aws-tools-tests"));
	    }
    }
}
