using System;

namespace TTC.Deployment.AmazonWebServices
{
    public class NoInstancesException : Exception
    {
        private readonly string _deploymentGroupName;

        public NoInstancesException(string deploymentGroupName)
        {
            _deploymentGroupName = deploymentGroupName;
        }

        public override string Message
        {
            get
            {
                return string.Format("Found no instances to deploy to (in deployment group '{0}'). " +
                                     "Your application set directory should include subdirectories " +
                                     "corresponding to tags specified in your VPC template", _deploymentGroupName);
            }
        }
    }


}
