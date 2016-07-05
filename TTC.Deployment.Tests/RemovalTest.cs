using System;
using System.Linq;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class RemovalTest
    {
        private AwsConfiguration _awsConfiguration;
        private AmazonCloudFormationClient _cloudFormationClient;
        private AmazonEC2Client _ec2Client;

        [Test]
        public void DeletesExistingStack()
        {
            var stackName = "AwsToolsRemovalDeletesExistingStackTest";
            SetUp(stackName);

            var deployer = new Deployer(_awsConfiguration);
            deployer.CreateStack(new StackTemplate
            {
                StackName = stackName,
                TemplatePath = CloudFormationTemplates.Path("example-basic-vpc.template")
            });

            deployer.DeleteStack(stackName);

            var status = StackStatus.DELETE_IN_PROGRESS;
            status = WaitForStackDeleted(status, stackName);

            Assert.AreEqual(StackStatus.DELETE_COMPLETE, status);
        }

        [Test]
        public void DoesNotThrowWhenStackDoesNotExist()
        {
            var stackName = "AwsToolsRemovalDoesNotThrowWhenStackDoesNotExistTest";
            SetUp(stackName);
            var deployer = new Deployer(_awsConfiguration);
            var stack = _cloudFormationClient.ListStacks().StackSummaries.FirstOrDefault(s => s.StackName == stackName);

            Assert.IsTrue(stack == null || stack.StackStatus == StackStatus.DELETE_COMPLETE, "Stack should not exist!");

            TestDelegate act = () => deployer.DeleteStack(stackName);

            Assert.DoesNotThrow(act);
        }


        [Test]
        public void ThrowsWhenStackFailsToDetele()
        {
            var stackName = "AwsToolsRemovalThrowsWhenStackFailsToDeteleTest";
            var securityGroupName = "AwsToolsRemovalThrowsWhenStackFailsToDeteleTest";
            SetUp(stackName);

            var deployer = new Deployer(_awsConfiguration);
            try
            {
                deployer.CreateStack(new StackTemplate
                    {
                        StackName = stackName,
                        TemplatePath = CloudFormationTemplates.Path("example-basic-vpc.template")
                    });
                CreateSecurityGroup(securityGroupName, stackName);

                TestDelegate act = () => deployer.DeleteStack(stackName);

                Assert.Throws<FailedToDeleteStackException>(act);
            }
            finally
            {
                DeleteSecurityGroup(securityGroupName, stackName);
                DeletePreviousTestStack(stackName);
            }
        }

        private void CreateSecurityGroup(string securityGroupName, string stackName)
        {

            var vpcId = GetVpcId(stackName);

            _ec2Client.CreateSecurityGroup(new CreateSecurityGroupRequest
            {
                GroupName = securityGroupName,
                VpcId = vpcId,
                Description = securityGroupName
            });

            SecurityGroup securityGroup = null;
            while (securityGroup == null)
            {
                securityGroup =
                    _ec2Client.DescribeSecurityGroups(new DescribeSecurityGroupsRequest())
                              .SecurityGroups.FirstOrDefault(sg => sg.GroupName.StartsWith(securityGroupName) && sg.VpcId == vpcId);

                if (securityGroup == null)
                    Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }

        private void DeleteSecurityGroup(string securityGroupName, string stackName)
        {
            var vpcId = GetVpcId(stackName);
            var securityGroups =
                _ec2Client.DescribeSecurityGroups(new DescribeSecurityGroupsRequest())
                          .SecurityGroups.Where(sg => sg.GroupName.StartsWith(securityGroupName) && sg.VpcId == vpcId)
                          .ToList();

            if (!securityGroups.Any())
                return;

            foreach (var securityGroup in securityGroups)
            {
                _ec2Client.DeleteSecurityGroup(new DeleteSecurityGroupRequest {GroupId = securityGroup.GroupId});
                WaitForSecurityGroupDeleted(securityGroup);
            }
        }


        private string GetVpcId(string stackName)
        {
            var vpc =
                _cloudFormationClient.DescribeStackResource(new DescribeStackResourceRequest
                {
                    LogicalResourceId = "vpc1",
                    StackName = stackName
                });

            var vpcId = vpc.StackResourceDetail.PhysicalResourceId;
            return vpcId;
        }

        private void WaitForSecurityGroupDeleted(SecurityGroup securityGroup)
        {
            while (securityGroup != null)
            {
                securityGroup =
                    _ec2Client.DescribeSecurityGroups()
                              .SecurityGroups.FirstOrDefault(g => g.GroupId == securityGroup.GroupId);

                if (securityGroup != null)
                    Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }

        private StackStatus WaitForStackDeleted(StackStatus status, string stackName)
        {
            while (status == StackStatus.DELETE_IN_PROGRESS)
            {
                var stack = _cloudFormationClient.ListStacks().StackSummaries.First(s => s.StackName == stackName);
                status = stack.StackStatus;
                if (status == StackStatus.DELETE_IN_PROGRESS) Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            return status;
        }

        private void SetUp(string stackName)
        {
            _awsConfiguration = new AwsConfiguration
            {
                AwsEndpoint = TestConfiguration.AwsEndpoint,
                Credentials = new TestSuiteCredentials(),
                Bucket = "aws-deployment-tools-tests"
            };

            _cloudFormationClient =
                new AmazonCloudFormationClient(new AmazonCloudFormationConfig
                    {
                        RegionEndpoint = _awsConfiguration.AwsEndpoint
                    });

            _ec2Client = new AmazonEC2Client(new AmazonEC2Config {RegionEndpoint = _awsConfiguration.AwsEndpoint});

            DeletePreviousTestStack(stackName);
        }

        private void DeletePreviousTestStack(string stackName)
        {
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, stackName);
        }
    }
}
