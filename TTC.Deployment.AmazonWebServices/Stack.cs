using System.Collections.Generic;

namespace TTC.Deployment.AmazonWebServices
{
    public class Stack
    {
        private readonly Dictionary<string, string> _outputs;
        private readonly string _stackName;

        public Stack(string stackName, Dictionary<string, string> outputs)
        {
            _stackName = stackName;
            _outputs = outputs;
        }

        public string StackName
        {
            get { return _stackName; }
        }

        public Dictionary<string, string> Outputs
        {
            get { return _outputs; }
        }
    }
}