using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Amazon.AutoScaling;
using Amazon.CodeDeploy;
using Amazon.CodeDeploy.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime.Internal;

namespace TTC.Deployment.AmazonWebServices
{
    public class Bundle
    {
        private readonly string _applicationSetName;
        private readonly DirectoryInfo _bundleDirectory;
        private readonly string _bundleName;
        private readonly string _version;
        private readonly string _bucket;
        private readonly string _etag;
        private readonly string _stackName;
        private readonly DeploymentGroupSpecification _deploymentGroup;

        public Bundle(string applicationSetName, DirectoryInfo bundleDirectory, string version, string bucket, string etag, string stackName)
        {
            _applicationSetName = applicationSetName;
            _bundleDirectory = bundleDirectory;
            _bundleName = bundleDirectory.Name;
            _version = version;
            _bucket = bucket;
            _etag = etag;
            _stackName = stackName;
            _deploymentGroup = DeploymentGroupSpecification.FromFile(Path.Combine(_bundleDirectory.FullName, "deployspec.yml"));


            Console.WriteLine("Bucket initialized with bucket: " + bucket);
        }

        public string ApplicationSetName
        {
            get { return _applicationSetName; }
        }

        public string Bucket
        {
            get { return _bucket; }
        }

        public string BundleName
        {
            get { return _bundleName; }
        }

        public string ETag
        {
            get { return _etag; }
        }

        public string FileName
        {
            get { return string.Format("{0}.{1}.{2}.zip", _applicationSetName, _version, _bundleName); }
        }

        public string Version
        {
            get { return _version; }
        }

        public CreateDeploymentResponse DeployToStack(
            AmazonCodeDeployClient codeDeployClient, 
            AmazonIdentityManagementServiceClient iamClient, 
            AmazonAutoScalingClient autoScalingClient, 
            Role role)
        {
            var deploymentGroupName = _stackName + "_" + BundleName;

            EnsureDeploymentGroupExistsForBundle(codeDeployClient, iamClient, autoScalingClient, role, deploymentGroupName);

            var deploymentResponse = codeDeployClient.CreateDeployment(new CreateDeploymentRequest
            {
                ApplicationName = CodeDeployApplicationName,
                DeploymentGroupName = deploymentGroupName,
                Revision = new RevisionLocation
                {
                    RevisionType = RevisionLocationType.S3,
                    S3Location = new S3Location
                    {
                        Bucket = Bucket,
                        Key = FileName,
                        BundleType = BundleType.Zip,
                        ETag = ETag
                    }
                }
            });

            return deploymentResponse;
        }

        public string CodeDeployApplicationName
        {
            get { return _stackName + "." + BundleName; }
        }

        public DeploymentGroupSpecification DeploymentGroup
        {
            get { return _deploymentGroup; }
        }

        public bool TargetsAutoScalingDeploymentGroup
        {
            get { return DeploymentGroup.IsAutoScaling; }
        }

        void EnsureDeploymentGroupExistsForBundle(AmazonCodeDeployClient codeDeployClient, AmazonIdentityManagementServiceClient iamClient, AmazonAutoScalingClient autoScalingClient, Role role, string deploymentGroupName)
        {
            var serviceRoleArn = role.Arn;

            if (TargetsAutoScalingDeploymentGroup)
            {
                var group =
                    autoScalingClient.DescribeAutoScalingGroups()
                        .AutoScalingGroups.FirstOrDefault(
                            asg => asg.Tags.Any(t => t.Key == "DeploymentRole" && t.Value == deploymentGroupName));

                if (group == null)
                    throw new ApplicationException(
                        string.Format("Auto scaling group with DeploymentRole {0} does not exist.", deploymentGroupName));

                try
                {
                    codeDeployClient.CreateDeploymentGroup(new CreateDeploymentGroupRequest
                    {
                        ApplicationName = CodeDeployApplicationName,
                        DeploymentGroupName = deploymentGroupName,
                        ServiceRoleArn = serviceRoleArn,
                        AutoScalingGroups = new List<string> {group.AutoScalingGroupName}
                    });
                }
                catch (DeploymentGroupAlreadyExistsException)
                {
                    // reuse a previously created deployment group with the same name
                }
            }
            else
            {
                try
                {
                    Console.WriteLine("Will assume role {0} for deployment", serviceRoleArn);
                    codeDeployClient.CreateDeploymentGroup(new CreateDeploymentGroupRequest
                    {
                        ApplicationName = CodeDeployApplicationName,
                        DeploymentGroupName = deploymentGroupName,
                        ServiceRoleArn = serviceRoleArn,
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
                catch (DeploymentGroupAlreadyExistsException)
                {
                    // since this is EC2, we can reuse a previously created deployment group with the same name
                }
            }
        }

        public string Push(AmazonS3Client s3Client, AmazonCodeDeployClient codeDeployClient)
        {
            var zipFileName = string.Format("{0}.{1}.{2}.zip", ApplicationSetName, Version, BundleName);
            var tempPath = Path.Combine(Path.GetTempPath(), zipFileName + "." + Guid.NewGuid() + ".zip");

            ZipFile.CreateFromDirectory(_bundleDirectory.FullName, tempPath, CompressionLevel.Optimal, false, Encoding.ASCII);
            
            var allTheBuckets = s3Client.ListBuckets(new ListBucketsRequest()).Buckets;

            if (!allTheBuckets.Exists(b => b.BucketName == Bucket))
            {
                s3Client.PutBucket(new PutBucketRequest { BucketName = Bucket, UseClientRegion = true });
            }
            
            var putResponse = s3Client.PutObject(new PutObjectRequest
            {
                BucketName = Bucket,
                Key = zipFileName,
                FilePath = tempPath,
            });

            var registration = new RegisterApplicationRevisionRequest
            {
                ApplicationName = CodeDeployApplicationName,
                Description = "Revision " + Version,
                Revision = new RevisionLocation
                {
                    RevisionType = RevisionLocationType.S3,
                    S3Location = new S3Location
                    {
                        Bucket = Bucket,
                        BundleType = BundleType.Zip,
                        Key = zipFileName,
                        Version = Version
                    }
                }
            };
            try
            {
                codeDeployClient.RegisterApplicationRevision(registration);
            }
            catch (ApplicationDoesNotExistException)
            {
                codeDeployClient.CreateApplication(new CreateApplicationRequest { ApplicationName = CodeDeployApplicationName });
                codeDeployClient.RegisterApplicationRevision(registration);
            }

            return putResponse.ETag;
        }
    }
}