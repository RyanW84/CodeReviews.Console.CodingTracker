using Microsoft.Extensions.Configuration;
using TCSS.Console.CodingTracker;

namespace CodeTracker.RyanW84;

internal class Program
{
    static void Main(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        string connectionString = configuration.GetSection("ConnectionStrings")[
            "DefaultConnection"
        ];

        Console.WriteLine(connectionString);

        var dataAccess = new DataAccess();

        dataAccess.CreateDatabase();

        //SeedData.SeedRecords(20); // Commented out as it adds 20 records every time it is called

        UserInterface.MainMenu();
    }
}
