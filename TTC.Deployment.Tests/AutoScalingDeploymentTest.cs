using System;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;
using Amazon.IdentityManagement.Model;
using Amazon.IdentityManagement;
using System.Threading;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class AutoScalingDeploymentTest
    {
        private AmazonIdentityManagementServiceClient _iamClient;
        private AwsConfiguration _awsConfiguration;
        private Deployer _deployer;
        private Stack _stack;
        const string StackName = "AwsToolsAutoScalingTestVPC2";
        private static bool _hasCreatedStack;

        [SetUp]
        public void EnsureStackExists()
        {
            if (_hasCreatedStack) return;

            _awsConfiguration = new AwsConfiguration
            {
                AssumeRoleTrustDocument = Roles.Path("code-deploy-trust.json"),
                IamRolePolicyDocument = Roles.Path("code-deploy-policy.json"),
                Bucket = "aws-deployment-tools-tests",
                RoleName = "CodeDeployRole",
                AwsEndpoint = TestConfiguration.AwsEndpoint,
                Credentials = new TestSuiteCredentials()
            };

            _iamClient = new AmazonIdentityManagementServiceClient(
                new AmazonIdentityManagementServiceConfig
                {
                    RegionEndpoint = _awsConfiguration.AwsEndpoint,
                    ProxyHost = _awsConfiguration.ProxyHost,
                    ProxyPort = _awsConfiguration.ProxyPort
                });
            
            DeletePreviousTestStack();

            _deployer = new Deployer(_awsConfiguration);

            _stack = _deployer.CreateStack(new StackTemplate
            {
                StackName = StackName,
                TemplatePath = CloudFormationTemplates.Path("example-windows-vpc-autoscaling-group.template")
            });
            _hasCreatedStack = true;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            DeletePreviousTestStack();
        }

        private void DeletePreviousTestStack()
        {
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, StackName);
        }

        [Test]
        public void DeploysCodeToInstancesInTheAutoScalingGroup()
        {
            var goodRevision = _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = "HelloWorld",
                Version = "GoodAutoScalingRevision",
                LocalDirectory = ExampleRevisions.Directory("HelloWorld-AutoScaling")
            });

            _deployer.DeployRelease(goodRevision, StackName, "CodeDeployRole");

            var publicDnsName = _stack.Outputs["publicDnsName"];
            var homePageUrl = string.Format("http://{0}/index.aspx", publicDnsName);

            Console.WriteLine(homePageUrl);

            var webpageText = Retry.Do(() => Http.Get(homePageUrl), TimeSpan.FromSeconds(10));
            Assert.That(webpageText, Is.EqualTo("Hello, world!"));
        }
    }
}
