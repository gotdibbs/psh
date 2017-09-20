namespace Psh.Interface
{
    internal class Config
    {
        public string ConnectionString { get; set; }

        public bool DryRun { get; set; }

        public string[] Files { get; set; }

        public string Path { get; set; }

        private string _rootNamespace;
        public string RootNamespace
        {
            get { return _rootNamespace; }
            set
            {
                if (_rootNamespace == value)
                {
                    return;
                }

                if (!value.StartsWith("/"))
                {
                    value = "/" + value;
                }
                if (value.EndsWith("/"))
                {
                    value = value.Remove(value.Length - 1);
                }

                _rootNamespace = value;
            }
        }

        public string SolutionName { get; set; }

        public string[] Ignore { get; set; }

        public Override[] Overrides { get; set; }
    }
}
