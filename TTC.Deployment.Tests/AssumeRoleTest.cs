using Amazon.CloudFormation;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using NUnit.Framework;
using System;
using System.Net;
using System.Threading;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class AssumeRoleTest
    {
        AwsConfiguration _awsConfiguration;
        AmazonIdentityManagementServiceClient _iamClient;
        AmazonSecurityTokenServiceClient _securityTokenServiceClient;
        AmazonS3Client _s3Client;
        User _user;
        BasicAWSCredentials _userCredentials;
        
        Role _role;
        string _roleName;

        private readonly string _userName = "aws_tools_assume_role_test_user";
        private readonly string _bucketName = "aws-tools-test-bucket-" + Guid.NewGuid().ToString().ToLowerInvariant();

        [SetUp]
        public void SetUp()
        {
            _roleName = "assume-role-" + DateTime.Now.ToFileTime();

            _awsConfiguration = new AwsConfiguration
            {
                AwsEndpoint = TestConfiguration.AwsEndpoint,
                Credentials = new TestSuiteCredentials()
            };
            
            _iamClient = new AmazonIdentityManagementServiceClient(
                new AmazonIdentityManagementServiceConfig
                {
                    RegionEndpoint = _awsConfiguration.AwsEndpoint,
                    ProxyHost = _awsConfiguration.ProxyHost,
                    ProxyPort = _awsConfiguration.ProxyPort
                });

            _s3Client = new AmazonS3Client(new AmazonS3Config { RegionEndpoint = _awsConfiguration.AwsEndpoint });

            DeletePreviousTestStack();

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

            var accessKey = _iamClient.CreateAccessKey(new CreateAccessKeyRequest { UserName = _user.UserName }).AccessKey;
            _userCredentials = new BasicAWSCredentials(accessKey.AccessKeyId, accessKey.SecretAccessKey);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTheBucket();
            DeletePreviousTestStack();
        }

        private void DeleteTheBucket()
        {
            IgnoringNoSuchBucket(() => _s3Client.DeleteBucket(_bucketName));
        }

        [Test]
        public void FailsToCreateBucketBasedOnBadRoleThatCannotAssumeOtherRoles()
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
            _awsConfiguration.RoleName = _role.Arn;

            var deployer = new Deployer(_awsConfiguration);
            Assert.Throws<AmazonCloudFormationException>(() => deployer.CreateStack(new StackTemplate
            {
                StackName = "SimpleBucketTestStack",
                TemplatePath = CloudFormationTemplates.Path("simple-s3-bucket-template.json")
            }));
        }

        [Test]
        public void CreatesBucketBasedOnRoleThatCanAssumeAppropriateRole()
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

            _awsConfiguration.RoleName = _role.Arn;
            var deployer = new Deployer(_awsConfiguration);
            deployer.CreateStack(new StackTemplate {
                StackName = "SimpleBucketTestStack",
                TemplatePath = CloudFormationTemplates.Path("simple-s3-bucket-template.json")
            });

            var s3Response = _s3Client.GetBucketLocation(_bucketName);
            Assert.AreEqual(s3Response.HttpStatusCode, HttpStatusCode.OK);
        }

        private void DeleteRole(string roleName)
        {
            try
            {
                _iamClient.GetRole(new GetRoleRequest {RoleName = roleName});
            }
            catch (NoSuchEntityException)
            {
                return;
            }

            var rolePoliciesResponse = _iamClient.ListRolePolicies(new ListRolePoliciesRequest { RoleName = roleName });

            foreach (var p in rolePoliciesResponse.PolicyNames)
            {
                var request = new DeleteRolePolicyRequest { PolicyName = p, RoleName = roleName };
                IgnoringNoSuchEntity(() => _iamClient.DeleteRolePolicy(request));
            }

            IgnoringNoSuchEntity(() => _iamClient.DeleteRole(new DeleteRoleRequest { RoleName = roleName }));
        }

        private void DeletePreviousTestStack()
        {
            DeleteRole("some_role_that_is_no_good_test");
            DeleteUser(_userName);
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, "SimpleBucketTestStack");
        }

        private void DeleteUser(string userName)
        {
            try
            {
                var userPolicies =
                    _iamClient.ListUserPolicies(new ListUserPoliciesRequest {UserName = userName}).PolicyNames;

                foreach (var policy in userPolicies)
                {
                    var request = new DeleteUserPolicyRequest {UserName = userName, PolicyName = policy};
                    IgnoringNoSuchEntity(() => { _iamClient.DeleteUserPolicy(request); });
                }
            }
            catch (NoSuchEntityException)
            {
                return;
            }

            var keys = _iamClient.ListAccessKeys(new ListAccessKeysRequest { UserName = _userName });

            foreach (var key in keys.AccessKeyMetadata)
            {
                var request = new DeleteAccessKeyRequest { UserName = userName, AccessKeyId = key.AccessKeyId };
                IgnoringNoSuchEntity(() => { _iamClient.DeleteAccessKey(request); });
            }

            IgnoringNoSuchEntity(() => _iamClient.DeleteUser(new DeleteUserRequest { UserName = userName }));
        }

        private void IgnoringNoSuchEntity(Action action)
        {
            try
            {
                action();
            }
            catch (NoSuchEntityException)
            {
                Console.WriteLine("Ignoring no such entity...");
            }
        }

        private void IgnoringNoSuchBucket(Action action)
        {
            try
            {
                action();
            }
            catch (AmazonS3Exception)
            {
                Console.WriteLine("Ignoring no such bucket...");
            }
        }

        private string GetAWSAccountIdFromArn(User user)
        {
            var tokens = user.Arn.Split(':');
            return tokens[4];
            // yep - you heard me
        }
    }
}
