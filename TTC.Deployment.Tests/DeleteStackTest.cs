using System;
using System.IO;
using System.Linq;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class DeleteStackTest
    {
        private AwsConfiguration _awsConfiguration;
        private Deployer _deployer;
        private AmazonCloudFormationClient _cloudFormationClient;
        private readonly string _stackName = "aws-tools-test-stack-" + Guid.NewGuid();

        [Test]
        public void DeletesStack()
        {
            _awsConfiguration = new AwsConfiguration
            {
                IamRolePolicyDocument = Roles.Path("s3-policy-new-bucket.json"),
                AssumeRoleTrustDocument = Roles.Path("code-deploy-trust.json"),
                Bucket = "s3-push-test",
                RoleName = "SomeNewRole",
                Credentials = new TestSuiteCredentials(),
                AwsEndpoint = TestConfiguration.AwsEndpoint
            };
            var templatePath = CloudFormationTemplates.Path("security-group-template.json");

            _cloudFormationClient = new AmazonCloudFormationClient(TestConfiguration.Credentials, TestConfiguration.AwsEndpoint);
            _cloudFormationClient.CreateStack(new CreateStackRequest { StackName = _stackName, TemplateBody = File.ReadAllText(templatePath) });
            WaitForStackToHaveBeenCreated(_stackName);
            _deployer = new Deployer(_awsConfiguration);
            _deployer.DeleteStack(_stackName);
            ExpectStackToHaveBeenDeleted(_stackName);
        }

        private void WaitForStackToHaveBeenCreated(string stackName)
        {
            Console.WriteLine("Waiting for stack {0} to be created...", stackName);
            Retry.Do(() => { ExpectStackToHaveBeenCreated(stackName); }, TimeSpan.FromSeconds(5), 100);
        }

        private void ExpectStackToHaveBeenCreated(string stackName)
        {
            Assert.That(GetStackStatus(stackName), Is.EqualTo(StackStatus.CREATE_COMPLETE));
        }

        private StackStatus GetStackStatus(string stackName)
        {
            try
            {
                return
                    _cloudFormationClient.DescribeStacks(new DescribeStacksRequest {StackName = stackName})
                        .Stacks.First()
                        .StackStatus;
            }
            catch (AmazonCloudFormationException)
            {
                return StackStatus.DELETE_COMPLETE;
            }
        }

        private void ExpectStackToHaveBeenDeleted(string stackName)
        {
            Assert.That(GetStackStatus(stackName), Is.EqualTo(StackStatus.DELETE_COMPLETE));
        }
    }
}
