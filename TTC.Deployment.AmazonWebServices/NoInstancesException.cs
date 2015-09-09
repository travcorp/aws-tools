using System;

namespace TTC.Deployment.AmazonWebServices
{
    public class NoInstancesException : Exception
    {
        public override string Message
        {
            get
            {
                return "Found no instances to deploy to. Your application set directory should include subdirectories corresponding to instance tags specified in your VPC template";
            }
        }
    }


}
