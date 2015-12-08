using System;

namespace TTC.Deployment.AmazonWebServices
{
    public class FailedToDeleteStackException : ApplicationException
    {
        public FailedToDeleteStackException(string stackName) : base(string.Format("Failed to delete stack '{0}'", stackName))
        {
        }
    }
}