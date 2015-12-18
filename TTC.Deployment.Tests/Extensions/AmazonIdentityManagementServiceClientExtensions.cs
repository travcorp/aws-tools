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

        public static void DeleteRole(this AmazonIdentityManagementServiceClient client, string roleName) {
            try
            {
                client.GetRole(new GetRoleRequest { RoleName = roleName });
            }
            catch (NoSuchEntityException)
            {
                return;
            }

            var rolePoliciesResponse = client.ListRolePolicies(new ListRolePoliciesRequest { RoleName = roleName });

            foreach (var p in rolePoliciesResponse.PolicyNames)
            {
                var request = new DeleteRolePolicyRequest { PolicyName = p, RoleName = roleName };
                IgnoringNoSuchEntity(() => client.DeleteRolePolicy(request));
            }

            IgnoringNoSuchEntity(() => client.DeleteRole(new DeleteRoleRequest { RoleName = roleName }));
        }

        public static void DeleteUser(this AmazonIdentityManagementServiceClient client, string userName)
        {
            try
            {
                var userPolicies =
                    client.ListUserPolicies(new ListUserPoliciesRequest { UserName = userName }).PolicyNames;

                foreach (var policy in userPolicies)
                {
                    var request = new DeleteUserPolicyRequest { UserName = userName, PolicyName = policy };
                    IgnoringNoSuchEntity(() => { client.DeleteUserPolicy(request); });
                }
            }
            catch (NoSuchEntityException)
            {
                return;
            }

            var keys = client.ListAccessKeys(new ListAccessKeysRequest { UserName = userName });

            foreach (var key in keys.AccessKeyMetadata)
            {
                var request = new DeleteAccessKeyRequest { UserName = userName, AccessKeyId = key.AccessKeyId };
                IgnoringNoSuchEntity(() => { client.DeleteAccessKey(request); });
            }

            IgnoringNoSuchEntity(() => client.DeleteUser(new DeleteUserRequest { UserName = userName }));
        }

        public static void IgnoringNoSuchEntity(Action action)
        {
            try
            {
                action();
            }
            catch (NoSuchEntityException)
            {
                Console.WriteLine("Ignoring no such entity...");
            }
        }

        private static string GetAWSAccountIdFromArn(User user)
        {
            return user.Arn.Split(':')[4];
        }
    }
}
