using System;
using System.IO;
using NUnit.Framework;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class BundleTest
    {
        [Test]
        public void DeploymentManifestAllowsInstances()
        {
            var dir = new DirectoryInfo(@".\ExampleRevisions\HelloWorld-1.2.3\WebLayer");
            var bundle = new Bundle("HelloWorld-1.2.3", dir, "SomeVersion", "SomeBucket", "SomeETag");
            Assert.That(bundle.TargetsAutoScalingDeploymentGroup, Is.False);
        }
        
        [Test]
        public void DeploymentManifestAllowsAutoScalingGroups()
        {
            var dir = new DirectoryInfo(@".\ExampleRevisions\HelloWorld-Autoscaling\WebLayer");
            var bundle = new Bundle("HelloWorld-AutoScaling", dir, "SomeVersion", "SomeBucket", "SomeETag");
            Assert.That(bundle.TargetsAutoScalingDeploymentGroup);
        }

        [Test]
        public void MissingDeploymentManifestThrowsUsefulError()
        {
            var dir = new DirectoryInfo(@".\ExampleRevisions\HelloWorld-NoDeploySpec\WebLayer");
            Assert.That(() => new Bundle("HelloWorld-NoDeploySpec", dir, "SomeVersion", "SomeBucket", "SomeETag"), Throws.InstanceOf<MissingDeploymentManifestException>());
        }
    }

}
