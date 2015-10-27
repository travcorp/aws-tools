using Amazon;
using Amazon.CloudFormation;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using NUnit.Framework;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Threading;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class AssumeRoleTest
    {
        AmazonCloudFormationClient _cloudFormationClient;
        AwsConfiguration _awsConfiguration;
        AmazonIdentityManagementServiceClient _iamClient;
        AmazonSecurityTokenServiceClient _securityTokenServiceClient;
        AmazonS3Client _s3Client;
        User _user;
        BasicAWSCredentials _userCredentials;
        string _accessKeyId;
        string _userName;
        Role _role;
        string _roleName;
        string _bucketName;

        [SetUp]
        public void SetUp()
        {
            ConfigurationManager.AppSettings["AWSProfileName"] = "default";
            _userName = "test_user_16";
            _roleName = "assume-role-" + DateTime.Now.ToFileTime().ToString();
            _bucketName = "aws-tools-test-bucket-1";

            _awsConfiguration = new AwsConfiguration
            {
                AwsEndpoint = RegionEndpoint.USWest2,
                Proxy = new AwsProxy()
            };
            _cloudFormationClient = new AmazonCloudFormationClient(new AmazonCloudFormationConfig { RegionEndpoint = _awsConfiguration.AwsEndpoint });
            DeletePreviousTestStack();

            _iamClient = new AmazonIdentityManagementServiceClient(
                new AmazonIdentityManagementServiceConfig
                {
                    RegionEndpoint = _awsConfiguration.AwsEndpoint,
                    ProxyHost = _awsConfiguration.Proxy.Host,
                    ProxyPort = _awsConfiguration.Proxy.Port
                });

            _user = _iamClient.CreateUser(new CreateUserRequest
            {
                UserName = _userName
            }).User;

            _role = _iamClient.CreateRole(new CreateRoleRequest
            {
                RoleName = _roleName,
                AssumeRolePolicyDocument = @"{
                  ""Statement"":
                  [
                    {
                      ""Principal"":{""AWS"":""{AccountId}""},
                      ""Effect"":""Allow"",
                      ""Action"":[""sts:AssumeRole""]
                    }
                  ]
                }".Replace("{AccountId}", GetAWSAccountIdFromArn(_user))
            }).Role;

            _iamClient.PutUserPolicy(new PutUserPolicyRequest
            {
                UserName = _user.UserName,
                PolicyName = "assume-policy-1",
                PolicyDocument = @"{
                    ""Statement"":{
                        ""Effect"":""Allow"",
                        ""Action"":""sts:AssumeRole"",
                        ""Resource"":""*""
                    }
                }"
            });

            var accessKey = _iamClient.CreateAccessKey(new CreateAccessKeyRequest
            {
                UserName = _user.UserName
            }).AccessKey;

            _accessKeyId = accessKey.AccessKeyId;
            _userCredentials = new BasicAWSCredentials(accessKey.AccessKeyId, accessKey.SecretAccessKey);
        }

        [Test]
        public void FailsToCreateVPCBasedOnBadRoleThatCannotAssumeOtherRoles()
        {
            var clientId = Guid.NewGuid();
            const string sessionName = "NetUser";
            _securityTokenServiceClient = new AmazonSecurityTokenServiceClient(_userCredentials, _awsConfiguration.AwsEndpoint);
            Thread.Sleep(TimeSpan.FromSeconds(60));
            var impotentCredentials = _securityTokenServiceClient.AssumeRole(new AssumeRoleRequest
            {
                RoleArn = _role.Arn,
                RoleSessionName = sessionName,
                DurationSeconds = 3600,
                ExternalId = clientId.ToString()
            }).Credentials;

            _awsConfiguration.Credentials = impotentCredentials;

            var deployer = new Deployer(_awsConfiguration);
            Assert.Throws<AmazonCloudFormationException>(() => deployer.CreateStack(new StackTemplate
            {
                StackName = "SimpleBucketTestStack",
                TemplatePath = @".\simple-s3-bucket-template.json",
                AssumedRoleARN = _role.Arn
            }));
        }

        [Test]
        public void CreatesVPCBasedOnBadRoleThatCanAssumeAppropriateRole()
        {
            _iamClient.PutRolePolicy(new PutRolePolicyRequest
            {
                RoleName = _role.RoleName,
                PolicyName = "assume-policy-1",
                PolicyDocument = @"{
                  ""Version"": ""2012-10-17"",
                  ""Statement"": [
                    {
                      ""Effect"": ""Allow"",
                      ""Action"": [
                        ""s3:CreateBucket"",
                        ""cloudformation:*""
                      ],
                      ""Resource"": [
                        ""*""
                      ]
                    }
                  ]
                }"
            });

            var clientId = Guid.NewGuid();
            const string sessionName = "Net2User";
            _securityTokenServiceClient = new AmazonSecurityTokenServiceClient(_userCredentials, _awsConfiguration.AwsEndpoint);
            Thread.Sleep(TimeSpan.FromSeconds(60));
            var goodCredentials = _securityTokenServiceClient.AssumeRole(new AssumeRoleRequest
            {
                RoleArn = _role.Arn,
                RoleSessionName = sessionName,
                DurationSeconds = 3600,
                ExternalId = clientId.ToString()
            }).Credentials;

            _awsConfiguration.Credentials = goodCredentials;
            var deployer = new Deployer(_awsConfiguration);
             deployer.CreateStack(new StackTemplate {
       
                StackName = "SimpleBucketTestStack",
                TemplatePath = @".\simple-s3-bucket-template.json",
                AssumedRoleARN = _role.Arn
            });

            _s3Client = new AmazonS3Client(
                new AmazonS3Config
                {
                    RegionEndpoint =_awsConfiguration.AwsEndpoint
                });

            var s3Response = _s3Client.GetBucketLocation(_bucketName);
            Assert.AreEqual(s3Response.HttpStatusCode, HttpStatusCode.OK);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteRole("some_role_that_is_no_good_test");
            var userPolicies = _iamClient.ListUserPolicies(new ListUserPoliciesRequest { UserName = _userName }).PolicyNames;
            foreach (var policy in userPolicies)
            {
                _iamClient.DeleteUserPolicy(new DeleteUserPolicyRequest
                {
                    UserName = _userName,
                    PolicyName = policy
                });
            }
            _iamClient.DeleteAccessKey(new DeleteAccessKeyRequest
            {
                UserName = _userName,
                AccessKeyId = _accessKeyId
            });
            _iamClient.DeleteUser(new DeleteUserRequest { UserName = _userName });
            _s3Client.DeleteBucket(_bucketName);
        }

        private void DeleteRole(string roleName)
        {
            try
            {
                var roleResponse = _iamClient.GetRole(new GetRoleRequest
                {
                    RoleName = roleName
                });

                if (roleResponse.Role == null) return;

                var rolePoliciesResponse = _iamClient.ListRolePolicies(new ListRolePoliciesRequest
                {
                    RoleName = roleName
                });
           
                foreach(var p in rolePoliciesResponse.PolicyNames)
                {
                    _iamClient.DeleteRolePolicy(new DeleteRolePolicyRequest
                    {
                        PolicyName = p,
                        RoleName = roleName
                    });
                } 
            
                _iamClient.DeleteRole(new DeleteRoleRequest
                {
                    RoleName = roleName
                });
            } catch { }
        }

        private void DeletePreviousTestStack()
        {
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, "SimpleBucketTestStack");
        }

        private void PutRolePolicy(string pathToRoleDocument, string roleName)
        {
            var response = _iamClient.PutRolePolicy(new PutRolePolicyRequest
            {
                RoleName = roleName,
                PolicyName = "create-s3-bucket-test",
                PolicyDocument = File.ReadAllText(pathToRoleDocument)
            });
        }

        private string CreateRole(string pathToRoleDocument, string roleName)
        {
            _iamClient = new AmazonIdentityManagementServiceClient(
                new AmazonIdentityManagementServiceConfig
                {
                    RegionEndpoint = _awsConfiguration.AwsEndpoint,
                    ProxyHost = _awsConfiguration.Proxy.Host,
                    ProxyPort = _awsConfiguration.Proxy.Port
                });

            var response = _iamClient.CreateRole(new CreateRoleRequest
            {
                RoleName = roleName,
                AssumeRolePolicyDocument = File.ReadAllText(pathToRoleDocument)
            });

            return response.Role.Arn;
        }

        private string GetAWSAccountIdFromArn(User user)
        {
            var tokens = user.Arn.Split(':');
            return tokens[4];
            // yep - you heard me
        }
    }
}
