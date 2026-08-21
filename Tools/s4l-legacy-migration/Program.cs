namespace S4LLegacyMigration;

internal static class Program
{
    // Legacy NetspherePirates MySQL dump -> OpenS4L PostgreSQL migration helper.
    // v1.0: reads a mysqldump --no-create-info (or --compatible=postgresql) data dump and
    // rewrites it into a form psql can apply. Because the exact legacy schema varies, this
    // tool is intentionally a guided, table-by-table converter rather than an opaque black box.
    //
    // Usage:
    //   S4LLegacyMigration <input.sql> [output.sql] [--table <name>]
    //   S4LLegacyMigration --help

    public static int Main(string[] args)
    {
        var help = args.Any(a => a is "--help" or "-h" or "/?");
        if (help || args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var input = args[0];
        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Input file not found: {input}");
            return 1;
        }

        var output = args.Length > 1 ? args[1] : Path.ChangeExtension(input, ".pg.sql");
        string? tableFilter = null;
        for (int i = 2; i < args.Length; i++)
            if (args[i] == "--table" && i + 1 < args.Length) tableFilter = args[i + 1];

        try
        {
            var converter = new DumpConverter();
            var result = converter.Convert(File.ReadAllText(input), tableFilter);
            File.WriteAllText(output, result);
            Console.WriteLine($"Wrote PostgreSQL SQL to {output}");
            Console.WriteLine($"  INSERT statements: {converter.InsertStatements}");
            Console.WriteLine($"  Unhandled lines:   {converter.UnhandledLines}");
            Console.WriteLine();
            Console.WriteLine("Apply with:  psql -h localhost -U postgres -d <db> -f \"" + output + "\"");
            Console.WriteLine("(Create the target databases first: auth and game.)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Conversion failed: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            S4 League Legacy Data Migration Tool
            ------------------------------------
            Converts a legacy NetspherePirates MySQL dump (data) into PostgreSQL-friendly SQL.

            Usage:
              S4LLegacyMigration <input.sql> [output.sql] [--table <name>]

            Options:
              <input.sql>      A mysqldump data dump (INSERT ... VALUES ...)
              <output.sql>     Optional output path (defaults to <input>.pg.sql)
              --table <name>   Only convert statements for the named table

            Notes:
              - Assumes a data dump of INSERT statements (mysqldump --no-create-info, or
                --compatible=postgresql --skip-extended-insert).
              - Rewrites MySQL-specific syntax: `identifier` backticks, \\' and \\" escapes,
                NULL, and boolean 0/1 literals.
              - Table/column names are lower-cased so unquoted PostgreSQL identifiers match.
              - It does NOT create tables; run the OpenS4L schema/migrations first, or create
                the destination tables yourself. Only then feed the generated SQL to psql.
            """);
    }
}
