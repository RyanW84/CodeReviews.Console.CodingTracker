using System.ComponentModel.DataAnnotations;
using CodeTracker.RyanW84;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using static TCSS.Console.CodingTracker.Enums;

namespace TCSS.Console.CodingTracker;

internal static class SeedData
{
    internal static void SeedRecords(int count)
    {
        Random random = new Random();
        DateTime currentDate = DateTime.Now.Date; // Todays date

        List<CodingRecord> records = new List<CodingRecord>();

        for (int i = 1; i <= count; i++)
        {
            //in order to generate 0-12 hours, you have to set the max value as 13
            DateTime startDate = currentDate.AddHours(random.Next(13));
            DateTime endDate = currentDate.AddHours(random.Next(13));

            if (startDate >= endDate)
            {
                (startDate, endDate) = (endDate, startDate); //Tuple swap added to avoid negative entries
            }

            records.Add(
                new CodingRecord
                {
                    Id = i,
                    DateStart = startDate,
                    DateEnd = endDate,
                }
            );

            //Increment the date for the next record
            currentDate = currentDate.AddDays(1);
        }

        var dataAccess = new DataAccess();
        dataAccess.BulkInsertRecords(records);
    }
}

internal class DataAccess
{
    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();

    private string ConnectionString;

    public DataAccess()
    {
        ConnectionString = configuration.GetSection("ConnectionStrings")["DefaultConnection"];
    }

    internal void CreateDatabase()
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();

            string createTableQuery =
                @"
CREATE TABLE IF NOT EXISTS records (
Id integer PRIMARY KEY AUTOINCREMENT,
DateStart TEXT NOT NULL,
DateEnd TEXT NOT NULL
)";

            connection.Execute(createTableQuery);
        }
    }

    internal void InsertRecord(CodingRecord record)
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();

            string insertQuery =
                @"
INSERT INTO records (DateStart,DateEnd)
VALUES (@DateStart, @DateEnd)";

            connection.Execute(insertQuery, new { record.DateStart, record.DateEnd }); // This highlighted an error until I added the Using Dapper line at the top.
        }
    }

    internal IEnumerable<CodingRecord> GetAllRecords() // Ienumerable is used as it is quick and easy to view data, read only forward only - more efficient than a list, as a list has to load itself fully into memory first.
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();

            string selectQuery = "SELECT * FROM records";

            var records = connection.Query<CodingRecord>(selectQuery);

            foreach (var record in records)
            {
                record.Duration = record.DateEnd - record.DateStart;
            }

            return records;
        }
    }

    internal void BulkInsertRecords(List<CodingRecord> records)
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();

            //Prepare the query for placeholders with multiple records
            string insertQuery =
                @"
INSERT INTO records (DateStart, DateEnd)
VALUES (@DateStart, @DateEnd)";

            // Excecute the query for each record
            connection.Execute(
                insertQuery,
                records.Select(record => new { record.DateStart, record.DateEnd })
            );
        }
    }

    internal void UpdateRecord(CodingRecord updatedRecord)
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();

            string updateQuery =
                @"
UPDATE records
SET DateStart=@DateStart, DateEnd=@DateEnd
WHERE Id = @Id";

            connection.Execute(
                updateQuery,
                new
                {
                    updatedRecord.DateStart,
                    updatedRecord.DateEnd,
                    updatedRecord.Id,
                }
            );
        }
    }

    internal int DeleteRecord(int recordId)
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();

            string deleteQuery = "DELETE FROM records WHERE Id = @Id";

            int rowsAffected = connection.Execute(deleteQuery, new { Id = recordId });

            return rowsAffected;
        }
    }
}

internal class Enums
{
    internal enum MainMenuChoices
    {
        [Display(Name = "Add Record")]
        AddRecord,

        [Display(Name = "View Records")]
        ViewRecords,

        [Display(Name = "Update Record")]
        UpdateRecord,

        [Display(Name = "Delete Record")]
        DeleteRecord,

        Quit,
    }
}

internal static class UserInterface
{
    private static string GetEnumDisplayName(Enum enumValue) //Enums weren't showing their display name
    {
        var displayAttribute =
            enumValue
                .GetType()
                .GetField(enumValue.ToString())
                .GetCustomAttributes(typeof(DisplayAttribute), false)
                .FirstOrDefault() as DisplayAttribute;

        return displayAttribute != null ? displayAttribute.Name : enumValue.ToString();
    }

    private static void AddRecord()
    {
        CodingRecord record = new();

        var dateInputs = GetDateInputs();
        record.DateStart = dateInputs[0];
        record.DateEnd = dateInputs[1];

        var dataAccess = new DataAccess();
        dataAccess.InsertRecord(record);
    }

    private static void ViewRecords(IEnumerable<CodingRecord> records)
    {
        var table = new Table();
        table.AddColumn("Id");
        table.AddColumn("Start Date");
        table.AddColumn("End Date");
        table.AddColumn("Duration");

        foreach (var record in records)
        {
            table.AddRow(
                record.Id.ToString(),
                record.DateStart.ToString(),
                record.DateEnd.ToString(),
                $"{record.Duration.TotalHours % 24} hours {record.Duration.TotalMinutes % 60} minutes"
            );
        }
        AnsiConsole.Write(table); // Accidentally missed this off earlier and was wondering why it wasn't displaying results!
    }

    private static void DeleteRecord()
    {
        var dataAccess = new DataAccess();

        var records = dataAccess.GetAllRecords();
        ViewRecords(records);

        var id = GetNumber("Please type the id of the habit you want to delete: ");

        if (!AnsiConsole.Confirm("\nAre you sure?"))
            return;

        var response = dataAccess.DeleteRecord(id);

        var responseMessage =
            response < 1
                ? $"\nRecord with the id {id} doesn't exist. Press any key to return to Main Menu"
                : "\nRecord deleted successfully. Press any key to return to Main Menu";

        System.Console.WriteLine(responseMessage);
        System.Console.ReadKey();
    }

    private static DateTime[] GetDateInputs()
    {
        var startDateInput = AnsiConsole.Ask<string>(
            "Input Start Date with the format: dd-mm-yy hh:mm (24 hour clock). Or enter 0 to return to main menu."
        );

        if (startDateInput == "0")
            MainMenu();

        var startDate = Validation.ValidateStartDate(startDateInput);

        var endDateInput = AnsiConsole.Ask<string>(
            "Input End Date with the format: dd-mm-yy hh:mm (24 hour clock). Or enter 0 to return to main menu."
        );

        if (endDateInput == "0")
            MainMenu();

        var endDate = Validation.ValidateEndDate(startDate, endDateInput);

        return [startDate, endDate];
    }

    internal static void MainMenu()
    {
        var isMenuRunning = true;

        while (isMenuRunning)
        {
            var usersChoice = AnsiConsole.Prompt(
                new SelectionPrompt<MainMenuChoices>()
                    .Title("\nWhat would you like to do?")
                    .AddChoices(Enum.GetValues(typeof(MainMenuChoices)).Cast<MainMenuChoices>())
                    .UseConverter(choice => GetEnumDisplayName(choice))
            );

            switch (usersChoice)
            {
                case MainMenuChoices.AddRecord:
                    AddRecord();
                    break;
                case MainMenuChoices.ViewRecords:
                    var dataAccess = new DataAccess();
                    var records = dataAccess.GetAllRecords();
                    ViewRecords(records);
                    break;
                case MainMenuChoices.UpdateRecord:
                    UpdateRecord();
                    break;
                case MainMenuChoices.DeleteRecord:
                    DeleteRecord();
                    break;
                case MainMenuChoices.Quit:
                    System.Console.WriteLine("\nGoodbye");
                    isMenuRunning = false;
                    break;
            }
        }
    }

    private static void UpdateRecord()
    {
        var dataAccess = new DataAccess();

        var records = dataAccess.GetAllRecords();
        ViewRecords(records);

        var id = GetNumber("\nPlease type the id of the habit you want to update: ");

        var record = records.Where(x => x.Id == id).Single();

        var dates = GetDateInputs();

        record.DateStart = dates[0];
        record.DateEnd = dates[1];

        dataAccess.UpdateRecord(record);
    }

    private static int GetNumber(string message)
    {
        string numberInput = AnsiConsole.Ask<string>(message);

        if (numberInput == "0")
            MainMenu();

        var output = Validation.ValidateInt(numberInput, message);

        return output;
    }
}

internal class CodingRecord
{
    internal int Id { get; set; }
    internal DateTime DateStart { get; set; }
    internal DateTime DateEnd { get; set; }
    internal TimeSpan Duration { get; set; }
}
