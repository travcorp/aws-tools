using System;
using System.Linq;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class ProvisioningTest
    {
        private AwsConfiguration _awsConfiguration;
        private AmazonCloudFormationClient _cloudFormationClient;
        const string StackName = "AwsToolsProvisioningTestVPC";

        [SetUp]
        public void SetUp()
        {
            _awsConfiguration = new AwsConfiguration
            {
                AwsEndpoint = TestConfiguration.AwsEndpoint,
                Credentials = new TestSuiteCredentials()
            };
            _cloudFormationClient = new AmazonCloudFormationClient(
                new AmazonCloudFormationConfig {
                    RegionEndpoint = _awsConfiguration.AwsEndpoint
                }
            );
            DeletePreviousTestStack();
        }

        [TearDown]
        public void TearDown()
        {
            DeletePreviousTestStack();
        }

        [Test]
        public void CreatesVirtualPrivateCloudWithWindowsMachines()
        {
            var deployer = new Deployer(_awsConfiguration);
        
            deployer.CreateStack(new StackTemplate
            {
                StackName = StackName,
                TemplatePath = CloudFormationTemplates.Path("example-windows-vpc.template")
            });

            var status = StackStatus.CREATE_IN_PROGRESS;
            while (status == StackStatus.CREATE_IN_PROGRESS)
            {
                var stack = _cloudFormationClient.DescribeStacks(new DescribeStacksRequest { StackName = StackName }).Stacks.First();
                status = stack.StackStatus;
                if (status == StackStatus.CREATE_IN_PROGRESS) Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            Assert.AreEqual(status, StackStatus.CREATE_COMPLETE);
        }

    
        private void DeletePreviousTestStack()
        {
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, StackName);
        }
    }
}
