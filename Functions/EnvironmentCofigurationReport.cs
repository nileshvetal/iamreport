using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

using Gcs.Model;

namespace Gcs.Report
{
    public static class EnvironmentCofigurationReport
    {
        [FunctionName("environmentreport")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            string subscriptionId = req.Query["subscriptionId"];
            var accessToken = GetOAuthToken(context);

            var environmentConfigurationReportList = GetAllResource(subscriptionId, accessToken);

            return new OkObjectResult(new
            {
                value = environmentConfigurationReportList
            });
        }

        private static List<EnvironmentConfigurationReportModel> GetAllResource(string subscriptionId, string accessToken)
        {
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/resources?api-version=2020-06-01";

            var resultObject = SendGetRequest(url, accessToken);
            var resourceArray = resultObject.value;

            var subscriptionName = GetSubscriptionName(subscriptionId, accessToken);
            var environmentConfigurationReportList = new List<EnvironmentConfigurationReportModel>();
            for (int i = 0; i < resourceArray.Count; i++)
            {
                var resource = resourceArray[i];
                var environmentConfigurationReport = new EnvironmentConfigurationReportModel();

                environmentConfigurationReport.subscriptionName = subscriptionName;
                environmentConfigurationReport.resourceName = resource.name;
                var resourceGroupName = GetResouceGroupName(resource.id.ToString());
                environmentConfigurationReport.resourceGroupName = resourceGroupName;
                environmentConfigurationReport.resourceLocation = resource.location;
                environmentConfigurationReport.resourceType = resource.type;
                var configurationDetails = GetConfigurationDetails(resource.id.ToString(), resource.type.ToString(), accessToken);
                environmentConfigurationReport.configurationDetails = configurationDetails;

                environmentConfigurationReportList.Add(environmentConfigurationReport);
            }
            return environmentConfigurationReportList;
        }

        private static string GetConfigurationDetails(string scope, string resourceType, string accessToken)
        {
            var configurationDetails = string.Empty;
            switch (resourceType)
            {
                case "Microsoft.Compute/virtualMachines":
                    var url = $"https://management.azure.com{scope}?api-version=2018-10-01";
                    var resourceResult = SendGetRequest(url, accessToken);
                    var vmSize = resourceResult.properties != null ? resourceResult.properties.hardwareProfile != null ?
                                resourceResult.properties.hardwareProfile.vmSize != null ?
                                resourceResult.properties.hardwareProfile.vmSize.ToString()
                                : string.Empty : string.Empty : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(vmSize) ? $"vmSize: {vmSize}," : configurationDetails;

                    var newtworkInterfaceScope = resourceResult.properties != null ?
                            resourceResult.properties.networkProfile != null ?
                            resourceResult.properties.networkProfile.networkInterfaces != null ?
                            resourceResult.properties.networkProfile.networkInterfaces[0] != null ?
                            resourceResult.properties.networkProfile.networkInterfaces[0].id != null ?
                            resourceResult.properties.networkProfile.networkInterfaces[0].id.ToString()
                            : string.Empty : string.Empty : string.Empty : string.Empty : string.Empty;
                    if (!string.IsNullOrEmpty(newtworkInterfaceScope))
                    {
                        var networkInterfacesUrl = $"https://management.azure.com{newtworkInterfaceScope}?api-version=2018-10-01";
                        var networkInterfacesResult = SendGetRequest(networkInterfacesUrl, accessToken);
                        var privateIp = networkInterfacesResult.properties != null ? networkInterfacesResult.properties.ipConfigurations != null ?
                        networkInterfacesResult.properties.ipConfigurations[0] != null ? networkInterfacesResult.properties.ipConfigurations[0].properties != null ?
                        networkInterfacesResult.properties.ipConfigurations[0].properties.privateIPAddress.ToString() : string.Empty : string.Empty : string.Empty : string.Empty;
                        configurationDetails = !string.IsNullOrEmpty(privateIp) ?
                                                 $"{configurationDetails} privateIPAddress: {privateIp}," : configurationDetails;

                        var publicIpScope = networkInterfacesResult.properties != null ?
                                networkInterfacesResult.properties.ipConfigurations != null ?
                                networkInterfacesResult.properties.ipConfigurations[0] != null ?
                                networkInterfacesResult.properties.ipConfigurations[0].properties != null ?
                                networkInterfacesResult.properties.ipConfigurations[0].properties.publicIPAddress != null ?
                                networkInterfacesResult.properties.ipConfigurations[0].properties.publicIPAddress.id != null ?
                                networkInterfacesResult.properties.ipConfigurations[0].properties.publicIPAddress.id.ToString()
                                : string.Empty : string.Empty : string.Empty : string.Empty : string.Empty : string.Empty;
                        if (!string.IsNullOrEmpty(publicIpScope))
                        {
                            var publicIpUrl = $"https://management.azure.com{publicIpScope}?api-version=2018-10-01";
                            var publicIpResult = SendGetRequest(publicIpUrl, accessToken);
                            var publicIpAddress = publicIpResult.properties != null ?
                                                        publicIpResult.properties.ipAddress != null ?
                                                        publicIpResult.properties.ipAddress.ToString()
                                                        : string.Empty : string.Empty;

                            configurationDetails = !string.IsNullOrEmpty(publicIpAddress) ?
                                                    $"{configurationDetails} publicIPAddress: {publicIpAddress}" : configurationDetails;
                        }
                    }
                    break;

                case "Microsoft.Compute/disks":
                    var diskUrl = $"https://management.azure.com{scope}?api-version=2020-12-01";
                    var diskResult = SendGetRequest(diskUrl, accessToken);
                    var sku = diskResult.sku != null ? diskResult.sku.ToString() : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(sku) ? $"sku: {sku}," : configurationDetails;

                    var diskSizeGB = diskResult.properties != null ? diskResult.properties.diskSizeGB != null ?
                                        diskResult.properties.diskSizeGB.ToString() : string.Empty : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(diskSizeGB) ? $"{configurationDetails} diskSizeGB: {diskSizeGB}," : configurationDetails;

                    var managedBy = diskResult.managedBy != null ? diskResult.managedBy.ToString() : string.Empty;
                    var resourceName = GetReourceName(managedBy);
                    configurationDetails = !string.IsNullOrEmpty(resourceName) ? $"{configurationDetails} managedBy: {resourceName}" : configurationDetails;
                    break;

                case "Microsoft.Sql/servers":
                    var sqlServerUrl = $"https://management.azure.com{scope}?api-version=2020-08-01-preview";
                    var sqlServerResult = SendGetRequest(sqlServerUrl, accessToken);

                    var version = sqlServerResult.properties != null ? sqlServerResult.properties.version != null ?
                                    sqlServerResult.properties.version.ToString() : string.Empty : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(version) ? $"version: {version}," : configurationDetails;

                    var fullyQualifiedDomainName = sqlServerResult.properties != null ?
                                            sqlServerResult.properties.fullyQualifiedDomainName != null ?
                                            sqlServerResult.properties.fullyQualifiedDomainName.ToString() : string.Empty : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(fullyQualifiedDomainName) ?
                                         $"{configurationDetails} fullyQualifiedDomainName: {fullyQualifiedDomainName}," : configurationDetails;

                    var administratorLogin = sqlServerResult.properties != null ?
                                                               sqlServerResult.properties.administratorLogin != null ?
                                                               sqlServerResult.properties.administratorLogin.ToString() : string.Empty : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(administratorLogin) ?
                                         $"{configurationDetails} administratorLogin: {administratorLogin}" : configurationDetails;
                    break;

                case "Microsoft.Sql/servers/elasticpools":
                    var sqlServerElasticUrl = $"https://management.azure.com{scope}?api-version=2020-08-01-preview";
                    var sqlServerElasticResult = SendGetRequest(sqlServerElasticUrl, accessToken);

                    var licenseType = sqlServerElasticResult.properties != null ?
                                    sqlServerElasticResult.properties.licenseType != null ?
                                    sqlServerElasticResult.properties.licenseType.ToString() : string.Empty : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(licenseType) ? $"licenseType: {licenseType}," : configurationDetails;

                    var elasticSku = sqlServerElasticResult.sku != null ? sqlServerElasticResult.sku.ToString() : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(elasticSku) ? $"{configurationDetails} sku: {elasticSku}," : configurationDetails;

                    var kind = sqlServerElasticResult.kind != null ? sqlServerElasticResult.kind.ToString() : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(kind) ? $"{configurationDetails} kind: {kind}" : configurationDetails;
                    break;

                case "Microsoft.Sql/servers/databases":
                    var sqlServerDbUrl = $"https://management.azure.com{scope}?api-version=2020-08-01-preview";
                    var sqlServerDbResult = SendGetRequest(sqlServerDbUrl, accessToken);

                    var saType = sqlServerDbResult.properties != null ?
                                 sqlServerDbResult.properties.storageAccountType != null ?
                                 sqlServerDbResult.properties.storageAccountType.ToString() : string.Empty : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(saType) ? $"storageAccountType: {saType}," : configurationDetails;

                    var dbKind = sqlServerDbResult.kind != null ? sqlServerDbResult.kind.ToString() : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(dbKind) ? $"{configurationDetails} kind: {dbKind}," : configurationDetails;

                    var dbSku = sqlServerDbResult.sku != null ? sqlServerDbResult.sku.ToString() : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(dbSku) ? $"{configurationDetails} sku: {dbSku}," : configurationDetails;

                    var elasticPoolId = sqlServerDbResult.properties != null ?
                                 sqlServerDbResult.properties.elasticPoolId != null ?
                                 sqlServerDbResult.properties.elasticPoolId.ToString() : string.Empty : string.Empty;
                    var stringArray = elasticPoolId.Split(new string[] { "/" }, StringSplitOptions.None);
                    var elasticPoolName = stringArray.Length > 9 ? stringArray[10] : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(elasticPoolName) ? $"{configurationDetails} elasticPoolId: {elasticPoolName}," : configurationDetails;
                    break;

                case "Microsoft.Storage/storageAccounts":
                    var saUrl = $"https://management.azure.com{scope}?api-version=2020-08-01-preview";
                    var saResult = SendGetRequest(saUrl, accessToken);

                    var accessTier = saResult.properties != null ?
                                 saResult.properties.accessTier != null ?
                                 saResult.properties.accessTier.ToString() : string.Empty : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(accessTier) ? $"accessTier: {accessTier}," : configurationDetails;

                    var saKind = saResult.kind != null ? saResult.kind.ToString() : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(saKind) ? $"{configurationDetails} kind: {saKind}," : configurationDetails;

                    var saSku = saResult.sku != null ? saResult.sku.ToString() : string.Empty;
                    configurationDetails = !string.IsNullOrEmpty(saSku) ? $"{configurationDetails} sku: {saSku}" : configurationDetails;
                    break;
            }

            return configurationDetails;
        }

        private static string GetReourceName(string scope)
        {
            var stringArray = scope.Split(new string[] { "/" }, StringSplitOptions.None);

            return stringArray.Length > 7 ? stringArray[8] : string.Empty;
        }

        private static string GetResouceGroupName(string scope)
        {
            var stringArray = scope.Split(new string[] { "/" }, StringSplitOptions.None);
            return stringArray[4];
        }

        private static string GetSubscriptionName(string subscriptionId, string accessToken)
        {
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}?api-version=2020-01-01";

            var resultObject = SendGetRequest(url, accessToken);
            return resultObject.displayName;
        }

        private static string GetOAuthToken(ExecutionContext context)
        {
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables()
            .Build();

            var tenantId = config["tenantId"];
            var clientId = config["clientId"];
            // var clientSecret = config["clientSecret"];
            var userName = config["azureUserName"];
            var password = config["password"];


            var url = $"https://login.microsoftonline.com/{tenantId}/oauth2/token";

            var contentList = new List<KeyValuePair<string, string>>();
            contentList.Add(new KeyValuePair<string, string>("grant_type", "password"));
            contentList.Add(new KeyValuePair<string, string>("client_id", clientId));
            contentList.Add(new KeyValuePair<string, string>("username", userName));
            contentList.Add(new KeyValuePair<string, string>("password", password));
            contentList.Add(new KeyValuePair<string, string>("scope", "openid"));
            contentList.Add(new KeyValuePair<string, string>("resource", "https://management.azure.com/"));
            var httpContent = new FormUrlEncodedContent(contentList);

            HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = httpClient.PostAsync(url, httpContent).Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.StatusCode.ToString());
            }

            var responseJson = response.Content.ReadAsStringAsync().Result;
            var responseObject = JsonConvert.DeserializeObject<dynamic>(responseJson);

            return responseObject.access_token;
        }

        private static dynamic SendGetRequest(string url, string accessToken)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = httpClient.GetAsync(url).Result;
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.StatusCode.ToString());
            }

            var responseJson = response.Content.ReadAsStringAsync().Result;
            var responseObject = JsonConvert.DeserializeObject<dynamic>(responseJson);

            return responseObject;
        }
    }
}
