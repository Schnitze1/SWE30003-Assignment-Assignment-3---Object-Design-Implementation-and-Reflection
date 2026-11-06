using DmsDemo.Dms;

namespace DmsDemo.Dms.Ui;

public abstract class UiBase
{
    protected readonly DeliveryManagementSystem Dms;

    protected UiBase(DeliveryManagementSystem dms) => Dms = dms;

    protected static bool Running = true;

    protected static string Prompt(string message)
    {
        Console.Write(message);
        try { return Console.ReadLine()?.Trim() ?? ""; }
        catch (Exception) { Running = false; return ""; }
    }

    protected static void Error(string msg) => Console.WriteLine($"\n[ERROR] {msg}\n");
    protected static void Success(string msg) => Console.WriteLine($"\n[SUCCESS] {msg}\n");

    protected static string PromptValidated(string prompt, Func<string, string> validate)
    {
        while (true)
        {
            var val = Prompt(prompt);
            try { return validate(val); }
            catch (ValidationException e) { Error(e.Message); }
        }
    }

    protected void ShowInbox(string userId)
    {
        var messages = Dms.Inbox(userId);
        Console.WriteLine($"\n--- Inbox for {userId} ({messages.Count} messages) ---");
        if (messages.Count == 0) { Console.WriteLine("No messages."); return; }
        Console.WriteLine(new string('-', 65));
        for (var i = 0; i < messages.Count; i++)
        {
            var m = messages[i];
            Console.WriteLine($"[{i + 1}] {m.Timestamp} - {m.Title}");
            Console.WriteLine($"    {m.Message}");
        }
        Console.WriteLine(new string('-', 65));
    }
}