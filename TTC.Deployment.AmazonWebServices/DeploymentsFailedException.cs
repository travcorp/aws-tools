using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CodeDeploy.Model;

namespace TTC.Deployment.AmazonWebServices
{
    public class DeploymentsFailedException : ApplicationException
    {
        private readonly DeploymentInfo[] _failedDeployments;
        private readonly FailedInstance[] _failedInstances;

        public DeploymentsFailedException(DeploymentInfo[] failedDeployments, FailedInstance[] failedInstances)
        {
            _failedDeployments = failedDeployments;
            _failedInstances = failedInstances;
        }

        public IEnumerable<FailedInstance> FailedInstances
        {
            get { return _failedInstances; }
        }

        public override string Message
        {
            get
            {
                return string.Format("The following deployments failed:\n\n{0}", string.Join("\n\n", _failedDeployments.Select(DescribeFailedDeployment)));
            }
        }

        private string DescribeFailedDeployment(DeploymentInfo deploymentInfo)
        {
            return string.Format("{0} ({1})\n{2}", deploymentInfo.DeploymentGroupName, deploymentInfo.DeploymentId, DescribeFailedInstances(deploymentInfo.DeploymentId));
        }

        private string DescribeFailedInstances(string deploymentId)
        {
            var failedInstances = _failedInstances.Where(fi => fi.DeploymentId == deploymentId).ToArray();
            return failedInstances.Any() ? string.Join("\n\n", failedInstances.Select(f => f.ToString())) : "No instances found";
        }
    }
}