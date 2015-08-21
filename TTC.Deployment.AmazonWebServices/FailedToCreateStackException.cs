using System;

namespace TTC.Deployment.AmazonWebServices
{
    public class FailedToCreateStackException : Exception
    {
        public FailedToCreateStackException(string stackName, string status, string statusReason)
            : base(string.Format("Failed to create stack {0}: {1}\n{2}", stackName, status, statusReason))
        {
        }
    }
}