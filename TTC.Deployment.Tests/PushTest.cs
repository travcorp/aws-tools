﻿using System;
using System.IO;
using System.Linq;
using Amazon;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.S3;
using Amazon.S3.Model;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class PushTest
    {
        private AwsConfiguration _awsConfiguration;
        private Deployer _deployer;
        private AmazonS3Client _s3Client;
        private AmazonIdentityManagementServiceClient _iamClient;
        private string _localBuildDirectory;
        private string _applicationSetName;
        private string _version;

        [SetUp]
        public void SetUp()
        {
            _awsConfiguration = new AwsConfiguration
            {
                IamRolePolicyDocument = Path.Combine(Environment.CurrentDirectory, "./CodeDeployRole", "s3-policy-new-bucket.json"),
                AssumeRoleTrustDocument = Path.Combine(Environment.CurrentDirectory, "./CodeDeployRole", "code-deploy-trust.json"),
                Bucket = "a-new-releases",
                RoleName = "SomeNewRole",
                AwsEndpoint = RegionEndpoint.USEast1,
                Proxy = new AwsProxy()
            };
            _s3Client = new AmazonS3Client(_awsConfiguration.AwsEndpoint);
            _iamClient = new AmazonIdentityManagementServiceClient(_awsConfiguration.AwsEndpoint);
            _deployer = new Deployer(_awsConfiguration);
            _localBuildDirectory = Path.Combine(Environment.CurrentDirectory, "ExampleRevisions", "HelloWorld-1.2.3");
            _applicationSetName = "HelloWorld";
            _version = "1.1.1";
        }

        [Test]
        public void PushesToS3WithNewRole()
        {
            DeleteRolesAndPolicies();

            _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = _applicationSetName,
                LocalDirectory = _localBuildDirectory,
                Version = _version
            });

            var subdirectories = Directory.GetDirectories(_localBuildDirectory);
            foreach (var dir in subdirectories.Select(Path.GetFileNameWithoutExtension))
            {
                var objectKey = string.Format("{0}.{1}.{2}.zip", _applicationSetName, _version, dir);
                var bucketObjectResponse =  _s3Client.GetObject(new GetObjectRequest
                {
                    BucketName = _awsConfiguration.Bucket,
                    Key =objectKey
                });
                Assert.AreEqual(bucketObjectResponse.Key, objectKey);
            }
        }

        private void DeleteRolesAndPolicies()
        {
            try
            {
                _s3Client.DeleteBucket(_awsConfiguration.Bucket);
            }
            catch (AmazonS3Exception) { }

            try
            {
                _iamClient.DeleteRolePolicy(new DeleteRolePolicyRequest
                {
                    RoleName = _awsConfiguration.RoleName,
                    PolicyName = "s3-releases"
                });
            }
            catch (NoSuchEntityException) { }

            try
            {
                _iamClient.DeleteRole(new DeleteRoleRequest
                {
                    RoleName = _awsConfiguration.RoleName
                });
            }
            catch (NoSuchEntityException){ }
        }
    }
}