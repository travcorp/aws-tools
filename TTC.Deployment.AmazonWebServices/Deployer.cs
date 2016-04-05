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
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon;

namespace TTC.Deployment.AmazonWebServices
{
    public class Deployer
    {
        private readonly AmazonCodeDeployClient _codeDeployClient;
        private readonly AmazonCloudFormationClient _cloudFormationClient;
        private readonly AmazonS3Client _s3Client;
        private readonly AmazonIdentityManagementServiceClient _iamClient;
        private readonly AmazonAutoScalingClient _autoScalingClient;

        private readonly RegionEndpoint _awsEndpoint;
        private readonly string _bucket;
        private readonly string _assumeRoleTrustDocument;
        private readonly Role _role;
        private readonly string _iamRolePolicyDocument;

        public Deployer(AwsConfiguration awsConfiguration) {
            _awsEndpoint = awsConfiguration.AwsEndpoint;
            _bucket = awsConfiguration.Bucket;
            _assumeRoleTrustDocument = awsConfiguration.AssumeRoleTrustDocument;
            _iamRolePolicyDocument = awsConfiguration.IamRolePolicyDocument;

            AWSCredentials credentials;

            if (isArn(awsConfiguration.RoleName))
            {
                var securityTokenServiceClient = new AmazonSecurityTokenServiceClient(awsConfiguration.AwsEndpoint);

                var assumeRoleResult = securityTokenServiceClient.AssumeRole(new AssumeRoleRequest
                {
                    RoleArn = awsConfiguration.RoleName,
                    DurationSeconds = 3600,
                    RoleSessionName = "Net2User",
                    ExternalId = Guid.NewGuid().ToString()
                });

                Credentials stsCredentials = assumeRoleResult.Credentials;

                SessionAWSCredentials sessionCredentials =
                          new SessionAWSCredentials(stsCredentials.AccessKeyId,
                                                    stsCredentials.SecretAccessKey,
                                                    stsCredentials.SessionToken);

                credentials = sessionCredentials;

                _role = new AssumedRole(assumeRoleResult.AssumedRoleUser);
            }
            else {
                credentials = awsConfiguration.Credentials ?? new EnvironmentAWSCredentials();
            }

            _codeDeployClient = new AmazonCodeDeployClient(
                credentials,
                new AmazonCodeDeployConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.ProxyHost, 
                    ProxyPort = awsConfiguration.ProxyPort
                });

            _cloudFormationClient = new AmazonCloudFormationClient(
                credentials,
                new AmazonCloudFormationConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.ProxyHost, 
                    ProxyPort = awsConfiguration.ProxyPort
                });

            _s3Client = new AmazonS3Client(
                credentials,
                new AmazonS3Config {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.ProxyHost, 
                    ProxyPort = awsConfiguration.ProxyPort
                });

            _iamClient = new AmazonIdentityManagementServiceClient(
                credentials,
                new AmazonIdentityManagementServiceConfig  {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.ProxyHost, 
                    ProxyPort = awsConfiguration.ProxyPort
                });

            _autoScalingClient = new AmazonAutoScalingClient(
                credentials,
                new AmazonAutoScalingConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint,
                    ProxyHost = awsConfiguration.ProxyHost,
                    ProxyPort = awsConfiguration.ProxyPort
                });
        }

        private bool isArn(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName)) {
                return false;
            }

            return roleName.StartsWith("arn:", StringComparison.OrdinalIgnoreCase);
        }

        public Stack CreateStack(StackTemplate stackTemplate)
        {
            var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, stackTemplate.TemplatePath);

            var stackName = stackTemplate.StackName;

            _cloudFormationClient.CreateStack(new CreateStackRequest {
                StackName = stackName,
                TemplateBody = File.ReadAllText(templatePath),
                Capabilities = new List<string> { Capability.CAPABILITY_IAM },
                DisableRollback = false,
                Parameters = GetStackParameters(stackTemplate.ParameterPath)
            });

            WaitForStack(stackName);
            var stack =
                _cloudFormationClient.DescribeStacks(new DescribeStacksRequest {StackName = stackName}).Stacks.First();

            return new Stack(stackName, stack.Outputs.ToDictionary(x => x.OutputKey, x => x.OutputValue));
        }

        private List<Parameter> GetStackParameters(string parameterPath)
        {
            if (string.IsNullOrWhiteSpace(parameterPath))
                return null;

            return new StackParameterReader(parameterPath).Read();
        }

        public void DeleteStack(string stackName)
        {
            _cloudFormationClient.DeleteStack(new DeleteStackRequest {StackName = stackName});
            WaitForStackDeleted(stackName);
        }

        private void WaitForStack(string stackName)
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
                throw new FailedToCreateStackException(stackName, _awsEndpoint, status.Value, statusReason, eventsResponse.StackEvents);
            }
        }

        private void WaitForStackDeleted(string stackName)
        {
            var status = StackStatus.DELETE_IN_PROGRESS;
            string statusReason = null;
            StackSummary stack = null;

            while (status == StackStatus.DELETE_IN_PROGRESS)
            {
                stack = _cloudFormationClient.ListStacks().StackSummaries.FirstOrDefault(s => s.StackName == stackName);
                if (stack == null) break;

                status = stack.StackStatus;
                statusReason = stack.StackStatusReason;
                if (status == StackStatus.DELETE_IN_PROGRESS) Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            if (stack != null && status != StackStatus.DELETE_COMPLETE)
            {
                var eventsResponse = _cloudFormationClient.DescribeStackEvents(new DescribeStackEventsRequest { StackName = stackName });
                throw new FailedToDeleteStackException(stackName, _awsEndpoint, status.Value, statusReason, eventsResponse.StackEvents);
            }
        }

        public Release PushRevision(ApplicationSetRevision applicationSetRevision)
        {
            var subdirectories = Directory
                .GetDirectories(applicationSetRevision.LocalDirectory)
                .Select(sd => new DirectoryInfo(sd));

            var bundles = subdirectories
                .Select(subdirectory => 
                    PushBundleForSubdirectory(applicationSetRevision, subdirectory, _bucket)).ToArray();

            return new Release(
                applicationSetRevision.ApplicationSetName, 
                applicationSetRevision.Version, 
                bundles);
        }

        public void DeployRelease(Release release, string stackName, string codeDeployRoleName)
        {
            var role = GetOrCreateCodeDeployRole(codeDeployRoleName);

            var deploymentIds = release
                .Bundles
                .Select(bundle => bundle
                    .DeployToStack(
                        _codeDeployClient, 
                        _iamClient, 
                        _autoScalingClient, 
                        stackName,
                        role).DeploymentId).ToList();

            WaitForBundlesToDeploy(deploymentIds);
        }

        private Bundle PushBundleForSubdirectory(ApplicationSetRevision applicationSetRevision, DirectoryInfo subdirectory, string bucket)
        {
            var bundle = new Bundle(
                applicationSetRevision.ApplicationSetName, 
                subdirectory, 
                applicationSetRevision.Version, 
                bucket, 
                null);

            bundle.Push(_s3Client, _codeDeployClient);

            return bundle;
        }

        private Role GetOrCreateCodeDeployRole(string codeDeployRoleName)
        {
            if (string.IsNullOrWhiteSpace(_assumeRoleTrustDocument))
            {
                return null;
            }

            Role role;
            try
            {
                var response = _iamClient.CreateRole(new CreateRoleRequest
                {
                    RoleName = codeDeployRoleName,
                    AssumeRolePolicyDocument = File.ReadAllText(_assumeRoleTrustDocument)
                });

                role = response.Role;
            }
            catch (EntityAlreadyExistsException)
            {
                role =
                    _iamClient.GetRole(new GetRoleRequest { RoleName = codeDeployRoleName }).Role;
            }
            
            _iamClient.PutRolePolicy(new PutRolePolicyRequest
            { 
                RoleName = role.RoleName,
                PolicyName = "s3-releases",
                PolicyDocument = File.ReadAllText(_iamRolePolicyDocument)
            });

            return role;
        }

        private void WaitForBundlesToDeploy(List<string> deploymentIds)
        {
            var  deploymentsInfo = new List<DeploymentInfo>() ;
            var inProgress = deploymentIds.Count;
            while (inProgress > 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(10));
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

        private class AssumedRole : Role
        {
            public AssumedRole(AssumedRoleUser assumedRoleUser)
            {
                this.Path = "/";
                this.Arn = assumedRoleUser.Arn;
                this.CreateDate = DateTime.Now;
                this.RoleId = assumedRoleUser.AssumedRoleId;
                this.RoleName = assumedRoleUser.Arn.Split('/')[1];
            }
        }
    }
}