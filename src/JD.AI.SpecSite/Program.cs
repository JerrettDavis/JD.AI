namespace JD.AI.SpecSite;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (SpecSiteCli.IsHelp(args))
            {
                Console.WriteLine(SpecSiteCli.BuildHelpText());
                return 0;
            }

            var options = SpecSiteCli.Parse(args);
            var catalogs = SpecificationCatalogLoader.Load(options);
            if (catalogs.Count == 0)
            {
                Console.Error.WriteLine($"No specification indexes found under '{options.SpecsRoot}'.");
                return 1;
            }

            SpecificationSiteWriter.Write(options, catalogs);
            if (options.EmitDocFx)
                DocFxCatalogWriter.Write(options, catalogs);

            var specCount = catalogs.Sum(catalog => catalog.Documents.Count);
            Console.WriteLine(
                $"Generated {specCount} specification pages across {catalogs.Count} spec types.");
            Console.WriteLine($"HTML output: {options.OutputRoot}");
            if (options.EmitDocFx)
                Console.WriteLine($"DocFX output: {options.DocFxOutputRoot}");

            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(SpecSiteCli.BuildHelpText());
            return 2;
        }
    }
}
