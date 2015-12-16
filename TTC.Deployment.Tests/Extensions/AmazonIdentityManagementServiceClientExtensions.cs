using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using System;

namespace TTC.Deployment.Tests
{
    public static class AmazonIdentityManagementServiceClientExtensions
    {
        public static Role CreateRoleToAssume(this AmazonIdentityManagementServiceClient client, User user) {

            var roleName = "assume-role-" + DateTime.Now.ToFileTime();

            var role = client.CreateRole(new CreateRoleRequest
            {
                RoleName = roleName,
                AssumeRolePolicyDocument = @"{
                  ""Statement"":
                  [
                    {
                      ""Principal"":{""AWS"":""{AccountId}""},
                      ""Effect"":""Allow"",
                      ""Action"":[""sts:AssumeRole""]
                    }
                  ]
                }".Replace("{AccountId}", GetAWSAccountIdFromArn(user))
            }).Role;

            client.PutUserPolicy(new PutUserPolicyRequest
            {
                UserName = user.UserName,
                PolicyName = "assume-policy-42",
                PolicyDocument = @"{
                    ""Statement"":{
                        ""Effect"":""Allow"",
                        ""Action"":""sts:AssumeRole"",
                        ""Resource"":""{RoleARN}""
                    }
                }".Replace("{RoleARN}", role.Arn)
            });

            return role;

        }

        private static string GetAWSAccountIdFromArn(User user)
        {
            return user.Arn.Split(':')[4];
        }
    }
}
