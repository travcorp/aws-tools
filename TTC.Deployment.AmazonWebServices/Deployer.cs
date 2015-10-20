using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Amazon.AutoScaling;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.CodeDeploy;
using Amazon.CodeDeploy.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.S3;

namespace TTC.Deployment.AmazonWebServices
{
    public class Deployer
    {
        private readonly AmazonCodeDeployClient _codeDeployClient;
        private readonly AmazonCloudFormationClient _cloudFormationClient;
        private readonly AmazonS3Client _s3Client;
        private readonly AmazonIdentityManagementServiceClient _iamClient;
        private readonly AwsConfiguration _awsConfiguration;
        private readonly AmazonAutoScalingClient _autoScalingClient;

        public Deployer(AwsConfiguration awsConfiguration) {
            _awsConfiguration = awsConfiguration;

            _codeDeployClient = new AmazonCodeDeployClient(
                new AmazonCodeDeployConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.Proxy.Host, 
                    ProxyPort = awsConfiguration.Proxy.Port
                });

            _cloudFormationClient = new AmazonCloudFormationClient(
                new AmazonCloudFormationConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.Proxy.Host, 
                    ProxyPort = awsConfiguration.Proxy.Port
                });

            _s3Client = new AmazonS3Client(
                new AmazonS3Config {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.Proxy.Host, 
                    ProxyPort = awsConfiguration.Proxy.Port
                });

            _iamClient = new AmazonIdentityManagementServiceClient(
                new AmazonIdentityManagementServiceConfig  {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.Proxy.Host, 
                    ProxyPort = awsConfiguration.Proxy.Port
                });

            _autoScalingClient = new AmazonAutoScalingClient(
                new AmazonAutoScalingConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint,
                    ProxyHost = awsConfiguration.Proxy.Host,
                    ProxyPort = awsConfiguration.Proxy.Port
                });
        }

        public Stack CreateStack(StackTemplate stackTemplate)
        {
            var templatePath = Path.Combine(Environment.CurrentDirectory, stackTemplate.TemplatePath);

            var stackName = stackTemplate.StackName;

            _cloudFormationClient.CreateStack(new CreateStackRequest {
                StackName = stackName,
                TemplateBody = File.ReadAllText(templatePath),
                Capabilities = new List<string> { Capability.CAPABILITY_IAM },
                DisableRollback = true
            });

            WaitForStack(stackName);
            var stack =
                _cloudFormationClient.DescribeStacks(new DescribeStacksRequest {StackName = stackName}).Stacks.First();

            return new Stack
            {
                StackName = stackName,
                Outputs = stack.Outputs.ToDictionary(x => x.OutputKey, x => x.OutputValue)
            };
        }

        void WaitForStack(string stackName)
        {
            var status = StackStatus.CREATE_IN_PROGRESS;
            string statusReason = null;
            while (status == StackStatus.CREATE_IN_PROGRESS)
            {
                var stack = _cloudFormationClient.DescribeStacks(new DescribeStacksRequest { StackName = stackName }).Stacks.First();
                status = stack.StackStatus;
                statusReason = stack.StackStatusReason;
                if (status == StackStatus.CREATE_IN_PROGRESS) Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            if (status != StackStatus.CREATE_COMPLETE)
            {
                var eventsResponse = _cloudFormationClient.DescribeStackEvents(new DescribeStackEventsRequest { StackName = stackName });
                throw new FailedToCreateStackException(stackName, _awsConfiguration.AwsEndpoint, status.Value, statusReason, eventsResponse.StackEvents);
            }
        }

        public Release PushRevision(ApplicationSetRevision applicationSetRevision)
        {
            EnsureCodeDeployRoleExists();
            var subdirectories = Directory.GetDirectories(applicationSetRevision.LocalDirectory).Select(sd => new DirectoryInfo(sd));
            var bundles = subdirectories.Select(subdirectory => PushBundleForSubdirectory(applicationSetRevision, subdirectory, _awsConfiguration.Bucket)).ToArray();
            return new Release(applicationSetRevision.ApplicationSetName, applicationSetRevision.Version, bundles);
        }

        public void DeployRelease(Release release, string stackName)
        {
            EnsureCodeDeployRoleExists();
            var deploymentIds = new List<string>();
            foreach (var bundle in release.Bundles)
            {
                deploymentIds.Add(bundle.DeployToStack(_codeDeployClient, _iamClient, _autoScalingClient, stackName, _awsConfiguration.RoleName).DeploymentId);
            }
            WaitForBundlesToDeploy(deploymentIds);
        }

        Bundle PushBundleForSubdirectory(ApplicationSetRevision applicationSetRevision, DirectoryInfo subdirectory, string bucket)
        {
            var bundle = new Bundle(applicationSetRevision.ApplicationSetName, subdirectory, applicationSetRevision.Version, bucket, null);
            bundle.Push(_s3Client, _codeDeployClient);
            return bundle;
        }

        void EnsureCodeDeployRoleExists()
        {
            try
            {
                _iamClient.CreateRole(new CreateRoleRequest
                {
                    RoleName = _awsConfiguration.RoleName,
                    AssumeRolePolicyDocument = File.ReadAllText(_awsConfiguration.AssumeRoleTrustDocument)
                });
            }
            catch (EntityAlreadyExistsException)
            {
                return;
            }

            _iamClient.PutRolePolicy(new PutRolePolicyRequest
            {
                RoleName = _awsConfiguration.RoleName,
                PolicyName = "s3-releases",
                PolicyDocument = File.ReadAllText(_awsConfiguration.IamRolePolicyDocument)
            });
        }

        void WaitForBundlesToDeploy(List<string> deploymentIds)
        {
            var  deploymentsInfo = new List<DeploymentInfo>() ;
            var inProgress = deploymentIds.Count;
            while (inProgress > 0)
            {
                deploymentsInfo = _codeDeployClient.BatchGetDeployments(new BatchGetDeploymentsRequest { DeploymentIds = deploymentIds }).DeploymentsInfo;
                inProgress = deploymentsInfo.Count(i => i.Status == DeploymentStatus.InProgress || i.Status == DeploymentStatus.Created);
            }
            var failedDeployments = deploymentsInfo.Where(i => i.Status != DeploymentStatus.Succeeded).ToArray();
            if (failedDeployments.Any()) {
                throw new DeploymentsFailedException(failedDeployments, GetFailedInstancesFor(failedDeployments));
            }
        }

        private FailedInstance[] GetFailedInstancesFor(DeploymentInfo[] failedDeployments)
        {
            var allFailedInstances = new List<FailedInstance>();
            foreach (var deployment in failedDeployments)
            {
                var instancesResult = _codeDeployClient.ListDeploymentInstances(new ListDeploymentInstancesRequest
                {
                    DeploymentId = deployment.DeploymentId,
                    InstanceStatusFilter = new List<string> { "Failed" }
                });

                var tmpDeployment = deployment;
                var awsInstances = instancesResult.InstancesList.Select(id =>
                        _codeDeployClient.GetDeploymentInstance(new GetDeploymentInstanceRequest
                        {
                            InstanceId = id,
                            DeploymentId = tmpDeployment.DeploymentId
                        }));

                allFailedInstances.AddRange(awsInstances.Select(i => {
                    var firstFailEvent = i.InstanceSummary.LifecycleEvents.FirstOrDefault(lce => lce.Status == LifecycleEventStatus.Failed);
                    var tail = firstFailEvent == null ? string.Empty : firstFailEvent.Diagnostics.LogTail;
                    return new FailedInstance(i.InstanceSummary.InstanceId, deployment.DeploymentId, tail);
                }));
            }

            return allFailedInstances.ToArray();
        }
    }
}
