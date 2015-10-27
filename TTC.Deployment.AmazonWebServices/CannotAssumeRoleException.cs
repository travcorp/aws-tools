using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.CloudFormation.Model;

namespace TTC.Deployment.AmazonWebServices
{
    public class CannotAssumeRoleException : Exception
    {
        public CannotAssumeRoleException(string roleToAssumeARN)
            : base(string.Format("Current credentials can not assume role: {0}", roleToAssumeARN))
        {
        }
    }
}