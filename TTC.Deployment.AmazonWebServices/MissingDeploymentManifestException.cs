using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTC.Deployment.AmazonWebServices
{
    public class MissingDeploymentManifestException : ApplicationException
    {
        private readonly string _path;
        public MissingDeploymentManifestException(string path)
        {
            _path = path;
        }

        public override string Message { get { return String.Format("No appspec.yml found at {0}", _path); } }
    }
}
