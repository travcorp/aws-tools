using System;

namespace TTC.Deployment.Tests
{
    public class Roles
    {
        public static string Path(string filename)
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Roles", filename);
        }
    }
}
