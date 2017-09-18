using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Psh.Interface
{
    internal class WebResource
    {
        public string Namespace { get; set; }

        public string Name
        {
            get
            {
                return $"{CustomizationPrefix}_{Namespace}";
            }
        }

        public string Description { get; set; }

        public string FilePath { get; set; }

        public WebResourceType WebResourceType { get; set; }

        public string CustomizationPrefix { get; set; }

        public Guid? Id { get; set; }

        public bool Create { get; set; }

        public WebResource(string sourceDirectory, string resourcePath, string customizationPrefix, string rootNamespace)
        {
            FilePath = resourcePath;
            CustomizationPrefix = customizationPrefix;

            if (File.Exists(FilePath + ".psh"))
            {
                var config = JsonConvert.DeserializeObject<Override>(File.ReadAllText(FilePath + ".psh"));

                if (config == null || string.IsNullOrEmpty(config.Namespace))
                {
                    throw new ArgumentException($"Encountered an override for {FilePath} but it was invalid. Please review and try again.");
                }

                Namespace = config.Namespace;
                Description = config.Description;
            }

            if (string.IsNullOrEmpty(Namespace))
            {
                Namespace = resourcePath
                        .Replace(sourceDirectory, string.Empty)
                        .Replace("\\", "/");

                if (!string.IsNullOrEmpty(rootNamespace))
                {
                    if (!rootNamespace.StartsWith("/"))
                    {
                        rootNamespace = "/" + rootNamespace;
                    }
                    if (rootNamespace.EndsWith("/"))
                    {
                        rootNamespace = rootNamespace.Remove(rootNamespace.Length - 1);
                    }

                    Namespace = rootNamespace + Namespace;
                }
            }

            WebResourceType = ConvertStringExtension(Path.GetExtension(Namespace));

            Validate();
        }

        private WebResourceType ConvertStringExtension(string extensionValue)
        {
            switch (extensionValue.Replace(".", string.Empty).ToLower())
            {
                case "css":
                    return WebResourceType.Css;
                case "xml":
                    return WebResourceType.Xml;
                case "gif":
                    return WebResourceType.Gif;
                case "htm":
                    return WebResourceType.Html;
                case "html":
                    return WebResourceType.Html;
                case "ico":
                    return WebResourceType.Png;
                case "jpg":
                    return WebResourceType.Jpg;
                case "png":
                    return WebResourceType.Png;
                case "js":
                    return WebResourceType.JScript;
                case "xsl":
                    return WebResourceType.Stylesheet_XSL;
                default:
                    throw new ArgumentOutOfRangeException($"{extensionValue} is not recognized as a valid file extension for a Web Resource.");
            }
        }

        public void Validate()
        {
            Regex inValidWRNameRegex = new Regex("[^a-z0-9A-Z_\\./]|[/]{2,}",
                (RegexOptions.Compiled | RegexOptions.CultureInvariant));

            // Test valid characters
            if (inValidWRNameRegex.IsMatch(Namespace))
            {
                throw new Exception($"File at path '{FilePath}' contains an invalid character. Please check for invalid characters, rename the file, and try again.");
            }

            // Test length
            if (Namespace.Length > 100)
            {
                throw new Exception($"File at path '{FilePath}' results in a name that is too long. Please consider renaming the file or reorganizing your project directories.");
            }
        }

        public Entity ToEntity()
        {
            if (string.IsNullOrEmpty(CustomizationPrefix))
            {
                throw new ArgumentNullException("Received invalid customization prefix, cannot continue");
            }

            var entity = new Entity(Constants.WebResourceLogicalName);
            entity["content"] = GetContents();
            entity["description"] = Description;
            entity["name"] = Name;
            entity["displayname"] = Path.GetFileName(Namespace);
            entity["webresourcetype"] = new OptionSetValue((int)WebResourceType);

            if (Id != null)
            {
                entity["webresourceid"] = entity.Id = Id.Value;
            }

            return entity;
        }

        private string GetContents()
        {
            // TODO: allow minification switch

            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(FilePath)));
        }
    }
}