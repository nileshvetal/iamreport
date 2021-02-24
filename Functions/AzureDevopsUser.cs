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
    public static class AzureDevopsUser
    {
        [FunctionName("users")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            string organizationName = req.Query["organizationName"];
            if (string.IsNullOrEmpty(organizationName))
            {
                return new BadRequestObjectResult("Organization Name is not valid");
            }
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables()
            .Build();
            var azureDevopsUsername = config["azureDevopsUsername"];
            var perpetualAccessToken = config["perpetualAccessToken"];

            var users = GetUsers(organizationName, azureDevopsUsername, perpetualAccessToken);
            if (users == null)
            {
                return new BadRequestObjectResult("Something went wrong");
            }

            return new OkObjectResult(new
            {
                value = users
            });
        }

        private static List<AzureDevopsUserModel> GetUsers(string organizationName, string azureDevopsUsername, string perpetualAccessToken)
        {
            var azureDevopsUsers = new List<AzureDevopsUserModel>();
            var usersUrl = $"https://vssps.dev.azure.com/{organizationName}/_apis/graph/users?api-version=6.0-preview.1";

            var resultObject = SendGetRequest(usersUrl, azureDevopsUsername, perpetualAccessToken);
            if (resultObject == null) return null;

            var users = resultObject.value;
            for (int i = 0; i < users.Count; i++)
            {
                var user = users[i];
                var azureDevopsUser = new AzureDevopsUserModel();
                azureDevopsUser.principalDisplayName = user.displayName;
                azureDevopsUser.id = user.originId;

                azureDevopsUsers.Add(azureDevopsUser);
            }

            return azureDevopsUsers;
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
