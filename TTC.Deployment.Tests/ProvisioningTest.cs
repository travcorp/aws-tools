﻿using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class ProvisioningTest
    {
        private AwsConfiguration _awsConfiguration;
        private AmazonCloudFormationClient _cloudFormationClient;
        const string StackName = "TestEnv1";

        [SetUp]
        public void SetUp()
        {
            ConfigurationManager.AppSettings["AWSProfileName"] = "default";
           
            _awsConfiguration = new AwsConfiguration
            {
                AssumeRoleTrustDocument = Path.Combine(Environment.CurrentDirectory, "CodeDeployRole", "code-deploy-trust.json"),
                IamRolePolicyDocument = Path.Combine(Environment.CurrentDirectory, "CodeDeployRole", "code-deploy-policy.json"),
                Bucket = "test-releases",
                RoleName = "CodeDeployRole",
                AwsEndpoint =  RegionEndpoint.USEast1,
                Proxy = new AwsProxy(),
                StackOutputFile = "StackOutput.json"
            };
            _cloudFormationClient = new AmazonCloudFormationClient(new AmazonCloudFormationConfig { RegionEndpoint = _awsConfiguration.AwsEndpoint });
            DeletePreviousTestStack();
        }

        [TearDown]
        public void TearDown()
        {
            DeletePreviousTestStack();
        }

        [Test]
        public void CreatesCloud()
        {
            var deployer = new Deployer(_awsConfiguration);
        
            deployer.CreateStack(new StackTemplate
            {
                StackName = StackName,
                TemplatePath = @".\example-windows-vpc-template.json"
            });

            var status = StackStatus.CREATE_IN_PROGRESS;
            while (status == StackStatus.CREATE_IN_PROGRESS)
            {
                var stack = _cloudFormationClient.DescribeStacks(new DescribeStacksRequest { StackName = StackName }).Stacks.First();
                status = stack.StackStatus;
                if (status == StackStatus.CREATE_IN_PROGRESS) Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            Assert.AreEqual(status, StackStatus.CREATE_COMPLETE);

            using (var file = File.OpenText(_awsConfiguration.StackOutputFile))
            {
                var outputsInFile = JObject.Parse(file.ReadToEnd());

                foreach (var outputInFile in outputsInFile)
                {
                    if (outputInFile.Key == "Output1")
                    {
                        Assert.AreEqual("Value1", outputInFile.Value.ToString());
                    }
                }
            }
        }

        private void DeletePreviousTestStack()
        {
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, StackName);
        }
    }
}
