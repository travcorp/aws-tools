using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.CloudFormation.Model;

namespace TTC.Deployment.AmazonWebServices
{
    public class FailedToDeleteStackException : Exception
    {
        public FailedToDeleteStackException(string stackName, RegionEndpoint awsEndpoint, string status, string statusReason, IEnumerable<StackEvent> stackEvents)
            : base(string.Format("Failed to delete stack {0} (in {1}): {2}\n{3}\n\nEVENTS:\n\n{4}",
                stackName, awsEndpoint, status, statusReason, string.Join(Environment.NewLine, stackEvents.Select(e => e.ResourceType + ": " + e.ResourceStatusReason).ToArray())))
        {
        }
    }
}