using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Text;

using Gcs.Model;

namespace Gcs.Report
{
    public static class GroupMembershipReport
    {
        [FunctionName("groupmembershipreport")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            string organizationName = req.Query["organizationName"];
            string userId = req.Query["userId"];
            if (string.IsNullOrEmpty(organizationName) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Organization Name or User is not valid");
            }
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables()
            .Build();
            var azureDevopsUsername = config["azureDevopsUsername"];
            var perpetualAccessToken = config["perpetualAccessToken"];

            var groupMembershipReportList = GetGroupMembershipReport(organizationName, userId, azureDevopsUsername, perpetualAccessToken);
            if (groupMembershipReportList == null)
            {
                return new BadRequestObjectResult("Something went wrong");
            }

            return new OkObjectResult(new
            {
                value = groupMembershipReportList
            });
        }

        private static List<GroupMembershipReportModel> GetGroupMembershipReport(string organizationName, string userId, string azureDevopsUsername, string perpetualAccessToken)
        {
            var groupMembershipReportList = new List<GroupMembershipReportModel>();
            var usersUrl = $"https://vssps.dev.azure.com/{organizationName}/_apis/graph/users?api-version=6.0-preview.1";
			// Getting all users
            var resultObject = SendGetRequest(usersUrl, azureDevopsUsername, perpetualAccessToken);
            if (resultObject == null) return null;

            var users = resultObject.value;
            var subjectDescriptor = string.Empty;
            var principalDisplayName = string.Empty;
            var principalEmail = string.Empty;
            for (int i = 0; i < users.Count; i++)
            {
                var user = users[i];
                if (string.Equals(userId, user.originId.ToString()))
                {
                    subjectDescriptor = user.descriptor.ToString();
                    principalDisplayName = user.displayName.ToString();
                    principalEmail = user.mailAddress.ToString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(subjectDescriptor))
            {
                return groupMembershipReportList;
            }

            var groupUrl = $"https://vssps.dev.azure.com/{organizationName}/_apis/graph/Memberships/{subjectDescriptor}?api-version=6.0-preview.1";
            var groupResultObject = SendGetRequest(groupUrl, azureDevopsUsername, perpetualAccessToken);
            if (groupResultObject == null) return null;

            var groups = groupResultObject.value;

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var groupDetailLink = group._links != null ? group._links.container != null ?
                                group._links.container.href != null ? group._links.container.href.ToString() :
                                string.Empty : string.Empty : string.Empty;

                var groupDetails = SendGetRequest(groupDetailLink, azureDevopsUsername, perpetualAccessToken);
                if (groupDetails == null) return null;

                var groupMembershipReport = new GroupMembershipReportModel();
                groupMembershipReport.principalDisplayName = principalDisplayName;
                groupMembershipReport.principalEmail = principalEmail;
                groupMembershipReport.organizationName = organizationName;
                groupMembershipReport.projectName = GetProjectName(groupDetails.principalName.ToString());
                groupMembershipReport.groupDisplayName = groupDetails.displayName.ToString();
                groupMembershipReport.groupDescription = groupDetails.description.ToString();

                groupMembershipReportList.Add(groupMembershipReport);
            }
            return groupMembershipReportList;
        }

        private static string GetProjectName(string groupPrincipalName)
        {
            var stringArray = groupPrincipalName.Split(new string[] { "[" }, StringSplitOptions.None)[1]
                                    .Split(new string[] { "]" }, StringSplitOptions.None);
            return stringArray[0];
        }

        private static dynamic SendGetRequest(string url, string azureDevopsUsername, string perpetualAccessToken)
        {
            HttpClient httpClient = new HttpClient();
            var byteArray = Encoding.ASCII.GetBytes($"{azureDevopsUsername}:{perpetualAccessToken}");
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            HttpResponseMessage response = httpClient.GetAsync(url).Result;
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseJson = response.Content.ReadAsStringAsync().Result;
            var responseObject = JsonConvert.DeserializeObject<dynamic>(responseJson);

            return responseObject;
        }
    }
}
