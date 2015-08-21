using System.Linq;
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace TTC.Deployment.Tests
{
    internal class StackHelper
    {
        public static void DeleteStack(RegionEndpoint awsEndpoint, string stackName)
        {
            var cloudFormationClient = new AmazonCloudFormationClient(awsEndpoint);
            try
            {
                cloudFormationClient.DeleteStack(new DeleteStackRequest { StackName = stackName });
                var testStackStatus = StackStatus.DELETE_IN_PROGRESS;
                while (testStackStatus == StackStatus.DELETE_IN_PROGRESS)
                {
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