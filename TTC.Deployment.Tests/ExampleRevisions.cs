using System;
using System.IO;

namespace TTC.Deployment.Tests
{
    public class ExampleRevisions
    {
        public static string Directory(string name)
        {
            return Path.Combine(Environment.CurrentDirectory, @"ExampleRevisions", name);
        }

        public static DirectoryInfo BundleDirectory(string revisionName, string bundleName)
        {
            return new DirectoryInfo(Path.Combine(Directory(revisionName), bundleName));
        }
    }
}
