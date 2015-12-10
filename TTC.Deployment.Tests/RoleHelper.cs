using System;
using Amazon.IdentityManagement.Model;
using Amazon.IdentityManagement;
using Amazon.Runtime;

namespace TTC.Deployment.Tests
{
    internal class RoleHelper
    {
        private AmazonIdentityManagementServiceClient _iamClient;
        private User _user;

        internal RoleHelper(AmazonIdentityManagementServiceClient iamClient)
        {
            _iamClient = iamClient;
        }
        public  Role CreateRoleForUserToAssume(User user)
        {
            _user = user;
            var roleName = "assume-role-" + DateTime.Now.ToFileTime();

            var role = _iamClient.CreateRole(new CreateRoleRequest
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

            _iamClient.PutUserPolicy(new PutUserPolicyRequest
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

            var accessKey = _iamClient.CreateAccessKey(new CreateAccessKeyRequest { UserName = user.UserName }).AccessKey;
            var userCredentials = new BasicAWSCredentials(accessKey.AccessKeyId, accessKey.SecretAccessKey);
            return role;

        }

        public void DeleteRole(string roleName)
        {
            try
            {
                _iamClient.GetRole(new GetRoleRequest { RoleName = roleName });
            }
            catch (NoSuchEntityException)
            {
                return;
            }

            var rolePoliciesResponse = _iamClient.ListRolePolicies(new ListRolePoliciesRequest { RoleName = roleName });

            foreach (var p in rolePoliciesResponse.PolicyNames)
            {
                var request = new DeleteRolePolicyRequest { PolicyName = p, RoleName = roleName };
                IgnoringNoSuchEntity(() => _iamClient.DeleteRolePolicy(request));
            }

            IgnoringNoSuchEntity(() => _iamClient.DeleteRole(new DeleteRoleRequest { RoleName = roleName }));
        }

        public void DeleteUser(string userName)
        {
            try
            {
                var userPolicies =
                    _iamClient.ListUserPolicies(new ListUserPoliciesRequest { UserName = userName }).PolicyNames;

                foreach (var policy in userPolicies)
                {
                    var request = new DeleteUserPolicyRequest { UserName = userName, PolicyName = policy };
                    IgnoringNoSuchEntity(() => { _iamClient.DeleteUserPolicy(request); });
                }
            }
            catch (NoSuchEntityException)
            {
                return;
            }

            var keys = _iamClient.ListAccessKeys(new ListAccessKeysRequest { UserName = userName });

            foreach (var key in keys.AccessKeyMetadata)
            {
                var request = new DeleteAccessKeyRequest { UserName = userName, AccessKeyId = key.AccessKeyId };
                IgnoringNoSuchEntity(() => { _iamClient.DeleteAccessKey(request); });
            }

            IgnoringNoSuchEntity(() => _iamClient.DeleteUser(new DeleteUserRequest { UserName = userName }));
        }

        private void IgnoringNoSuchEntity(Action action)
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

        private  string GetAWSAccountIdFromArn(User user)
        {
            var tokens = user.Arn.Split(':');
            return tokens[4];
            // yep - you heard me
        }
    }
}