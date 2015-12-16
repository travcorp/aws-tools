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
            var awsEndpoint = TestConfiguration.AwsEndpoint;
            var credentials = new TestSuiteCredentials();

            _s3Client = new AmazonS3Client(awsEndpoint);
            _iamClient = new AmazonIdentityManagementServiceClient(awsEndpoint);

            var role = _iamClient.GetRole(new GetRoleRequest { RoleName = "SomeNewRole" });
            _awsConfiguration = new AwsConfiguration
            {
                IamRolePolicyDocument = Roles.Path("s3-policy-new-bucket.json"),
                AssumeRoleTrustDocument = Roles.Path("code-deploy-trust.json"),
                Bucket = "s3-push-test",
                AssumedRole = role.Role,
                AwsEndpoint = awsEndpoint,
                Credentials = credentials
            };

            _deployer = new Deployer(_awsConfiguration);
            _localBuildDirectory = ExampleRevisions.Directory("HelloWorld-1.2.3");
            _applicationSetName = "HelloWorld";
            _version = "1.1.1";
            DeleteRolesAndPolicies();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteRolesAndPolicies();
        }

        [Test]
        public void PushesToS3WithNewRole()
        {
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
                    RoleName = _awsConfiguration.AssumedRole.RoleName,
                    PolicyName = "s3-releases"
                });
            }
            catch (NoSuchEntityException) { }

            try
            {
                _iamClient.DeleteRole(new DeleteRoleRequest
                {
                    RoleName = _awsConfiguration.AssumedRole.RoleName
                });
            }
            catch (NoSuchEntityException){ }
        }
    }
}
