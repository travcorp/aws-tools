using Amazon;
using CommandLine;
using CommandLine.Text;
using TTC.Deployment.AmazonWebServices;

namespace AWSS3Push
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options)) return;

            var deployer = new Deployer(new AwsConfiguration
            {
                AssumeRoleTrustDocument = options.AssumeRolePolicyPath,
                IamRolePolicyDocument = options.S3AccessPolicyDocumentPath,
                Bucket = options.BucketName,
                RoleArn = "S3-Push",
                AwsEndpoint = RegionEndpoint.GetBySystemName(options.RegionEndpoint)
            });

            deployer.PushRevision(new ApplicationSetRevision
            {
               LocalDirectory  = options.BuildDirectoryPath,
               Version = options.Version,
               ApplicationSetName = options.ApplicationSetName
            });
        }
    }

    class Options
    {
        [Option('v', "version", Required = true, HelpText = "Version to deploy")]
        public string Version { get; set; }

        [Option('b', "buildDirectoryPath", Required = true, HelpText = "Path to build directory")]
        public string BuildDirectoryPath { get; set; }

        [Option('a', "applicationSetName", Required = true, HelpText = "ApplicationSetName")]
        public string ApplicationSetName { get; set; }

        [Option('r', "assumeRoleTrustDocument", Required = true, HelpText = "Path to AssumeRole trust document")]
        public string AssumeRolePolicyPath { get; set; }

        [Option('p', "IAMRolePolicyDocumentPath", Required = true, HelpText = "Path to IAM role policy document")]
        public string S3AccessPolicyDocumentPath { get; set; }

        [Option('n', "bucketName", Required = true, HelpText = "S3 bucket name to create / use")]
        public string BucketName { get; set; }

        [Option('e', "regionEndpoint", Required = false, HelpText = "Amazon region endpoint", DefaultValue = "us-east-1")]
        public string RegionEndpoint { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
