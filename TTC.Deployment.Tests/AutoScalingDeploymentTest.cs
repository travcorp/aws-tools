using System;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using System.Threading;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class AutoScalingDeploymentTest
    {
        private AmazonIdentityManagementServiceClient _iamClient;
        private User _user;
        private RoleHelper _roleHelper;
        private Role _role;
        private AwsConfiguration _awsConfiguration;
        private Deployer _deployer;
        private Stack _stack;
        const string StackName = "AwsToolsAutoScalingTestVPC2";
        private static bool _hasCreatedStack;

        [SetUp]
        public void EnsureStackExists()
        {
            if (_hasCreatedStack) return;
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
                UserName = "TestDeployerUserASG2"
            }).User;


            _roleHelper = new RoleHelper(_iamClient);
            _role = _roleHelper.CreateRoleForUserToAssume(_user);
    

            _iamClient.PutRolePolicy(new PutRolePolicyRequest
            {
                RoleName = _role.RoleName,
                PolicyName = "assume-policy-8",
                PolicyDocument = @"{
                  ""Version"": ""2012-10-17"",
                  ""Statement"": [
                    {
                      ""Effect"": ""Allow"",
                      ""Action"": [
                        ""*""
                      ],
                      ""Resource"": [
                        ""*""
                      ]
                    }
                  ]
                }"
            });
            Thread.Sleep(TimeSpan.FromSeconds(10));
            _awsConfiguration.RoleName = _role.Arn;

            _deployer = new Deployer(_awsConfiguration);

            DeletePreviousTestStack();
            _stack = _deployer.CreateStack(new StackTemplate
            {
                StackName = StackName,
                TemplatePath = CloudFormationTemplates.Path("example-windows-vpc-autoscaling-group-template.json")
            });
            _hasCreatedStack = true;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _roleHelper.DeleteRole(_role.Arn);
            _roleHelper.DeleteUser("TestDeployerUserASG2");
            DeletePreviousTestStack();
        }

        private void DeletePreviousTestStack()
        {
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, StackName);
        }

        [Test]
        public void DeploysCodeToInstancesInTheAutoScalingGroup()
        {
            var goodRevision = _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = "HelloWorld",
                Version = "GoodAutoScalingRevision",
                LocalDirectory = @".\ExampleRevisions\HelloWorld-AutoScaling"
            });

            _deployer.DeployRelease(goodRevision, StackName);

            var publicDnsName = _stack.Outputs["publicDnsName"];
            var homePageUrl = string.Format("http://{0}/index.aspx", publicDnsName);

            Console.WriteLine(homePageUrl);

            var webpageText = Retry.Do(() => Http.Get(homePageUrl), TimeSpan.FromSeconds(10));
            Assert.That(webpageText, Is.EqualTo("Hello, world!"));
        }
    }
}
