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

                var connection = new CrmServiceClient(config.ConnectionString);

                var solution = GetSolution(connection, config.SolutionName);

                var webResources = GetFiles(config, solution.CustomizationPrefix);

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

        private WebResource[] GetFiles(Config config, string customizationPrefix)
        {
            if (!Directory.Exists(config.Path))
            {
                throw new ArgumentException("Specified directory does not exist. Please check your configuration.");
            }

            var singleFilesFilter = false;
            if (config.Files != null && config.Files.Length > 0)
            {
                singleFilesFilter = true;
                config.Files = config.Files.Select(f => Path.GetFullPath(Path.Combine(config.Path, f))).ToArray();
            }

            var ignoreFilter = false;
            if (config.Ignore != null && config.Ignore.Length > 0)
            {
                ignoreFilter = true;
                config.Ignore = config.Ignore.Select(f => Path.GetFullPath(Path.Combine(config.Path, f))).ToArray();
            }

            if (config.Overrides != null && config.Overrides.Length > 0)
            {
                if (config.Overrides.Any(o => string.IsNullOrEmpty(o.File)))
                {
                    throw new ArgumentException("Encountered an Override without the file path fragment specified. Please review your config and try again.");
                }

                foreach (var o in config.Overrides)
                {
                    o.File = Path.GetFullPath(Path.Combine(config.Path, o.File));
                }
            }

            return Constants.ValidExtensions
                .SelectMany(extension => Directory.GetFiles(config.Path, $"*.{extension}", SearchOption.AllDirectories))
                .Distinct()
                .Where(resourcePath => singleFilesFilter ? config.Files.Any(f => f == resourcePath) : true)
                .Where(resourcePath => ignoreFilter ? !config.Ignore.Any(f => f == resourcePath) : true)
                .Select(resourcePath => new WebResource(config, resourcePath, customizationPrefix))
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
