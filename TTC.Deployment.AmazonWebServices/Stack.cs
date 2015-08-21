using System.Collections.Generic;

namespace TTC.Deployment.AmazonWebServices
{
    public class Stack
    {
        public string StackName { get; set; }
        public Dictionary<string, string> Outputs { get; set; }
    }
}