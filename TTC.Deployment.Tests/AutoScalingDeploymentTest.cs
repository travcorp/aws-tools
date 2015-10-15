using System;
using System.Linq;
using System.Configuration;
using System.IO;
using Amazon;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class AutoScalingDeploymentTest
    {
        private AwsConfiguration _awsConfiguration;
        private Deployer _deployer;
        private Stack _stack;
        const string StackName = "AwsToolsAutoScalingTestVPC2";
        static private bool _hasCreatedStack;

        [SetUp]
        public void EnsureStackExists()
        {
            if (_hasCreatedStack) return;

            ConfigurationManager.AppSettings["AWSProfileName"] = "default";
            _awsConfiguration = new AwsConfiguration
            {
                AssumeRoleTrustDocument = Path.Combine(Environment.CurrentDirectory, "CodeDeployRole", "code-deploy-trust.json"),
                IamRolePolicyDocument = Path.Combine(Environment.CurrentDirectory, "CodeDeployRole", "code-deploy-policy.json"),
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
                TemplatePath = @".\example-windows-vpc-autoscaling-group-template.json"
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
                LocalDirectory = @".\ExampleRevisions\HelloWorld-AutoScaling"
            });

            _deployer.DeployRelease(goodRevision, StackName);

            var publicDnsName = _stack.Outputs.First(o => o.Key == "publicDnsName").Value;
            var homePageUrl = string.Format("http://{0}/index.aspx", publicDnsName);

            Console.WriteLine(homePageUrl);

            var webpageText = Retry.Do(() => Http.Get(homePageUrl), TimeSpan.FromSeconds(10));
            Assert.That(webpageText, Is.EqualTo("Hello, world!"));
        }
    }
}
