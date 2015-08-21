using System.Collections.Generic;

namespace TTC.Deployment.AmazonWebServices
{
    public class Release
    {
        private readonly string _applicationSetName;
        private readonly string _version;
        private readonly IEnumerable<Bundle> _bundles;

        public Release(string applicationSetName, string version, IEnumerable<Bundle> bundles)
        {
            _applicationSetName = applicationSetName;
            _version = version;
            _bundles = bundles;
        }

        public string ApplicationSetName
        {
            get { return _applicationSetName; }
        }

        public string Version
        {
            get { return _version; }
        }

        public IEnumerable<Bundle> Bundles
        {
            get { return _bundles; }
        }
    }
}