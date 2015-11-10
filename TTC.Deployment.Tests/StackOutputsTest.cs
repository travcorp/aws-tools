using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class StackOutputsTest
    {
        private string _stackName = "TestStackOutputsStack";

        [SetUp]
        public void SetUp()
        {
            DeletePreviousTestStack();
        }

        [Test]
        public void CreatingStackCapturesOutputs()
        {
            var deployer = new Deployer(new AwsConfiguration { AwsEndpoint = TestConfiguration.AwsEndpoint });
            var stack = deployer.CreateStack(new StackTemplate
            {
                StackName = _stackName,
                TemplatePath = CloudFormationTemplates.Path("stack-outputs.json")
            });
            Assert.That(stack.Outputs["outputA"], Is.EqualTo("123"));
            Assert.That(stack.Outputs["outputB"], Is.EqualTo("456"));
        }

        [TearDown]
        public void TearDown()
        {
            DeletePreviousTestStack();
        }

        private void DeletePreviousTestStack()
        {
            StackHelper.DeleteStack(TestConfiguration.AwsEndpoint, _stackName);
        }
     }
}
