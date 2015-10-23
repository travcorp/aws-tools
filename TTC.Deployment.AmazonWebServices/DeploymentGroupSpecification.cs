using System.IO;
using YamlDotNet.Serialization;

namespace TTC.Deployment.AmazonWebServices
{
    public class DeploymentGroupSpecification
    {
        [YamlMember(Alias = "autoscaling")]
        public bool IsAutoScaling { get; set; }

        public static DeploymentGroupSpecification FromFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new MissingDeploymentManifestException(path);
            }
            using (var stream = File.Open(path, FileMode.Open))
            {
                using (var reader = new StreamReader(stream))
                {
                    var deserializer = new Deserializer();
                    return deserializer.Deserialize<DeploymentGroupFile>(reader).DeploymentGroup;
                }
            }
        }
    }

    public class DeploymentGroupFile
    {
        [YamlMember(Alias = "deploymentGroup")]
        public DeploymentGroupSpecification DeploymentGroup { get; set; }
    }
}