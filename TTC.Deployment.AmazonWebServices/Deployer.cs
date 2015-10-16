﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.CloudSearchDomain;
using Amazon.CodeDeploy;
using Amazon.CodeDeploy.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Amazon.AutoScaling;
using Amazon.SecurityToken.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TTC.Deployment.AmazonWebServices
{
    public class Deployer
    {
        private readonly AmazonCodeDeployClient _codeDeployClient;
        private readonly AmazonCloudFormationClient _cloudFormationClient;
        private readonly AmazonS3Client _s3Client;
        private readonly AmazonIdentityManagementServiceClient _iamClient;
        private readonly AwsConfiguration _awsConfiguration;
        private readonly AmazonSecurityTokenServiceClient _securityTokenServiceClient;
        private readonly AmazonAutoScalingClient _autoScalingClient;

        public Deployer(AwsConfiguration awsConfiguration)
        {
            _awsConfiguration = awsConfiguration;

            _securityTokenServiceClient = new AmazonSecurityTokenServiceClient(awsConfiguration.AwsEndpoint);
            
            // Either assume role or use credentials from the key store
            AWSCredentials tempCredentials;
            if (!String.IsNullOrWhiteSpace(awsConfiguration.AssumeRoleName))
                tempCredentials = _securityTokenServiceClient.AssumeRole(new AssumeRoleRequest {DurationSeconds = 3600, RoleArn = awsConfiguration.AssumeRoleName, RoleSessionName = "AssumingRoleForDeployment"}).Credentials;
            else
                tempCredentials = new EnvironmentAWSCredentials();

            _codeDeployClient = new AmazonCodeDeployClient(
                tempCredentials,
                new AmazonCodeDeployConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.Proxy.Host, 
                    ProxyPort = awsConfiguration.Proxy.Port
                });

            _cloudFormationClient = new AmazonCloudFormationClient(
                tempCredentials,
                new AmazonCloudFormationConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.Proxy.Host, 
                    ProxyPort = awsConfiguration.Proxy.Port
                });

            _s3Client = new AmazonS3Client(
                tempCredentials,
                new AmazonS3Config {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.Proxy.Host, 
                    ProxyPort = awsConfiguration.Proxy.Port
                });

            _iamClient = new AmazonIdentityManagementServiceClient(
                tempCredentials,
                new AmazonIdentityManagementServiceConfig  {
                    RegionEndpoint = awsConfiguration.AwsEndpoint, 
                    ProxyHost = awsConfiguration.Proxy.Host, 
                    ProxyPort = awsConfiguration.Proxy.Port
                });

            _autoScalingClient = new AmazonAutoScalingClient(
                tempCredentials,
                new AmazonAutoScalingConfig {
                    RegionEndpoint = awsConfiguration.AwsEndpoint,
                    ProxyHost = awsConfiguration.Proxy.Host,
                    ProxyPort = awsConfiguration.Proxy.Port
                });
        }

        public Stack CreateStack(StackTemplate stackTemplate)
        {
            var stackName = stackTemplate.StackName;

            var parameters = GetStackParameters();
            foreach (var param in parameters)
                Console.WriteLine(param.ParameterKey + " : " + param.ParameterValue);

            if (IsTemplateOnS3(stackTemplate.TemplatePath))
            {
                _cloudFormationClient.CreateStack(new CreateStackRequest
                {
                    StackName = stackName,
                    Capabilities = new List<string> { Capability.CAPABILITY_IAM },
                    DisableRollback = true,
                    Parameters = parameters,
                    TemplateURL = stackTemplate.TemplatePath
                });
            }
            else
            {
                var templatePath = Path.Combine(Environment.CurrentDirectory, stackTemplate.TemplatePath);
                string templateBody = File.ReadAllText(templatePath);

                _cloudFormationClient.CreateStack(new CreateStackRequest
                {
                    StackName = stackName,
                    TemplateBody = templateBody,
                    Capabilities = new List<string> { Capability.CAPABILITY_IAM },
                    DisableRollback = true,
                    Parameters = parameters
                });
            }

            WaitForStack(stackName);
            var stack = _cloudFormationClient.DescribeStacks(new DescribeStacksRequest {StackName = stackName}).Stacks.First();

            SaveStackOutput(stack);

            return new Stack
            {
                StackName = stackName,
                Outputs = stack.Outputs.ToDictionary(x => x.OutputKey, x => x.OutputValue)
            };
        }

        bool IsTemplateOnS3(string templatePath)
        {
            Uri uri;
            if (!Uri.TryCreate(templatePath, UriKind.Absolute, out uri))
                return false;

            return true;
        }

        private List<Parameter> GetStackParameters()
        {
            if (String.IsNullOrWhiteSpace(_awsConfiguration.ParametersPath))
                return null;

            List<Parameter> returnList = new List<Parameter>();

            var webClient = new WebClient();
            using (var stream = new MemoryStream(webClient.DownloadData(_awsConfiguration.ParametersPath)))
            using (var sr = new StreamReader(stream))
            {
                var parametersInFile = JObject.Parse(sr.ReadToEnd());

                foreach (var paramInFile in parametersInFile)
                {
                    var parameter = new Parameter {ParameterKey = paramInFile.Key, ParameterValue = paramInFile.Value.ToString()};
                    returnList.Add(parameter);
                }
            }


            return returnList;
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
                throw new FailedToCreateStackException(stackName, status.Value, statusReason);
            }
        }

        void SaveStackOutput(Amazon.CloudFormation.Model.Stack stack)
        {
            if (stack.Outputs == null || stack.Outputs.Count == 0)
                return;

            // Check is same file already exists in the bucket
            var objectsInBucket = _s3Client.ListObjects(new ListObjectsRequest {BucketName = _awsConfiguration.StackOutputBucket}).S3Objects;
            if (objectsInBucket.Any(objectInBucket => objectInBucket.Key == _awsConfiguration.StackOutputFile))
                throw new ApplicationException(string.Format("S3 object with key {0} already exists in the bucket {1}", _awsConfiguration.StackOutputFile, _awsConfiguration.StackOutputBucket));

            var jsonOutput = new JObject();
            foreach (var output in stack.Outputs)
            {
                jsonOutput.Add(new JProperty(output.OutputKey, output.OutputValue));
            }

            var stream = new MemoryStream();

            // Write json
            using(var sw = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(sw))
            {
                jsonOutput.WriteTo(writer);
                sw.Flush();
                stream.Position = 0;

                // Put json to S3
                PutObjectResponse s3response;
                using (var sr = new StreamReader(stream))
                {
                    s3response = _s3Client.PutObject(new PutObjectRequest
                    {
                        BucketName = _awsConfiguration.StackOutputBucket,
                        Key = _awsConfiguration.StackOutputFile,
                        ContentBody = sr.ReadToEnd()
                    });
                }

                if (s3response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                    throw new ApplicationException(string.Format("S3 put object was not successfull - HttpStatusCode={0}", s3response.HttpStatusCode));
            }
        }

        public Release PushRevision(ApplicationSetRevision applicationSetRevision)
        {
            EnsureCodeDeployRoleExists();
            var subdirectories = Directory.GetDirectories(applicationSetRevision.LocalDirectory);
            var bundles = subdirectories.Select(subdirectory => PushBundleForSubdirectory(applicationSetRevision, subdirectory, _awsConfiguration.Bucket)).ToArray();

            return new Release(applicationSetRevision.ApplicationSetName, applicationSetRevision.Version, bundles);
        }

        public void DeployRelease(Release release, string stackName)
        {
            EnsureCodeDeployRoleExists();
            var deploymentIds = new List<string>();
            foreach (var bundle in release.Bundles)
            {
                var deploymentGroupName = stackName + "_" + bundle.BundleName;
                EnsureDeploymentGroupExistsForApplicationBundleAndEnvironment(bundle.ApplicationSetName, bundle.BundleName, deploymentGroupName);
                var deploymentResponse = DeployBundleToEnvironment(bundle, deploymentGroupName);
                deploymentIds.Add(deploymentResponse.DeploymentId);
            }
            WaitForBundlesToDeploy(deploymentIds);
        }

        Bundle PushBundleForSubdirectory(ApplicationSetRevision applicationSetRevision, string subdirectory, string bucket)
        {
            var dir = Path.GetFileNameWithoutExtension(subdirectory);
            return new Bundle(applicationSetRevision.ApplicationSetName, dir, applicationSetRevision.Version, bucket,
                PushDirectoryAsCodeDeployApplication(subdirectory, applicationSetRevision.ApplicationSetName, dir, applicationSetRevision.Version, bucket));
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

        void EnsureDeploymentGroupExistsForApplicationBundleAndEnvironment(string applicationSetName, string bundleName, string deploymentGroupName)
        {
            var getRoleResponse = _iamClient.GetRole(new GetRoleRequest
            {
                RoleName = _awsConfiguration.RoleName
            });

            try
            {
                if (String.IsNullOrWhiteSpace(_awsConfiguration.DeployToAutoScalingGroups) 
                    || _awsConfiguration.DeployToAutoScalingGroups.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                {
                    _codeDeployClient.CreateDeploymentGroup(new CreateDeploymentGroupRequest
                    {
                        ApplicationName = CodeDeployApplicationNameForApplicationSetAndBundle(applicationSetName, bundleName),
                        DeploymentGroupName = deploymentGroupName,
                        ServiceRoleArn = getRoleResponse.Role.Arn,
                        Ec2TagFilters = new List<EC2TagFilter>
                        {
                            new EC2TagFilter
                            {
                                Type = EC2TagFilterType.KEY_AND_VALUE,
                                Key = "DeploymentRole",
                                Value = deploymentGroupName
                            }
                        }
                    });
                }
                else if (_awsConfiguration.DeployToAutoScalingGroups.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                {
                    var group = _autoScalingClient.DescribeAutoScalingGroups().AutoScalingGroups.FirstOrDefault(asg => asg.Tags.Any(t => t.Key == "DeploymentRole" && t.Value == deploymentGroupName));

                    if (group == null)
                        throw new ApplicationException(String.Format("Auto scaling group with DeploymentRole {0} does not exist.", deploymentGroupName));

                    _codeDeployClient.CreateDeploymentGroup(new CreateDeploymentGroupRequest
                    {
                        ApplicationName = CodeDeployApplicationNameForApplicationSetAndBundle(applicationSetName, bundleName),
                        DeploymentGroupName = deploymentGroupName,
                        ServiceRoleArn = getRoleResponse.Role.Arn,
                        AutoScalingGroups = new List<string> { group.AutoScalingGroupName }
                    });
                }
                else
                    throw new ApplicationException("Invalid value of DeployToAutoScalingGroups parameter.");

            }
            catch (DeploymentGroupAlreadyExistsException)
            {
            }
        }

        CreateDeploymentResponse DeployBundleToEnvironment(Bundle bundle,string deploymentGroupName)
        {
            CreateDeploymentResponse deploymentResponse = _codeDeployClient.CreateDeployment(new CreateDeploymentRequest
            {
                ApplicationName = CodeDeployApplicationNameForApplicationSetAndBundle(bundle.ApplicationSetName, bundle.BundleName),
                DeploymentGroupName = deploymentGroupName,
                Revision = new RevisionLocation
                {
                    RevisionType = RevisionLocationType.S3,
                    S3Location = new S3Location
                    {
                        Bucket = bundle.Bucket,
                        Key = bundle.FileName,
                        BundleType = BundleType.Zip,
                        ETag = bundle.ETag
                    }
                }
            });

            var deploymentStatus = DeploymentStatus.Created;
            DeploymentInfo deploymentInfo = null;
            while (deploymentStatus == DeploymentStatus.Created || deploymentStatus == DeploymentStatus.InProgress)
            {
                deploymentInfo =  _codeDeployClient.GetDeployment(new GetDeploymentRequest
                {
                    DeploymentId = deploymentResponse.DeploymentId
                }).DeploymentInfo;
                deploymentStatus = deploymentInfo.Status;
            }

            if (deploymentStatus == DeploymentStatus.Failed)
            {
                var failedInstances = GetFailedInstancesFor(new[] { deploymentInfo });
                if (failedInstances.Any())
                {
                    throw new DeploymentsFailedException(failedInstances);                    
                }
                throw new NoInstancesException();
            }
            return deploymentResponse;
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
            if (failedDeployments.Any())
            {
                var failedInstances = GetFailedInstancesFor(failedDeployments);
                throw new DeploymentsFailedException(failedInstances);
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
                    InstanceStatusFilter = new List<string> {"Failed"}
                });

                var tmpDeployment = deployment;
                var awsInstances =  instancesResult.InstancesList.Select(id =>
                        _codeDeployClient.GetDeploymentInstance(new GetDeploymentInstanceRequest
                        {
                            InstanceId = id,
                            DeploymentId = tmpDeployment.DeploymentId
                        }));

                allFailedInstances.AddRange(awsInstances.Select(i => 
                    new FailedInstance(i.InstanceSummary.InstanceId, deployment.DeploymentId, i.InstanceSummary.LifecycleEvents.First(lce => lce.Status == LifecycleEventStatus.Failed).Diagnostics.LogTail))
                );
            }

            return allFailedInstances.ToArray();
        }

        string PushDirectoryAsCodeDeployApplication(string directory, string applicationSetName, string bundleName, string versionString, string bucketName)
        {
            var zipFileName = string.Format("{0}.{1}.{2}.zip", applicationSetName, versionString, bundleName);
            var tempPath = Path.Combine(Path.GetTempPath(), zipFileName + "." + Guid.NewGuid() + ".zip");

            ZipFile.CreateFromDirectory(directory, tempPath, CompressionLevel.Optimal, false, Encoding.ASCII);

            var allTheBuckets = _s3Client.ListBuckets(new ListBucketsRequest()).Buckets;

            if(!allTheBuckets.Exists(b =>b.BucketName == bucketName))
            {
                _s3Client.PutBucket(bucketName);
            }

            var putResponse = _s3Client.PutObject(new PutObjectRequest
            {
                BucketName = bucketName, Key = zipFileName, FilePath = tempPath
            });

            var codeDeployApplicationName = CodeDeployApplicationNameForApplicationSetAndBundle(applicationSetName, bundleName);

            var registration = new RegisterApplicationRevisionRequest
            {
                ApplicationName = codeDeployApplicationName,
                Description = "Revision " + versionString,
                Revision = new RevisionLocation
                {
                    RevisionType = RevisionLocationType.S3,
                    S3Location = new S3Location
                    {
                        Bucket = bucketName,
                        BundleType = BundleType.Zip,
                        Key = zipFileName,
                        Version = versionString
                    }
                }
            };
            try
            {
                _codeDeployClient.RegisterApplicationRevision(registration);
            }
            catch (ApplicationDoesNotExistException)
            {
                _codeDeployClient.CreateApplication(new CreateApplicationRequest { ApplicationName = codeDeployApplicationName });
                _codeDeployClient.RegisterApplicationRevision(registration);
            }

            return putResponse.ETag;
        }

        private static string CodeDeployApplicationNameForApplicationSetAndBundle(string applicationSetName, string bundleName)
        {
            return applicationSetName + "." + bundleName;
        }
    }
}
