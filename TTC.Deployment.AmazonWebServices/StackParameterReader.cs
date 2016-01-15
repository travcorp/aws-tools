using System.Collections.Generic;
using System.IO;
using Amazon.CloudFormation.Model;
using Newtonsoft.Json.Linq;

namespace TTC.Deployment.AmazonWebServices
{
    public class StackParameterReader
    {
        private readonly string _filePath;

        public StackParameterReader(string filePath)
        {
            _filePath = filePath;
        }

        public List<Parameter> Read()
        {
            var result = new List<Parameter>();

            using (var stream = new FileStream(_filePath, FileMode.Open))
            using (var sr = new StreamReader(stream))
            {
                var content = sr.ReadToEnd();
                if (string.IsNullOrWhiteSpace(content))
                    return result;

                var parametersInFile = JObject.Parse(content);

                foreach (var paramInFile in parametersInFile)
                {
                    var parameter = new Parameter { ParameterKey = paramInFile.Key, ParameterValue = paramInFile.Value.ToString() };
                    result.Add(parameter);
                }
            }

            return result;
        }
    }
}
