using System;
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
                AwsEndpoint = RegionEndpoint.GetBySystemName(options.Region),
                Proxy = new AwsProxy { Host = options.ProxyHost, Port = options.ProxyPort },
                ParametersPath = options.ParametersPath,
                StackOutputFile = options.StackOutputFile,
                StackOutputBucket = options.StackOutputBucket,
                AssumeRoleName = options.AssumeRoleName,
                DeleteStackName = options.DeleteStackName
            });

            if (String.IsNullOrWhiteSpace(options.DeleteStackName))
            {
                deployer.CreateStack(new StackTemplate
                {
                    StackName = options.StackName,
                    TemplatePath = options.TemplatePath
                });
            }
            else
            {
                deployer.DeleteStack(options.DeleteStackName);
            }
        }
    }

    class Options
    {
        [Option('s', "stackName", Required = true, HelpText = "Name of the stack to provision / deploy to")]
        public string StackName { get; set; }

        [Option('t', "templatePath", Required = true, HelpText = "Path to cloud formation template")]
        public string TemplatePath { get; set; }

        [Option('r', "Region", HelpText = "AWS Region (e.g. us-east-1)", DefaultValue = "us-east-1")]
        public string Region { get; set; }

        [Option('h', "proxyHost", Required = false, HelpText = "The proxy host")]
        public string ProxyHost { get; set; }

        [Option('x', "proxyPort", Required = false, HelpText = "The proxy port")]
        public int ProxyPort { get; set; }

        [Option('p', "parametersPath", Required = false, HelpText = "Path (or URL) to the file with stack template parameters")]
        public string ParametersPath { get; set; }
        
        [Option('o', "stackOutputFile", Required = false, HelpText = "File name where stack output will be saved after stack creation")]
        public string StackOutputFile { get; set; }

        [Option('b', "stackOutputBucket", Required = false, HelpText = "Name of the bucket where file with stack output will be saved after stack creation")]
        public string StackOutputBucket { get; set; }

        [Option('y', "assumeRoleName", Required = false, HelpText = "ARN of the role that will be assumed in AWS API calls")]
        public string AssumeRoleName { get; set; }

        [Option('d', "deleteStackName", Required = false, HelpText = "Name of the stack to delete")]
        public string DeleteStackName { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
