using System.Collections.Generic;
using System.IO;
using Amazon.CodeDeploy;
using Amazon.CodeDeploy.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

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

        public Bundle(string applicationSetName, DirectoryInfo bundleDirectory, string version, string bucket, string etag)
        {
            _applicationSetName = applicationSetName;
            _bundleDirectory = bundleDirectory;
            _bundleName = bundleDirectory.Name;
            _version = version;
            _bucket = bucket;
            _etag = etag;
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

        public CreateDeploymentResponse DeployToStack(AmazonCodeDeployClient codeDeployClient, AmazonIdentityManagementServiceClient iamClient, string stackName, string roleName)
        {
            var deploymentGroupName = stackName + "_" + BundleName;

            EnsureDeploymentGroupExistsForBundle(codeDeployClient, iamClient, roleName, deploymentGroupName);

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
            get { return ApplicationSetName + "." + BundleName; }
        }

        public DeploymentGroupSpecification DeploymentGroup  { get; set; }

        public bool TargetsAutoScalingDeploymentGroup
        {
            get { return DeploymentGroup.IsAutoScaling; }
        }

        void EnsureDeploymentGroupExistsForBundle(AmazonCodeDeployClient codeDeployClient, AmazonIdentityManagementServiceClient iamClient, string roleName, string deploymentGroupName)
        {
            var getRoleResponse = iamClient.GetRole(new GetRoleRequest
            {
                RoleName = roleName
            });

            var serviceRoleArn = getRoleResponse.Role.Arn;

            try
            {
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


            /*try
            {
                if (String.IsNullOrWhiteSpace(_awsConfiguration.DeployToAutoScalingGroups)
                    || _awsConfiguration.DeployToAutoScalingGroups.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                {

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
                        ServiceRoleArn = serviceRoleArn,
                        AutoScalingGroups = new List<string> { group.AutoScalingGroupName }
                    });
                }
                else
                    throw new ApplicationException("Invalid value of DeployToAutoScalingGroups parameter.");

            }
            catch (DeploymentGroupAlreadyExistsException)
            {
            }*/
        }
    }
}