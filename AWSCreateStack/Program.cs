using System;
using Amazon;
using CommandLine;
using CommandLine.Text;
using TTC.Deployment.AmazonWebServices;

namespace AWSCreateStack
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options))
                Environment.Exit(1);

            var deployer = new Deployer(new AwsConfiguration
            {
                AwsEndpoint = RegionEndpoint.GetBySystemName(options.Region),
                RoleName = options.RoleName,
                Proxy = new AwsProxy { Host = options.ProxyHost, Port = options.ProxyPort }
            });
            deployer.CreateStack(new StackTemplate
            {
                StackName = options.StackName,
                TemplatePath = options.TemplatePath,
                ParameterPath = options.ParameterPath
            });
        }
    }

    class Options
    {
        [Option('s', "stackName", Required = true, HelpText = "Name of the stack to provision / deploy to")]
        public string StackName { get; set; }

        [Option('t', "templatePath", Required = true, HelpText = "Path to cloud formation template")]
        public string TemplatePath { get; set; }

        [Option('p', "parameterPath", Required = false, HelpText = "Path to template parameters")]
        public string ParameterPath { get; set; }

        [Option('n', "roleName", Required = false, HelpText = "Assume role name")]
        public string RoleName { get; set; }

        [Option('r', "Region", HelpText = "AWS Region (e.g. us-east-1")]
        public string Region { get; set; }

        [Option('h', "proxyHost", Required = false, HelpText = "The proxy host")]
        public string ProxyHost { get; set; }

        [Option('x', "proxyPort", Required = false, HelpText = "The proxy port")]
        public int ProxyPort { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
