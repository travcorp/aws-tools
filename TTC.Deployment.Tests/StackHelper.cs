using System;
using System.Linq;
using System.Threading;
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.CodeDeploy;
using Amazon.CodeDeploy.Model;

namespace TTC.Deployment.Tests
{
    internal class StackHelper
    {
        public static void DeleteStack(RegionEndpoint awsEndpoint, string stackName)
        {
            var codeDeployClient = new AmazonCodeDeployClient(awsEndpoint);
            var apps = codeDeployClient.ListApplications().Applications.Where(name => name.StartsWith("HelloWorld"));
            foreach (var app in apps) {
                codeDeployClient.DeleteApplication(new DeleteApplicationRequest {ApplicationName = app});
            }

            var cloudFormationClient = new AmazonCloudFormationClient(awsEndpoint);
            try
            {
                cloudFormationClient.DeleteStack(new DeleteStackRequest { StackName = stackName });
                var testStackStatus = StackStatus.DELETE_IN_PROGRESS;
                while (testStackStatus == StackStatus.DELETE_IN_PROGRESS)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                    var stacksStatus =
                        cloudFormationClient.DescribeStacks(new DescribeStacksRequest { StackName = stackName });
                    testStackStatus = stacksStatus.Stacks.First(s => s.StackName == stackName).StackStatus;
                }
            }
            catch (AmazonCloudFormationException)
            {
            }
        }
    }
}