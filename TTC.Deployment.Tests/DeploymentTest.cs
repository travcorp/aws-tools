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
        const string StackName = "AwsToolsTestVPC";
        static private bool _hasCreatedStack;

        [SetUp]
        public void EnsureStackExists()
        {
            if (_hasCreatedStack) return;

            ConfigurationManager.AppSettings["AWSProfileName"] = "default";
            _awsConfiguration = new AwsConfiguration
            {
                AssumeRoleTrustDocument = Path.Combine(Environment.CurrentDirectory, "Roles", "code-deploy-trust.json"),
                IamRolePolicyDocument = Path.Combine(Environment.CurrentDirectory, "Roles", "code-deploy-policy.json"),
                Bucket = "aws-deployment-tools-tests",
                RoleName = "CodeDeployRole",
                AwsEndpoint = RegionEndpoint.USWest2,
                Proxy = new AwsProxy()
            };

            _deployer = new Deployer(_awsConfiguration);

            DeletePreviousTestStack();
            _stack = _deployer.CreateStack(new StackTemplate
            {
                StackName = StackName,
                TemplatePath = @".\example-windows-vpc-template.json"
            });
            _hasCreatedStack = true;
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
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
                Assert.Fail("Expected DeploymentsFailedException");
            }
            catch (DeploymentsFailedException e)
            {
                Assert.That(e.FailedInstances.Count(), Is.EqualTo(0));
                Assert.That(e.Message, Contains.Substring("No instances found"));
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

            var publicDnsName = _stack.Outputs.First(o => o.Key == "publicDnsName").Value;
            var homePageUrl = string.Format("http://{0}/index.aspx", publicDnsName);

            Console.WriteLine(homePageUrl);

            var webpageText = Retry.Do(() => { var html = Http.Get(homePageUrl); return html; }, TimeSpan.FromSeconds(10));

            Assert.That(webpageText, Is.EqualTo("Hello, world!"));
        }
    }
}