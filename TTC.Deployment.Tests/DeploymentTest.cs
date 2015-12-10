using System;
using System.Linq;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using System.Threading;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class DeploymentTest
    {
        private AmazonIdentityManagementServiceClient _iamClient;
        private User _user;
        private Role _role;
        private RoleHelper _roleHelper;

        private AwsConfiguration _awsConfiguration;
        private Deployer _deployer;
        private Stack _stack;
        const string StackName = "AwsToolsTestVPC";
        private static bool _hasCreatedStack;
        private string _username = "TestDeployerUserZ";

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
                UserName = _username
            }).User;

            _roleHelper = new RoleHelper(_iamClient);
            _role = _roleHelper.CreateRoleForUserToAssume(_user);
            Thread.Sleep(TimeSpan.FromSeconds(10));

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

            _awsConfiguration.RoleName = _role.Arn;
            _deployer = new Deployer(_awsConfiguration);

            DeletePreviousTestStack();
            _stack = _deployer.CreateStack(new StackTemplate
            {
                StackName = StackName,
                TemplatePath = CloudFormationTemplates.Path("example-windows-vpc-template.json")
            });
            _hasCreatedStack = true;
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _roleHelper.DeleteRole(_role.Arn);
            _roleHelper.DeleteUser(_username);
            DeletePreviousTestStack();
        }

        private void DeletePreviousTestStack()
        {
            StackHelper.DeleteStack(_awsConfiguration.AwsEndpoint, StackName);
        }

        [Test]
        public void ThrowsDeploymentsFailedExceptionForBadDeployments()
        {
            var badRevision = _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = "HelloWorld",
                Version = "BadWebLayerAppSpec",
                LocalDirectory = ExampleRevisions.Directory("HelloWorld-BadWebLayerAppSpec")
            });    

            var expectedTail = string.Join("\n",
               @"LifecycleEvent - BeforeInstall",
               @"Script - \before-install.bat",
               @"[stdout]",
               @"[stdout]C:\Windows\system32>echo ""oh noes!"" ",
               @"[stdout]""oh noes!""",
               @"[stdout]",
               @"[stdout]C:\Windows\system32>exit 1 ",
               ""
           );

           try
           {
               _deployer.DeployRelease(badRevision, StackName);
               Assert.Fail("Expected DeploymentsFailedException");
           }
           catch (DeploymentsFailedException e)
           {
               Assert.That(e.FailedInstances.First().Tail, Is.EqualTo(expectedTail));
           }
        }

        [Test]
        public void ThrowsDeploymentsFailedExceptionWhenNoInstancesWereMatched()
        {
            var badRevision = _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = "HelloWorld",
                Version = "Empty",
                LocalDirectory = ExampleRevisions.Directory("HelloWorld-Empty")
            });

            try
            {
                _deployer.DeployRelease(badRevision, StackName);
                Assert.Fail("Expected DeploymentsFailedException");
            }
            catch (DeploymentsFailedException e)
            {
                Assert.That(e.FailedInstances.Count(), Is.EqualTo(0));
                Assert.That(e.Message, Contains.Substring("No instances found"));
            }
        }

        [Test]
        public void DeploysCodeToAppropriateInstances()
        {
            var goodRevision = _deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = "HelloWorld",
                Version = "GoodRevision",
                LocalDirectory = ExampleRevisions.Directory("HelloWorld-1.2.3")
            });

            _deployer.DeployRelease(goodRevision, StackName);

            var publicDnsName = _stack.Outputs.First(o => o.Key == "publicDnsName").Value;
            var homePageUrl = string.Format("http://{0}/index.aspx", publicDnsName);

            Console.WriteLine(homePageUrl);

            var webpageText = Retry.Do(() => { var html = Http.Get(homePageUrl); return html; }, TimeSpan.FromSeconds(10));

            Assert.That(webpageText, Is.EqualTo("Hello, world!"));
        }
    }
}
