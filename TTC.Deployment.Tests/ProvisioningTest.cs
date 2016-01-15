using System;
using System.Collections.Generic;
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
    public class ProvisioningTest
    {
        private AwsConfiguration _awsConfiguration;
        private AmazonCloudFormationClient _cloudFormationClient;
        private AmazonEC2Client _ec2Client;

        [Test]
        public void CreatesVirtualPrivateCloudWithWindowsMachines()
        {
            string stackName = "ProvisioningTest-CreatesVirtualPrivateCloudWithWindowsMachines";
            SetUp(stackName);

            try
            {
                var deployer = new Deployer(_awsConfiguration);

                deployer.CreateStack(new StackTemplate
                    {
                        StackName = stackName,
                        TemplatePath = CloudFormationTemplates.Path("example-windows-vpc.template")
                    });

                var status = StackStatus.CREATE_IN_PROGRESS;
                while (status == StackStatus.CREATE_IN_PROGRESS)
                {
                    var stack =
                        _cloudFormationClient.DescribeStacks(new DescribeStacksRequest {StackName = stackName})
                                             .Stacks.First();
                    status = stack.StackStatus;
                    if (status == StackStatus.CREATE_IN_PROGRESS) Thread.Sleep(TimeSpan.FromSeconds(10));
                }

                Assert.AreEqual(status, StackStatus.CREATE_COMPLETE);
            }
            finally
            {
                DeleteTestStack(stackName);
            }
        }

        [Test]
        public void CreatesStackWithParameters()
        {
            var stackName = "ProvisioningTest-CreatesStackWithParameters";
            SetUp(stackName);
            try
            {
                var deployer = new Deployer(_awsConfiguration);

                deployer.CreateStack(new StackTemplate
                    {
                        StackName = stackName,
                        TemplatePath = CloudFormationTemplates.Path("example-parameters.template"),
                        ParameterPath = CloudFormationTemplates.Path("example-parameters.parameters")
                    });

                var status = StackStatus.CREATE_IN_PROGRESS;
                while (status == StackStatus.CREATE_IN_PROGRESS)
                {
                    var stack =
                        _cloudFormationClient.DescribeStacks(new DescribeStacksRequest {StackName = stackName})
                                             .Stacks.First();
                    status = stack.StackStatus;
                    if (status == StackStatus.CREATE_IN_PROGRESS) Thread.Sleep(TimeSpan.FromSeconds(10));
                }

                var vpcId =
                    _cloudFormationClient.DescribeStackResource(new DescribeStackResourceRequest
                        {
                            LogicalResourceId = "vpc1",
                            StackName = stackName
                        }).StackResourceDetail.PhysicalResourceId;
                var vpc = _ec2Client.DescribeVpcs(new DescribeVpcsRequest {VpcIds = new List<string> {vpcId}}).Vpcs.First();
                var vpcName = vpc.Tags.First(tag => tag.Key == "Name").Value;

                Assert.AreEqual(status, StackStatus.CREATE_COMPLETE);
                Assert.AreEqual("TestVpcName", vpcName);
            }
            finally
            {
                DeleteTestStack(stackName);
            }
        }


        private void SetUp(string stackName)
        {
            _awsConfiguration = new AwsConfiguration
            {
                AwsEndpoint = TestConfiguration.AwsEndpoint,
                Credentials = new TestSuiteCredentials()
            };

            _cloudFormationClient = new AmazonCloudFormationClient(
                new AmazonCloudFormationConfig
                {
                    RegionEndpoint = _awsConfiguration.AwsEndpoint
                }
            );

            _ec2Client = new AmazonEC2Client(new AmazonEC2Config { RegionEndpoint = _awsConfiguration.AwsEndpoint });

            DeleteTestStack(stackName);
        }

        private void DeleteTestStack(string stackName)
        {
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, stackName);
        }
    }
}
