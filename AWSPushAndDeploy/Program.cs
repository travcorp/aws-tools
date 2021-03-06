﻿using System;
using Amazon;
using CommandLine;
using CommandLine.Text;
using TTC.Deployment.AmazonWebServices;

namespace AWSPushAndDeploy
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
                AssumeRoleTrustDocument = options.AssumeRolePolicyPath,
                IamRolePolicyDocument = options.S3AccessPolicyDocumentPath,
                Bucket = options.BucketName,
                RoleName = options.RoleName,
                CodeDeployRoleName = options.CodeDeployRoleName,
                AwsEndpoint = RegionEndpoint.GetBySystemName(options.RegionEndpoint), 
                Proxy = new AwsProxy{ Host = options.ProxyHost, Port = options.ProxyPort }
            });
            var revision = deployer.PushRevision(new ApplicationSetRevision
            {
                ApplicationSetName = options.ApplicationSetName,
                StackName = options.StackName,
                Version = options.Version,
                LocalDirectory = options.BuildDirectoryPath
            });
            try
            {
                deployer.DeployRelease(revision, options.CodeDeployRoleName);
            }
            catch (Exception e)
            {
                Console.WriteLine("AWS Push And Deploy Error:");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Environment.Exit(666);
            }
        }
    }

    class Options
    {
        [Option('s', "stackName", Required = true, HelpText = "Name of the stack to provision / deploy to")]
        public string StackName { get; set; }

        [Option('v', "version", Required = true, HelpText = "Version to deploy")]
        public string Version { get; set; }

        [Option('a', "applicationSetName", Required = true, HelpText = "ApplicationSetName")]
        public string ApplicationSetName { get; set; }

        [Option('r', "assumeRoleTrustDocument", Required = true, HelpText = "Path to AssumeRole trust document")]
        public string AssumeRolePolicyPath { get; set; }

        [Option('p', "IAMRolePolicyDocumentPath", Required = true, HelpText = "Path to IAM role policy document")]
        public string S3AccessPolicyDocumentPath { get; set; }

        [Option('l', "bucketName", Required = true, HelpText = "S3 bucket name to create / use")]
        public string BucketName { get; set; }

        [Option('b', "buildDirectoryPath", Required = true, HelpText = "Path to build directory")]
        public string BuildDirectoryPath { get; set; }

        [Option('h', "proxyHost", Required = false, HelpText = "The proxy host")]
        public string ProxyHost { get; set; }

        [Option('x', "proxyPort", Required = false, HelpText = "The proxy port")]
        public int ProxyPort { get; set; }

        [Option('e', "regionEndpoint", Required = false, HelpText = "Amazon region endpoint", DefaultValue = "us-east-1")]
        public string RegionEndpoint { get; set; }

        [Option('n', "roleName", Required = false, HelpText = "Assume role name")]
        public string RoleName { get; set; }

        [Option('c', "codeDeployRoleName", Required = false, HelpText = "Assume code deploy role")]
        public string CodeDeployRoleName { get; set; }
        
        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
