using System;
using System.Collections.Generic;
using System.Linq;

namespace TTC.Deployment.AmazonWebServices
{
    public class DeploymentsFailedException : Exception
    {
        private readonly IEnumerable<FailedInstance> _failedInstances;

        public DeploymentsFailedException(IEnumerable<FailedInstance> failedInstances)
        {
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
                return "Deployment failed: " + string.Join(Environment.NewLine, FailedInstances.Select(i => i.ToString()));
            }
        }
    }

    public class FailedInstance
    {
        private readonly string _instanceId;
        private readonly string _deploymentId;
        private readonly string _tail;

        public FailedInstance(string instanceId, string deploymentId, string tail)
        {
            _instanceId = instanceId;
            _deploymentId = deploymentId;
            _tail = tail;
        }

        public string InstanceId
        {
            get { return _instanceId; }
        }

        public string DeploymentId
        {
            get { return _deploymentId; }
        }

        public string Tail
        {
            get { return _tail; }
        }

        public override string ToString()
        {
            return string.Format("InstanceId: {0}, DeploymentId: {1}{2} Tail: {3}", InstanceId, DeploymentId, Environment.NewLine, Tail);
        }
    }
}