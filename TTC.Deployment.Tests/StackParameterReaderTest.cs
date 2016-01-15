using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon.CloudFormation.Model;
using NUnit.Framework;
using Newtonsoft.Json;
using TTC.Deployment.AmazonWebServices;

namespace TTC.Deployment.Tests
{
    [TestFixture]
    public class StackParameterReaderTest
    {
        [Test]
        public void Read_WhenFileEmpty_ReturnsEmptyList()
        {
            var filePath = "ReadsOneParameter.txt";
            var content = "";

            CreateFile(filePath, content);

            try
            {
                var reader = new StackParameterReader(filePath);
                List<Parameter> parameters = reader.Read();

                Assert.IsNotNull(parameters);
                Assert.AreEqual(0, parameters.Count);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Test]
        public void Read_WhenEmptyJson_ReturnsEmptyList()
        {
            var filePath = "ReadsOneParameter.txt";
            var content = "{}";

            CreateFile(filePath, content);

            try
            {
                var reader = new StackParameterReader(filePath);
                List<Parameter> parameters = reader.Read();

                Assert.IsNotNull(parameters);
                Assert.AreEqual(0, parameters.Count);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Test]
        public void Read_WhenInvalidJson_Throws()
        {
            var filePath = "ReadsOneParameter.txt";
            var content = "{abc}";

            CreateFile(filePath, content);

            try
            {
                var reader = new StackParameterReader(filePath);

                Assert.Throws<JsonReaderException>(() => reader.Read());
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Test]
        public void Read_WhenOneParameter_ReturnsIt()
        {
            var filePath = "ReadsOneParameter.txt";
            var content = @"{""key1"" : ""value1""}";

            CreateFile(filePath, content);

            try
            {
                var reader = new StackParameterReader(filePath);
                List<Parameter> parameters = reader.Read();

                Assert.AreEqual(1, parameters.Count);
                Assert.AreEqual("key1", parameters[0].ParameterKey);
                Assert.AreEqual("value1", parameters[0].ParameterValue);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        [Test]
        public void Read_WhenMultipmePatameters_ReturnsThemAll()
        {
            var filePath = "ReadsOneParameter.txt";
            var content =
                @"{
                    ""key1"" : ""value1"",
                    ""key2"" : ""value2""
                }";

            CreateFile(filePath, content);

            try
            {
                var reader = new StackParameterReader(filePath);
                List<Parameter> parameters = reader.Read();

                Assert.AreEqual(2, parameters.Count);
                Assert.AreEqual("key1", parameters[0].ParameterKey);
                Assert.AreEqual("value1", parameters[0].ParameterValue);
                Assert.AreEqual("key2", parameters[1].ParameterKey);
                Assert.AreEqual("value2", parameters[1].ParameterValue);
            }
            finally
            {
                DeleteFile(filePath);
            }
        }

        private void CreateFile(string filePath, string content)
        {
            using (var file = File.Create(filePath))
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                file.Write(bytes, 0, bytes.Length);
            }
        }

        private void DeleteFile(string filePath)
        {
            File.Delete(filePath);
        }
    }
}
