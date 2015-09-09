using System;
using System.Configuration;
using System.IO;
using System.Linq;
using Amazon;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class DeploymentTest
    {
        private AwsConfiguration _awsConfiguration;
        private Deployer _deployer;
        private Stack _stack;
        const string StackName = "TestEnv2";

        [TestFixtureSetUp]
        public void SetUp()
        {
            ConfigurationManager.AppSettings["AWSProfileName"] = "default";
            _awsConfiguration = new AwsConfiguration
            {
                AssumeRoleTrustDocument = Path.Combine(Environment.CurrentDirectory, "CodeDeployRole", "code-deploy-trust.json"),
                IamRolePolicyDocument = Path.Combine(Environment.CurrentDirectory, "CodeDeployRole", "code-deploy-policy.json"),
                Bucket = "aws-test-releases",
                RoleName = "CodeDeployRole",
                AwsEndpoint = RegionEndpoint.USEast1,
                Proxy = new AwsProxy()
            };

            _deployer = new Deployer(_awsConfiguration);

            DeletePreviousTestStack();
            _stack = _deployer.CreateStack(new StackTemplate
            {
                StackName = StackName,
                TemplatePath = @".\example-windows-vpc-template.json"
            });
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
        public void ThrowsDeploymentsFailedExceptionForBadDeployments()
        {
            var badRevision = _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = "HelloWorld",
                Version = "BadWebLayerAppSpec",
                LocalDirectory = @".\ExampleRevisions\HelloWorld-BadWebLayerAppSpec"
            });    

            var expectedTail = string.Join("\n",
               @"LifecycleEvent - BeforeInstall",
               @"Script - \before-install.bat",
               @"[stdout]",
               @"[stdout]C:\Windows\system32>echo ""oh noes!"" ",
               @"[stdout]""oh noes!""",
               @"[stdout]",
               @"[stdout]C:\Windows\system32>exit 1 ",
               ""
           );

           try
           {
               _deployer.DeployRelease(badRevision, StackName);
               Assert.Fail("Expected DeploymentsFailedException");
           }
           catch (DeploymentsFailedException e)
           {
               Assert.That(e.FailedInstances.First().Tail, Is.EqualTo(expectedTail));
           }
        }

        [Test]
        public void ThrowsDeploymentsFailedExceptionWhenNoInstancesWereMatched()
        {
            var badRevision = _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = "HelloWorld",
                Version = "Empty",
                LocalDirectory = @".\ExampleRevisions\HelloWorld-Empty"
            });

            try
            {
                _deployer.DeployRelease(badRevision, StackName);
                Assert.Fail("Expected NoInstancesException");
            }
            catch (NoInstancesException)
            {
            }
        }

        [Test]
        public void DeploysCodeToAppropriateInstances()
        {
            var goodRevision = _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = "HelloWorld",
                Version = "GoodRevision",
                LocalDirectory = @".\ExampleRevisions\HelloWorld-1.2.3"
            });

            _deployer.DeployRelease(goodRevision, StackName);

            var publicUrl = _stack.Outputs.First(o => o.Key == "elasticIpUrl").Value;

            var webpageText = Retry.Do(() => Http.Get(publicUrl + "/index.aspx"), TimeSpan.FromSeconds(10));
            Assert.That(webpageText, Is.EqualTo("Hello, world!"));
        }
    }
}