using Amazon;
using CommandLine;
using CommandLine.Text;
using TTC.Deployment.AmazonWebServices;

namespace AWSCloudProvision
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options)) return;

            var deployer = new Deployer(new AwsConfiguration
            {
                AwsEndpoint = RegionEndpoint.USEast1,
                Proxy = new AwsProxy { Host = options.ProxyHost, Port = options.ProxyPort },
                ProfileName = options.ProfileName,
                ProfilesLocation = options.ProfilesLocation
            });
            deployer.CreateStack(new StackTemplate
            {
                StackName = options.StackName,
                TemplatePath = options.TemplatePath
            });
        }
    }

    class Options
    {
        [Option('s', "stackName", Required = true, HelpText = "Name of the stack to provision / deploy to")]
        public string StackName { get; set; }

        [Option('t', "templatePath", Required = true, HelpText = "Path to cloud formation template")]
        public string TemplatePath { get; set; }

        [Option('r', "Region", HelpText = "AWS Region (e.g. us-east-1")]
        public string Region { get; set; }

        [Option('h', "proxyHost", Required = false, HelpText = "The proxy host")]
        public string ProxyHost { get; set; }

        [Option('x', "proxyPort", Required = false, HelpText = "The proxy port")]
        public int ProxyPort { get; set; }

        [Option('f', "profileName", Required = true, HelpText = "Name of the IAM profile to use with AWS API")]
        public string ProfileName { get; set; }

        [Option('l', "profilesLocation", Required = true, HelpText = "Path to the IAM profiles file")]
        public string ProfilesLocation { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
