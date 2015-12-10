using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.S3;
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
        private AwsConfiguration _awsConfiguration;
        private AmazonIdentityManagementServiceClient _iamClient;
        private AmazonS3Client _s3Client;
        private User _user;
        private RoleHelper _roleHelper;
        private Role _role;

        private readonly string _userName = "aws_tools_assume_role_test_user";
        private readonly string _bucketName = "aws-tools-test-bucket-00";

        [SetUp]
        public void SetUp()
        {
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

            _user = _iamClient.CreateUser(new CreateUserRequest
            {
                UserName = _userName
            }).User;
            _roleHelper = new RoleHelper(_iamClient);
            _role = _roleHelper.CreateRoleForUserToAssume(_user);

            _s3Client = new AmazonS3Client(new AmazonS3Config { RegionEndpoint = _awsConfiguration.AwsEndpoint });
            DeletePreviousTestStack();

        }

        [TearDown]
        public void TearDown()
        {
            DeletePreviousTestStack();
        }

        [Test]
        public void CreatesBucketBasedOnRoleThatCanAssumeAppropriateRole()
        {
            var createBucketRole = _iamClient.PutRolePolicy(new PutRolePolicyRequest
            {
                RoleName = _role.RoleName,
                PolicyName = "assume-policy-8",
                PolicyDocument = @"{
                  ""Version"": ""2012-10-17"",
                  ""Statement"": [
                    {
                      ""Effect"": ""Allow"",
                      ""Action"": [
                        ""s3:*"",
                        ""cloudformation:*""
                      ],
                      ""Resource"": [
                        ""*""
                      ]
                    }
                  ]
                }"
            });

            Thread.Sleep(TimeSpan.FromSeconds(10));

            _awsConfiguration.RoleArn = _role.Arn;
            var deployer = new Deployer(_awsConfiguration);
            deployer.CreateStack(new StackTemplate {
                StackName = "SimpleBucketTestStack",
                TemplatePath = CloudFormationTemplates.Path("simple-s3-bucket-template.json"),
            });   

            var s3Response = _s3Client.GetBucketLocation(_bucketName);
            Assert.AreEqual(s3Response.HttpStatusCode, HttpStatusCode.OK);
        }

  
        private void DeletePreviousTestStack()
        {
            _roleHelper.DeleteRole("some_role_that_is_no_good_test");
            _roleHelper.DeleteUser(_userName);
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, "SimpleBucketTestStack");
            IgnoringNoSuchBucket(() => _s3Client.DeleteBucket(_bucketName));
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
    }
}
