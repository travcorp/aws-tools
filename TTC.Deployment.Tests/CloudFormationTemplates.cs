using System;

namespace TTC.Deployment.Tests
{
    internal class CloudFormationTemplates
    {
        public static string Path(string filename)
        {
            return System.IO.Path.Combine(Environment.CurrentDirectory, @"CloudFormationTemplates", filename);
        }
    }
}
