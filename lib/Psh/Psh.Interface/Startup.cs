using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Psh.Interface
{
    public class Startup
    {
        public async Task<object> Invoke(dynamic sourceConfig)
        {
            try
            {
                var config = ConvertDynamic<Config>(sourceConfig);
                string connectionString = config.ConnectionString;
                string solutionName = config.SolutionName;
                string path = config.Path;

                var connection = new CrmServiceClient(connectionString);

                var solution = GetSolution(connection, config.SolutionName);

                var webResources = GetFiles(path, config.Files, solution.CustomizationPrefix, config.RootNamespace);

                foreach (var resource in webResources)
                {
                    var existingId = GetExisting(connection, resource.Name);

                    if (existingId != null)
                    {
                        resource.Id = existingId;
                    }
                    else
                    {
                        resource.Create = true;
                    }

                    if (!config.DryRun)
                    {
                        Upsert(connection, resource, config.SolutionName);
                    }
                }

                if (!config.DryRun)
                {
                    Publish(connection, webResources);
                }

                return GetLog(webResources);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        private T ConvertDynamic<T>(IDictionary<string, object> dictionary)
        {
            var source = Newtonsoft.Json.JsonConvert.SerializeObject(dictionary);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(source);
        }

        private Solution GetSolution(CrmServiceClient connection, string solutionName)
        {
            var query = $@"<fetch count='1'>
	                        <entity name='solution'>
		                        <attribute name='publisherid' />
                                <attribute name='uniquename' />
                                <attribute name='friendlyname' />
                                <filter>
                                    <condition attribute='ismanaged' operator='eq' value='false' />
                                    <condition attribute='uniquename' operator='ne' value='Active' />
                                    <condition attribute='uniquename' operator='ne' value='Basic' />
                                    <condition attribute='uniquename' operator='eq' value='{solutionName}' />
                                </filter>
                                <link-entity name='publisher' from='publisherid' to='publisherid' alias='publisher'>
                                    <attribute name='customizationprefix' />
                                </link-entity>
	                        </entity>
                        </fetch>";

            var result = connection.RetrieveMultiple(new FetchExpression(query));

            if (result == null || result.Entities == null || result.Entities.Count != 1)
            {
                throw new Exception($"Could not locate the specified solution '{solutionName}'. Please validate and try again.");
            }

            if (!result.Entities[0].Contains("publisher.customizationprefix"))
            {
                throw new Exception($"Could not locate the publisher prefix for the specified solution '{solutionName}'. Please validate and try again.");
            }

            var solution = result.Entities.First();

            return new Solution
            {
                CustomizationPrefix = (string)((AliasedValue)solution["publisher.customizationprefix"]).Value,
                Id = solution.Id
            };
        }

        private WebResource[] GetFiles(string sourceDirectory, string[] files, string customizationPrefix, string rootNamespace)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new ArgumentException("Specified directory does not exist. Please check your configuration.");
            }

            var filterFiles = false;
            if (files != null && files.Length > 0)
            {
                filterFiles = true;
                files = files.Select(f => Path.GetFullPath(Path.Combine(sourceDirectory, f))).ToArray();
            }

            return Constants.ValidExtensions
                .SelectMany(extension => Directory.GetFiles(sourceDirectory, $"*.{extension}", SearchOption.AllDirectories))
                .Where(resourcePath => filterFiles ? files.Any(f => f == resourcePath) : true)
                .Select(resourcePath => new WebResource(sourceDirectory, resourcePath, customizationPrefix, rootNamespace))
                .ToArray();
        }

        private Guid? GetExisting(CrmServiceClient connection, string name)
        {
            var query = $@"<fetch count='1'>
	                        <entity name='webresource'>
		                        <attribute name='displayname' />
                                <attribute name='name' />
                                <attribute name='webresourceid' />
                                <filter>
                                    <condition attribute='name' operator='eq' value='{name}' />
                                </filter>
	                        </entity>
                        </fetch>";

            var result = connection.RetrieveMultiple(new FetchExpression(query));

            if (result == null || result.Entities == null || result.Entities.Count != 1)
            {
                return null;
            }

            return result.Entities[0].Id;
        }

        private void Upsert(CrmServiceClient connection, WebResource resource, string solutionName)
        {
            var entity = resource.ToEntity();

            if (resource.Create)
            {
                var id = connection.Create(entity);
                resource.Id = id;
            }
            else
            {
                connection.Update(entity);
            }

            if (solutionName != "Default")
            {
                connection.Execute(new AddSolutionComponentRequest
                {
                    ComponentType = (int)ComponentType.WebResource,
                    SolutionUniqueName = solutionName,
                    ComponentId = resource.Id.Value
                });
            }
        }

        private void Publish(CrmServiceClient connection, WebResource[] webResources)
        {
            var resouresToPublish = webResources.Where(wr => !wr.Create);

            var paramXml = new StringBuilder();
            paramXml.AppendLine("<importexportxml>");
            paramXml.AppendLine("   <entities />");
            paramXml.AppendLine("   <optionsets />");
            paramXml.AppendLine("   <webresources>");
            foreach (var resource in resouresToPublish)
            {
                paramXml.AppendLine($"       <webresource>{resource.Id.ToString()}</webresource>");
            }
            paramXml.AppendLine("   </webresources>");
            paramXml.AppendLine("</importexportxml>");

            connection.Execute(new PublishXmlRequest()
            {
                ParameterXml = paramXml.ToString()
            });
        }

        private string GetLog(WebResource[] webResources)
        {
            return string.Join("\n\r",
                webResources.Select(wr => $"Web Resource '{wr.Name}' {GetAction(wr.Create)} from '{wr.FilePath}'."));
        }

        private string GetAction(bool isCreate)
        {
            return isCreate ? "created" : "updated";
        }
    }
}
