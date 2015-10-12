# AWS VPC deployment tool

Collection of command line tools for creating AWS clouds, pushing applications to S3 and deploying to AWS environments.  Basically a wrapper around the AWS CLI, allowing for execution in a synchronous fashion with friendlier log outputs.

## Non CodeDeploy Users

You probably want to use two commands: `AWSS3Push`, followed by `AWSProvisionCloud` (In that order)

## CodeDeploy Users

You will want to use : `AWSProvisionCloud` followed by `AWSPushAndDeploy`


TODO: Something about AWS Credentials

### Provision

Provisions a set of AWS resources based on a Cloud Formation template. Uses AWS CloudFormation

Requires:

 - __stackName__                *a name for your new stack built by Cloud Formation* 
 - __templatePath__             *the local path to your Cloud Formation template*

Optional:
 - __region__                   *AWSRegion one of us-east-1, us-west-1, us-west-2, etc*
 - __proxyHost__                *Host of proxy server if you need to use one*
 - __proxyPort__                *Port of proxy server if you need to use one*
 - __parametersFile__           *Path to JSON file which specifies stack paramater values*
 - __stackOutputFile__          *Path to JSON file which will save stack output values*

`AWSProvisionCloud --stackName MyStack --templatePath c:\some_app\example-windows-vpc-template.json`


### Push

Pushes your applications to deploy to an S3 bucket, ready to be deployed. _This command should be used if you are not using CodeDeploy_

Requires:

 - __applicationSetName__           *a name for your group of applications*
 - __version__                      *version number for your code*
 - __buildDirectoryPath__           *a path to the local directory containing your built application(s)*
 - __s3Bucket__                     *name of the s3Bucket you would like to push the build to (as defined in your code deploy trust file)*
 - __roleName__                     *name of IAM role to create or use for S3/CodeDeploy permissions*
 - __assumeRoleTrustDocument__      *local path to an IAM role trust file - see below*
 - __IAMRolePolicyDocumentPath__    *local path to an s3 policy file - see below*
 - __region__                       *AWSRegion one of us-east-1, us-west-1, us-west-2, etc*
 

`AWSS3Push  --version 1.1.2 --buildDirectoryPath C:\some_app\ExampleRevisions --applicationSetName someTestBuild --assumeRoleTrustDocument some_app\CodeDeployRole\code-deploy-policy.json --IAMRolePolicyDocumentPath some_app\CodeDeployRole\code-deploy-trust.json --bucketName testReleases`


### Push and Deploy

Pushes a version of your app to S3 and deploys it to a running stack. Uses AWS CodeDeploy. _This command should be used if you are using CodeDeploy to deploy your application_

For deploy to work, the EC2 instances must have a code deploy agent installed. This can be either baked into the server image, or installed in the userdata section of the cloud formation template.  On Windows, something like:

```
  "<script>\n",                           
    "powershell.exe New-Item -Path c:\\temp -ItemType \"directory\" -Force \n",
    "powershell.exe Read-S3Object -BucketName aws-codedeploy-us-east-1/latest -Key codedeploy-agent.msi -File c:\\temp\\codedeploy-agent.msi \n",
    "powershell.exe Start-Process -Wait -FilePath c:\\temp\\codedeploy-agent.msi -WindowStyle Hidden \n"
  "</script>\n"
```

It uploads applications zipped into folders named after their CodeDeploy DeploymentGroups.  It then CodeDeploys each group to all EC2 instances tagged as follows:

```
Name:  DeploymentRole  
Value: {{CodeDeploy_DeploymentGroup}}
```

So, if you have two different machines - one for a website, one for an internal api you may tag them

web layer

```
Name:  DeploymentRole  
Value: MyStack_Website
```

internal api layer

```
Name:  DeploymentRole  
Value: MyStack_API
```

See the tests for help with this.

Requires:

 - __stackName__                  *the name of the running Cloud Formation stack to deploy to*
 - __applicationSetName__         *the name of your group of applications - as specified when pushed to S3*
 - __version__                    *version number for your code - as specified when pushed to S3*
 - __buildDirectoryPath__         *a path to the local directory containing your built application(s)*
 - __assumeRoleTrustDocument__    *local path to an IAM role trust file - see below*
 - __IAMRolePolicyDocumentPath__  *local path to code deploy policy file - see below*
 - __s3Bucket__                   *name of the s3Bucket you would like to pick your build up from (as defined in your code deploy trust file)*
 - __roleName__                   *name of IAM role to create or use for S3/CodeDeploy permissions*
 - __profileName__                *name of the IAM profile to be used when accessing AWS API*
 - __profilesLocation__           *path to credentials file which contains all IAM profiles*
 - __deployToAutoScalingGroups__  *can be 'true' or 'false'. Defines if deployment is going to individual instances or deployment groups*

 Optional:
 - __proxyHost__                *Host of proxy server if you need to use one*
 - __proxyPort__                *Port of proxy server if you need to use one*
 - __regionEndpoint__			*AWSRegion one of us-east-1, us-west-1, us-west-2, etc*

`AWSPushAndDeploy  --version 1.1.2 --buildDirectoryPath .\TTC.Deployment.Tests\ExampleRevisions\HelloWorld --applicationSetName someTestBuild --IAMRolePolicyDocumentPath .\TTC.Deployment.Tests\CodeDeployRole\code-deploy-policy.json --assumeRoleTrustDocument .\TTC.Deployment.Tests\CodeDeployRole\code-deploy-policy.json --bucketName testReleases --stackName MyStack`


## IAM role policy

An IAM role policy document is required for AWSS3Push and AWSDeployCode.  If you using AWSS3Push and AWSDeployCode you will need to grant access to the appropriate S3 bucket (matching the bucket name you pass into the command), and ec2 instances and tags.  An example IAM policy document for both S3Push and Code deploy follows

```
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:ListBucket",
        "s3:GetBucketLocation"
      ],
      "Resource": "arn:aws:s3:::aws-test-releases"
    },
        {
            "Effect": "Allow",
            "Action": [
                "s3:PutObject",
                "s3:GetObject",
                "s3:DeleteObject"
            ],
            "Resource": "arn:aws:s3:::aws-test-releases/*"
        },
        {
      "Effect": "Allow",
      "Action": [
        "autoscaling:CompleteLifecycleAction",
        "autoscaling:DeleteLifecycleHook",
        "autoscaling:DescribeAutoScalingGroups",
        "autoscaling:DescribeLifecycleHooks",
        "autoscaling:PutLifecycleHook",
        "autoscaling:RecordLifecycleActionHeartbeat",
        "ec2:DescribeInstances",
        "ec2:DescribeInstanceStatus",
        "tag:GetTags",
        "tag:GetResources"
      ],
      "Resource": "*"
    }
  ]
}
```

The path to this file should be passed to the `--IAMRolePolicyDocumentPath` arguments

### IAM role trust
An IAM role trust document is required for AWSS3Push and AWSDeployCode. The path to this file should be passed to the `--assumeRoleTrustDocument` arguments. An example file follows

```
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "",
      "Effect": "Allow",
      "Principal": {
        "Service": [
          "codedeploy.us-east-1.amazonaws.com", 
          "codedeploy.us-west-2.amazonaws.com",
          "codedeploy.eu-west-1.amazonaws.com"
        ]
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

# CloudFormation templates

## Required Tags

If you intend to run the AWSPushAndDeploy command, you are expected to add a tag to each server (or autoscaling group) letting it know which application set will be deployed to the machine.

The tag key is "DeploymentRole" and its value should be #{stack_name}_#{application_set_name}. this can be done as follows:

```
"Tags": [
  {
    "Key": "DeploymentRole",
    "Value": {"Fn::Join":
      [
        "",
        [
          { "Ref": "AWS::StackName" },
          "_",
          "ApiLayer"
        ]
      ]
    }
  }
]
```

Where ApiLayer is the name of the deployment group for a bundle


## License

(The MIT License)

Copyright © Trafalgar Management Services Ltd

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the 'Software'), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.