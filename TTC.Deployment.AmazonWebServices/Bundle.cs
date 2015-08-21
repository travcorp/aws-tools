namespace TTC.Deployment.AmazonWebServices
{
    public class Bundle
    {
        private readonly string _applicationSetName;
        private readonly string _bundleName;
        private readonly string _version;
        private readonly string _bucket;
        private readonly string _etag;

        public Bundle(string applicationSetName, string bundleName, string version, string bucket, string etag = null)
        {
            _applicationSetName = applicationSetName;
            _bundleName = bundleName;
            _version = version;
            _bucket = bucket;
            _etag = etag;
        }

        public string ApplicationSetName
        {
            get { return _applicationSetName; }
        }

        public string Bucket
        {
            get { return _bucket; }
        }

        public string BundleName
        {
            get { return _bundleName; }
        }

        public string ETag
        {
            get { return _etag; }
        }

        public string FileName
        {
            get { return string.Format("{0}.{1}.{2}.zip", _applicationSetName, _version, _bundleName); }
        }

        public string Version
        {
            get { return _version; }
        }
    }
}